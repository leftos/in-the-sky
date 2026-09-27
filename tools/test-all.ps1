#requires -Version 7
<#
.SYNOPSIS
Builds the solution once, then runs the format check, the tests and the Python analysis checks side by side under a
ceiling each and ends on one verdict.

.DESCRIPTION
The build runs alone and first, because every check after it reads the binaries it writes and the tests are told
--no-build: a run that carried on past a failed build would report on the last binaries that did compile. It runs under
tools/gate.ps1, so it cannot hang the run, and the whole of its output is in .tmp/test-all-build.log.

The checks then run as jobs. Each job's output is held back and printed whole under its own heading once they have all
finished: two checks writing to one console interleave into something unreadable exactly when one of them fails. None
waits on another, so the run costs what its longest one costs, the format check today.

Every check carries a ceiling of its own, a few times what it takes on this machine today, and the run carries -Ceiling
over both of them. A check that reaches its ceiling is stopped where it stands with every process it started, and its
row reads `timed out after <n> s` with the verdict failed while the other is left to finish. A red `timed out` row is a
hang and not a slow machine: read the check's log under .tmp rather than raise the ceiling.

MSBuild switches are written in dash form (-warnaserror): Git Bash's path conversion rewrites /warnaserror into a
Windows path and MSBuild then reports MSB1008, while the dash form reads the same from every shell.

.PARAMETER Ceiling
How long the whole run may take, in seconds, counted from its first line. Defaults to 600, which is the largest
check's ceiling with the build's and a margin beside it. Every check still running then is stopped and reported as
timed out, and the run exits non-zero.

.OUTPUTS
Every check's output whole under its own heading, then a table of what ran, how it went and how long it took with the
note a stopped check carries, then the wall time.
#>
[CmdletBinding()]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '',
    Justification = 'Interactive dev script; coloured status to the console is the UX.')]
param(
    [int]$Ceiling = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$tmp = Join-Path $root '.tmp'
New-Item -ItemType Directory -Force $tmp | Out-Null
$startedAt = Get-Date

# Every check's ceiling in seconds, against what it took on a machine like this: build seconds, format tens of seconds,
# tests seconds, the Python analysis seconds. A ceiling is a few times that, so a check that reaches one has hung
# rather than slowed.
$ceilings = [ordered]@{
    build    = 300
    format   = 180
    tests    = 180
    analysis = 120
}

# Starts one check as a job of its own. Its commands are handed over as arrays rather than as a line to parse, so an
# argument holding a space stays one argument and a bare -- reaches the runner instead of PowerShell's own parser. The
# commands run in order and the first non-zero status ends the check.
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
        param($workDir, $commands, $log, $pidFile)
        Set-Location $workDir
        # A job runs in a process of its own, and its id is what the tree of a check that overruns is killed from.
        Set-Content -Path $pidFile -Value $PID
        # On Windows a bare command name is resolved to the first file on PATH that Windows can start (an extension
        # PATHEXT lists). An extensionless script earlier on PATH - the bash shims a Claude Code plugin puts in front of
        # `uv` and `python` for the Bash tool, which a pwsh started from Bash inherits - is otherwise what `&` runs, and
        # it fails with "Cannot run a document in the middle of a pipeline". tools/gate.ps1 resolves the same way.
        function Resolve-Runnable {
            param([string]$Name)
            if (-not $IsWindows -or [System.IO.Path]::GetExtension($Name) -or [System.IO.Path]::GetDirectoryName($Name)) {
                return $Name
            }
            $runnable = $env:PATHEXT -split ';'
            $found = Get-Command $Name -CommandType Application -All -ErrorAction SilentlyContinue |
                Where-Object { $runnable -contains $_.Extension } |
                Select-Object -First 1
            if ($found) { return $found.Source }
            return $Name
        }

        $lines = [System.Collections.Generic.List[string]]::new()
        $code = 0
        foreach ($command in $commands) {
            $exe = Resolve-Runnable ([string]$command[0])
            $arguments = @($command | Select-Object -Skip 1)
            $header = "== $exe $($arguments -join ' ') =="
            Add-Content -Path $log -Value $header
            $lines.Add($header)
            # Written to the log as it goes rather than collected at the end: a check stopped at its ceiling never
            # returns its result, and the log is then the only place its partial output can be read from.
            $text = & $exe @arguments 2>&1 | Out-String -Stream | Tee-Object -FilePath $log -Append
            foreach ($line in $text) { $lines.Add([string]$line) }
            if ($LASTEXITCODE -ne 0) {
                $code = $LASTEXITCODE
                break
            }
        }
        [pscustomobject]@{
            Output   = $lines
            ExitCode = $code
        }
    } -ArgumentList $root, $Commands, $log, $pidFile
    [pscustomobject]@{
        Label     = $Label
        Job       = $job
        Seconds   = $Seconds
        Log       = $log
        PidFile   = $pidFile
        StartedAt = Get-Date
        TimedOut  = $false
    }
}

function Test-Running {
    param($Check)
    return $Check.Job.State -eq 'Running' -or $Check.Job.State -eq 'NotStarted'
}

# A check that reached its ceiling is stopped where it stands. The job's children go first, while the tree is still
# whole and taskkill /T can walk it: the dotnet a check started and the test hosts below it all sit under the job's own
# process, and stopping the job alone would orphan them. The job itself is stopped after that.
function Stop-Check {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Stops a background job of this run''s own; there is no system state for a caller to confirm.')]
    param($Check)
    $processId = 0
    if (Test-Path $Check.PidFile) {
        [void][int]::TryParse((Get-Content -Path $Check.PidFile -Raw).Trim(), [ref]$processId)
    }
    if ($processId -gt 0) {
        $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $processId" -ErrorAction SilentlyContinue)
        foreach ($child in $children) {
            & taskkill.exe /PID $child.ProcessId /T /F 2>&1 | Out-Null
        }
    }
    Stop-Job -Job $Check.Job -ErrorAction SilentlyContinue
    $Check.TimedOut = $true
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
        Write-Host "FAILED: $($Check.Label) (timed out after $($Check.Seconds) s; its output so far is in $($Check.Log))" -ForegroundColor Red
        Remove-Job -Job $job -Force
        Remove-Item $Check.PidFile -ErrorAction SilentlyContinue
        return [pscustomobject]@{
            Check = $Check.Label; Verdict = 'failed'; Seconds = $seconds; Note = "timed out after $($Check.Seconds) s"
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
        return [pscustomobject]@{ Check = $Check.Label; Verdict = 'failed'; Seconds = $seconds; Note = "see $($Check.Log)" }
    }

    Write-Host "OK: $($Check.Label)" -ForegroundColor Green
    return [pscustomobject]@{ Check = $Check.Label; Verdict = 'passed'; Seconds = $seconds; Note = '' }
}

# Both checks below read what the build writes, and the tests are told --no-build, so the build is the one step that
# runs on its own and the run ends here when it fails.
Write-Host '=== build ===' -ForegroundColor Cyan
$buildLog = Join-Path $tmp 'test-all-build.log'
pwsh tools/gate.ps1 -Log $buildLog -TimeoutSeconds $ceilings['build'] -- dotnet build InTheSky.slnx -c Release -warnaserror
$buildStatus = $LASTEXITCODE
$buildSeconds = [math]::Round(((Get-Date) - $startedAt).TotalSeconds)
$results = @()
if ($buildStatus -ne 0) {
    $why = "the format check and the tests read what it writes, and a red build is the run's verdict whatever the rest would say"
    Write-Host "The build failed, so no check was started: $why; the whole output is in $buildLog" -ForegroundColor Red
    $results += [pscustomobject]@{ Check = 'build'; Verdict = 'failed'; Seconds = $buildSeconds; Note = "see $buildLog" }
    Write-Host ''
    $results | Format-Table -AutoSize | Out-Host
    exit 1
}
Write-Host "build passed in $buildSeconds s." -ForegroundColor Green
$results += [pscustomobject]@{ Check = 'build'; Verdict = 'passed'; Seconds = $buildSeconds; Note = '' }

$checks = @()
# The leading comma keeps one command an array of arrays; @() alone would flatten it into its words. The runner's own
# guard after the separator sits beside the ceiling this script keeps: --timeout is a global test execution timeout the
# platform forwards to every test module, so one test that never completes ends its module rather than the whole run.
# The solution is handed to a single runner invocation, which runs its test assemblies side by side.
$testCommands = @(
    , @('dotnet', 'test', 'InTheSky.slnx', '-c', 'Release', '--no-build', '--', '--timeout', '2m')
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

# Every project under tools/ that carries a pyproject.toml is checked here in the four steps a project
# `sky.ps1 analysis -Check` runs, none of which writes. The list is read now, so a project landing under tools/ later
# is checked without an edit here; with none present there is nothing to check and the row says so.
# uv is given --directory rather than --project: --project picks the project uv resolves but leaves the working
# directory where this script stands, and ruff, ty and pytest each read their configuration from the directory they run
# in or above it (ty's --project defaults to the working directory and walks up from there, ty docs reference/cli.md).
# From the repo root, which holds no pyproject.toml, a project's pyproject.toml would then never be read.
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
            , @('uv', 'run', '--directory', $project, 'ruff', 'format', '--check', '.')
            , @('uv', 'run', '--directory', $project, 'ruff', 'check', '.')
            , @('uv', 'run', '--directory', $project, 'ty', 'check', '.')
            , @('uv', 'run', '--directory', $project, 'pytest', '.', '-q')
        }
    )
    $analysisCheck = Start-Check 'analysis' $analysisCommands $ceilings['analysis']
    $checks += $analysisCheck
}

Write-Host "`nRunning $($checks.Count) checks side by side, each under its own ceiling..." -ForegroundColor Cyan
$whole = $startedAt.AddSeconds($Ceiling)
while ($true) {
    $running = @($checks | Where-Object { Test-Running $_ })
    if ($running.Count -eq 0) { break }
    $now = Get-Date
    foreach ($check in $running) {
        if ($now -ge $check.StartedAt.AddSeconds($check.Seconds)) {
            Write-Host "$($check.Label) reached its ceiling of $($check.Seconds) s; stopping it and leaving the rest to finish." -ForegroundColor Red
            Stop-Check $check
        }
    }
    if ((Get-Date) -ge $whole) {
        foreach ($check in @($checks | Where-Object { Test-Running $_ })) {
            Write-Host "the run reached its own ceiling of $Ceiling s; stopping $($check.Label)." -ForegroundColor Red
            Stop-Check $check
        }
        break
    }
    Start-Sleep -Milliseconds 500
}

foreach ($label in @('tests', 'format')) {
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
$wall = "Wall time: $elapsed s (no check waits past its own ceiling and the run stops itself at $Ceiling s)."
if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) of $($results.Count) checks failed." -ForegroundColor Red
    Write-Host $wall
    exit 1
}

Write-Host 'All checks passed.' -ForegroundColor Green
Write-Host $wall
exit 0
