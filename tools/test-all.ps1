#requires -Version 7
<#
.SYNOPSIS
Builds the solution once, then runs the format check, the tests, the Python analysis, the provenance check and the
150-character line check side by side under a ceiling each and ends on one verdict.

.DESCRIPTION
The build runs alone and first, because the tests and the format check read the binaries it writes and the tests are
told --no-build: a run that carried on past a failed build would report on the last binaries that did compile. It runs
under tools/gate.ps1, so it cannot hang the run, and the whole of its output is in .tmp/test-all-build.log.

The checks then run as jobs. Each job's output is held back and printed whole under its own heading once they have all
finished: two checks writing to one console interleave into something unreadable exactly when one of them fails. None
waits on another, so the run costs what its longest one costs.

Every command a check runs goes through tools/gate.ps1 with the check's ceiling, a few times what it takes on an idle
machine today, and its whole output in a log of its own, .tmp/test-all-<check>-<n>.log. The gate's watchdog kills a
command with every process it started and exits 124, the check fails with the gate's kill line as its row's note, and
the other checks are left to finish. The kill line says which of three things happened:
 - `gate: STALLED`: no output and no CPU for the stall time. The command hung; read its log for where it stopped.
 - `gate: TIMED OUT`: the ceiling ran out on the load-adjusted clock, which already allows for other work on the
   machine. The command kept working past it: a busy loop, or a ceiling set too tight. Read the log before raising it.
 - `gate: BACKSTOP`: five times the ceiling passed in wall time. With a low machine-free share on that line the
   machine was busy rather than the command wrong: run it once more alone.
Each gate takes one of the machine's slots (tools/gate.ps1 explains them), so the checks run side by side only when
enough slots are free; a check that finds none waits for one, and its clocks start when it has it.

The run carries a backstop of its own over all of that: -Ceiling seconds of wall time, after which every check still
running is stopped where it stands and reported as timed out. Every command is already killed by its own gate at five
times its ceiling at the latest, so this stop only has to catch a gate that hung itself.

MSBuild switches are written in dash form (-warnaserror): Git Bash's path conversion rewrites /warnaserror into a
Windows path and MSBuild then reports MSB1008, while the dash form reads the same from every shell.

.PARAMETER Ceiling
The run's wall-time bound in seconds, counted from its first line. When it is not given it is five times the largest
check's ceiling plus 60 s: the longest any one gate can run before its own backstop kills it, and a margin. Every check
still running at the bound is stopped and reported as timed out, and the run exits non-zero.

.OUTPUTS
Every check's output whole under its own heading, then a table of what ran, how it went and how long it took with the
note a stopped check carries, then the wall time.
#>
[CmdletBinding()]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '',
    Justification = 'Interactive dev script; coloured status to the console is the UX.')]
param(
    [int]$Ceiling
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$tmp = Join-Path $root '.tmp'
New-Item -ItemType Directory -Force $tmp | Out-Null
$startedAt = Get-Date

# Every check's ceiling in seconds, against what it took on a machine like this: build seconds, format tens of seconds,
# tests seconds, the Python analysis seconds, the provenance check seconds, the line check a second or two. A ceiling is
# a few times that, counted on the gate's load-adjusted clock, so a busy machine does not use it up.
$ceilings = [ordered]@{
    build         = 300
    format        = 180
    tests         = 180
    analysis      = 120
    provenance    = 60
    'line-length' = 60
}
# The run's own bound: every gate is killed at five times its ceiling at the latest, so past the largest of those and a
# margin only a gate that hung itself can still be running.
if (-not $PSBoundParameters.ContainsKey('Ceiling')) {
    $Ceiling = 5 * ($ceilings.Values | Measure-Object -Maximum).Maximum + 60
}

# Starts one check as a job of its own. Its commands are handed over as arrays rather than as a line to parse, so an
# argument holding a space stays one argument and a bare -- reaches the runner instead of PowerShell's own parser. The
# commands run in order, each through tools/gate.ps1 under the check's ceiling, and the first non-zero status ends the
# check.
function Start-Check {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Starts a background job of this run''s own; there is no system state for a caller to confirm.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseUsingScopeModifierInNewRunspaces', '',
        Justification = 'The job reads its values from its own param block and -ArgumentList, which $using: cannot be combined with.')]
    param([string]$Label, [object[]]$Commands, [int]$Seconds)
    $log = Join-Path $tmp "test-all-$Label.log"
    $pidFile = Join-Path $tmp "test-all-$Label.pid"
    Remove-Item $log, $pidFile -ErrorAction SilentlyContinue
    $job = Start-Job -Name $Label -ScriptBlock {
        param($workDir, $commands, $log, $pidFile, $label, $seconds)
        Set-Location $workDir
        # A job runs in a process of its own, and its id is what the tree of a check the run's backstop stops is
        # killed from.
        Set-Content -Path $pidFile -Value $PID

        $lines = [System.Collections.Generic.List[string]]::new()
        $code = 0
        $kill = ''
        $index = 0
        foreach ($command in $commands) {
            $index++
            $commandLog = ".tmp/test-all-$label-$index.log"
            $header = "== $($command -join ' ') (whole output: $commandLog) =="
            Add-Content -Path $log -Value $header
            $lines.Add($header)
            $gateArguments = @('-NoProfile', '-File', 'tools/gate.ps1', '-Log', $commandLog, '-TimeoutSeconds', "$seconds", '--') + @($command)
            # Written to the log as it goes rather than collected at the end: a check stopped at the run's backstop
            # never returns its result, and the log is then the only place its partial output can be read from.
            $text = & pwsh @gateArguments 2>&1 | Out-String -Stream | Tee-Object -FilePath $log -Append
            foreach ($line in $text) { $lines.Add([string]$line) }
            if ($LASTEXITCODE -ne 0) {
                $code = $LASTEXITCODE
                if ($code -eq 124) {
                    $kill = [string](Select-String -Path $commandLog -Pattern 'gate: (STALLED|TIMED OUT|BACKSTOP)' |
                            Select-Object -Last 1 | ForEach-Object { $_.Line })
                }
                break
            }
        }
        [pscustomobject]@{
            Output   = $lines
            ExitCode = $code
            KillLine = $kill
        }
    } -ArgumentList $root, $Commands, $log, $pidFile, $Label, $Seconds
    [pscustomobject]@{
        Label    = $Label
        Job      = $job
        Log      = $log
        PidFile  = $pidFile
        TimedOut = $false
    }
}

function Test-Running {
    param($Check)
    return $Check.Job.State -eq 'Running' -or $Check.Job.State -eq 'NotStarted'
}

# The pid a check's job process wrote, or 0 before it has.
function Read-CheckPid {
    param($Check)
    $processId = 0
    if (Test-Path $Check.PidFile) {
        [void][int]::TryParse((Get-Content -Path $Check.PidFile -Raw).Trim(), [ref]$processId)
    }
    return $processId
}

# The processes a check's job process started since it began: the gate it is running, when it is running one.
function Get-CheckChild {
    param([int]$ParentId)
    $parent = Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if (-not $parent) { return @() }
    return @(Get-Process | Where-Object { $_.Parent -and $_.Parent.Id -eq $ParentId -and $_.StartTime -ge $parent.StartTime } |
            ForEach-Object { $_.Id })
}

function Invoke-StopTree {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Stops processes of this run''s own; there is no system state for a caller to confirm.')]
    param([int]$Id)
    # A child that exits between being listed and being stopped leaves nothing to stop, and -StopTree says so with
    # status 2 and its usage; that is not worth printing.
    if (-not (Get-Process -Id $Id -ErrorAction SilentlyContinue)) { return }
    & pwsh -NoProfile -File tools/gate.ps1 -StopTree $Id 2>&1 | Out-Host
}

# Every check still running at the run's backstop is stopped where it stands, with tools/gate.ps1 -StopTree on the gate
# its job's process is running: that ends the gate's job, and with it every process the gate's command started, then
# the tree under the gate. A job holds what a parent chain loses: taskkill /T follows parent pids, so a test host whose
# parent had already exited survived it, where the gate's job still holds it. The job's own process is left alive, so
# it reads the gate's non-zero status, stops its loop and completes: once a job's process is killed, PowerShell's job
# transport takes about a minute to notice, and Stop-Job, Remove-Job and the run's own exit all wait on it (measured
# 2026-09-27). A job still running 10 s later, caught between two of its commands, has its own process stopped too.
function Stop-Running {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Stops background jobs of this run''s own; there is no system state for a caller to confirm.')]
    param([object[]]$Running)
    foreach ($check in $Running) {
        Write-Host "the run reached its backstop of $Ceiling s (-Ceiling); stopping $($check.Label)." -ForegroundColor Red
        $processId = Read-CheckPid $check
        if ($processId -gt 0) { foreach ($child in (Get-CheckChild $processId)) { Invoke-StopTree $child } }
        $check.TimedOut = $true
    }
    $null = Wait-Job -Job @($Running | ForEach-Object { $_.Job }) -Timeout 10
    foreach ($check in @($Running | Where-Object { Test-Running $_ })) {
        $processId = Read-CheckPid $check
        if ($processId -gt 0) { Invoke-StopTree $processId }
    }
}

# Prints one finished check's captured output and returns its row of the table.
function Complete-Check {
    param($Check)
    $job = $Check.Job
    Write-Host "`n=== $($Check.Label) ===" -ForegroundColor Cyan

    $seconds = 0
    if ($job.PSBeginTime -and $job.PSEndTime) {
        $seconds = [math]::Round(($job.PSEndTime - $job.PSBeginTime).TotalSeconds)
    }

    # A stopped job returns no result at all, so what it did manage to say is read back from the log it was writing.
    if ($Check.TimedOut) {
        if (Test-Path $Check.Log) { Get-Content -Path $Check.Log -Tail 100 | Out-Host }
        $why = "stopped at the run's backstop of $Ceiling s; its output so far is in $($Check.Log)"
        Write-Host "FAILED: $($Check.Label) ($why)" -ForegroundColor Red
        Remove-Job -Job $job -Force
        Remove-Item $Check.PidFile -ErrorAction SilentlyContinue
        return [pscustomobject]@{
            Check = $Check.Label; Verdict = 'failed'; Seconds = $seconds; Note = "timed out at the run's backstop of $Ceiling s"
        }
    }

    $result = $null
    try {
        $result = Receive-Job -Job $job -ErrorAction Stop
    }
    catch {
        Write-Host $_ -ForegroundColor Red
    }
    finally {
        Remove-Job -Job $job -Force
        Remove-Item $Check.PidFile -ErrorAction SilentlyContinue
    }

    # A job that died without returning its result object is a failure however it reads: a missing verdict is not a
    # green one.
    if ($null -eq $result) {
        Write-Host "FAILED: $($Check.Label) (the check produced no result)" -ForegroundColor Red
        return [pscustomobject]@{ Check = $Check.Label; Verdict = 'failed'; Seconds = $seconds; Note = "see $($Check.Log)" }
    }

    $result.Output | Out-Host
    if ($result.ExitCode -ne 0) {
        Write-Host "FAILED: $($Check.Label) (exit $($result.ExitCode))" -ForegroundColor Red
        # A command the gate's watchdog killed carries the gate's own line, which says whether it stalled, ran past its
        # ceiling or met the backstop on a busy machine.
        $note = if ($result.ExitCode -eq 124 -and $result.KillLine) { $result.KillLine } else { "see $($Check.Log)" }
        return [pscustomobject]@{ Check = $Check.Label; Verdict = 'failed'; Seconds = $seconds; Note = $note }
    }

    Write-Host "OK: $($Check.Label)" -ForegroundColor Green
    return [pscustomobject]@{ Check = $Check.Label; Verdict = 'passed'; Seconds = $seconds; Note = '' }
}

# The checks below read what the build writes or the tree it was built from - the tests are told --no-build, the
# analysis, provenance and line checks read the tree - so the build is the one step that runs on its own and the run ends
# here when it fails.
Write-Host '=== build ===' -ForegroundColor Cyan
$buildLog = Join-Path $tmp 'test-all-build.log'
pwsh tools/gate.ps1 -Log $buildLog -TimeoutSeconds $ceilings['build'] -- dotnet build InTheSky.slnx -c Release -warnaserror
$buildStatus = $LASTEXITCODE
$buildSeconds = [math]::Round(((Get-Date) - $startedAt).TotalSeconds)
$results = @()
if ($buildStatus -ne 0) {
    $why = "the tests and the format check read what it writes, and a red build is the run's verdict whatever the rest would say"
    Write-Host "The build failed, so no check was started: $why; the whole output is in $buildLog" -ForegroundColor Red
    $results += [pscustomobject]@{ Check = 'build'; Verdict = 'failed'; Seconds = $buildSeconds; Note = "see $buildLog" }
    Write-Host ''
    $results | Format-Table -AutoSize | Out-Host
    exit 1
}
Write-Host "build passed in $buildSeconds s." -ForegroundColor Green
$results += [pscustomobject]@{ Check = 'build'; Verdict = 'passed'; Seconds = $buildSeconds; Note = '' }

$checks = @()
# The leading comma keeps one command an array of arrays; @() alone would flatten it into its words. The solution is
# handed to a single runner invocation, which runs its test assemblies side by side.
$testCommands = @(
    , @('dotnet', 'test', 'InTheSky.slnx', '-c', 'Release', '--no-build')
)
$checks += Start-Check 'tests' $testCommands $ceilings['tests']
# The format check is a job beside the tests rather than a step before the build: --verify-no-changes writes nothing,
# so it reads the same tree the build reads and costs the run nothing but one core. `dotnet format` is never called
# bare here - its whitespace pass undoes what csharpier wrote - so style and analyzers are named one at a time.
$formatCommands = @(
    @('dotnet', 'csharpier', 'check', '.'),
    @('dotnet', 'format', 'style', 'InTheSky.slnx', '--verify-no-changes', '--severity', 'info'),
    @('dotnet', 'format', 'analyzers', 'InTheSky.slnx', '--verify-no-changes', '--severity', 'info')
)
$checks += Start-Check 'format' $formatCommands $ceilings['format']
# The asset provenance gate: every asset file git carries must have a ledger entry, and CREDITS.md must match the ledger.
# It reads the tree the build reads rather than the build's output, so it runs beside the other checks with no order of
# its own. The leading comma keeps the one command an array of arrays, as above.
$provenanceCommands = @(
    , @('uv', 'run', '--locked', '--project', 'tools/provenance', 'python', '-m', 'provenance', 'check')
)
$checks += Start-Check 'provenance' $provenanceCommands $ceilings['provenance']
# The 150-character line check is a job here as well as a prek hook: prek runs it at commit and not before, so without
# this row a green whole gate could still fail the commit. Like the provenance check it reads the tree rather than the
# build's output, so it runs beside the others with no order of its own. The leading comma keeps the one command an
# array of arrays, as above.
$lineLengthCommands = @(
    , @('pwsh', 'tools/hooks/Test-LineLength.ps1', '-All')
)
$checks += Start-Check 'line-length' $lineLengthCommands $ceilings['line-length']

# Every project under tools/ that carries a pyproject.toml is checked here in the four steps a project
# `sky.ps1 analysis -Check` runs, none of which writes. The list is read now, so a project landing under tools/ later
# is checked without an edit here; with none present there is nothing to check and the row says so.
# uv is given --directory rather than --project: --project picks the project uv resolves but leaves the working
# directory where this script stands, and ruff, ty and pytest each read their configuration from the directory they run
# in or above it (ty's --project defaults to the working directory and walks up from there, ty docs reference/cli.md).
# From the repo root, which holds no pyproject.toml, a project's pyproject.toml would then never be read.
# uv is given --locked, as CI gives it, in this block and in the provenance command above: a uv.lock that no longer
# resolves fails here instead of being rewritten to a resolution nobody reviews.
$uvProjects = @(
    Get-ChildItem -Path (Join-Path $root 'tools/*/pyproject.toml') -File -ErrorAction SilentlyContinue |
        ForEach-Object { "tools/$($_.Directory.Name)" }
)
$analysisCheck = $null
if ($uvProjects.Count -eq 0) {
    Write-Host 'analysis: no uv projects'
}
else {
    $analysisCommands = @(
        foreach ($project in $uvProjects) {
            , @('uv', 'run', '--locked', '--directory', $project, 'ruff', 'format', '--check', '.')
            , @('uv', 'run', '--locked', '--directory', $project, 'ruff', 'check', '.')
            , @('uv', 'run', '--locked', '--directory', $project, 'ty', 'check', '.')
            , @('uv', 'run', '--locked', '--directory', $project, 'pytest', '.', '-q')
        }
    )
    $analysisCheck = Start-Check 'analysis' $analysisCommands $ceilings['analysis']
    $checks += $analysisCheck
}

Write-Host "`nRunning $($checks.Count) checks side by side, each command under the gate's watchdog..." -ForegroundColor Cyan
$whole = $startedAt.AddSeconds($Ceiling)
while ($true) {
    $running = @($checks | Where-Object { Test-Running $_ })
    if ($running.Count -eq 0) { break }
    if ((Get-Date) -ge $whole) {
        Stop-Running $running
        break
    }
    Start-Sleep -Milliseconds 500
}

foreach ($label in @('tests', 'format', 'provenance', 'line-length')) {
    $check = $checks | Where-Object { $_.Label -eq $label }
    if ($check) { $results += Complete-Check $check }
}
if ($analysisCheck) {
    $results += Complete-Check $analysisCheck
}
else {
    $results += [pscustomobject]@{ Check = 'analysis'; Verdict = 'passed'; Seconds = 0; Note = 'no uv projects' }
}

Write-Host ''
$results | Format-Table -AutoSize | Out-Host

$failed = @($results | Where-Object { $_.Verdict -eq 'failed' })
$elapsed = [math]::Round(((Get-Date) - $startedAt).TotalSeconds)
$wall = "Wall time: $elapsed s (every command runs under the gate's watchdog, and the run stops itself at $Ceiling s)."
if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) of $($results.Count) checks failed." -ForegroundColor Red
    Write-Host $wall
    exit 1
}

Write-Host 'All checks passed.' -ForegroundColor Green
Write-Host $wall
exit 0
