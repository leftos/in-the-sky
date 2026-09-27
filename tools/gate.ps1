#requires -Version 7
<#
.SYNOPSIS
Runs one gate command under a ceiling: the whole output to a log, the last lines on the screen, the command's own exit
status, and 124 when the ceiling killed it.

.DESCRIPTION
Usage: pwsh tools/gate.ps1 -Log <path> -TimeoutSeconds <n> [-Tail <n>] -- <command> [args...]

Why this exists, the two reasons tools/gate.sh gives in yaat, and the third this repo added:
 - Every line a command prints lands in an agent's context and is re-read on every later turn, so a build or a test run
   prints its last lines here and the rest stays in the log. Read or `rg` the log for the rest; never re-run the
   command to see its output again.
 - A pipeline reports the last command's status, so `cmd 2>&1 | Tee-Object log | Select-Object -Last 20` passes even
   when the build failed. This wrapper exits with the command's own status, and additionally fails when the log holds a
   known failure marker: a runner can print a green summary for stale binaries after "Build FAILED" and still exit 0.
 - A gate that hangs holds its caller until something else gives up, ten minutes and more. Every gate here is given a
   ceiling, and a run that reaches it is killed with its children and reported as 124, the status coreutils `timeout`
   uses, so no caller reads it as the command's own.

The command's standard output goes to -Log and its standard error to <log>.err, because Start-Process refuses to
redirect both to one file; once the process has ended the .err file is appended to the log and removed, so one file
holds everything, the output first and the errors after it. The command runs in the caller's working directory.

The three options below are read by hand out of $args rather than declared in a param block: a declared block sends
this script's own arguments through PowerShell's parameter binder, which reads the bare -- of
`pwsh tools/gate.ps1 -Log x -TimeoutSeconds 5 -- dotnet test -c Release` as a parameter name and stops with "the
parameter name '' is ambiguous" (PowerShell 7.5, 2026-09-14). A script with no param block is handed every word
untouched, separator and all, which is what lets the command keep its own -c. A caller in a session of its own
(`& tools/gate.ps1 ... -- dotnet build`) has the separator eaten by the parser before the script ever sees it, so the
command is read as everything past a --, or as the first word that is not one of the options above. It is then started
as it arrived, never through Invoke-Expression, so nothing in it is re-parsed by a shell.

.PARAMETER Log
Where the whole output is written, its directory created when missing, and an earlier run's log replaced. <log>.err
holds the command's standard error until the process ends and it is appended here.

.PARAMETER TimeoutSeconds
How long the command is given. A command still running then is killed with every process it started, the log gains a
`gate: TIMED OUT` line and the script exits 124. A ceiling is a few times what the command takes today: a run that
reaches one has hung, not slowed, so read the log rather than raise it.

.PARAMETER Tail
How many of the log's last lines are printed. Defaults to 20.

.OUTPUTS
On a failure, the log's failure marker lines with their line numbers; then the log's last -Tail lines, then one verdict
line: `gate: passed. Full output: <log>` on standard output, or `gate: FAILED (status <n>). Full output: <log>` on
standard error. The exit status is the command's own, 1 for a zero exit whose log reports a failure, 124 for the
ceiling, and 2 for a usage this script could not read.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# One regex for every line that means a gate failed even when the runner exited 0, plus this wrapper's own timeout line
# so that a gate wrapping a gate reports the inner one's ceiling.
$markers = '^Build FAILED\.|error CS\d+|: error |Test run summary: Failed!|^\s*failed: [1-9]|gate: TIMED OUT'
$usage = 'usage: pwsh tools/gate.ps1 -Log <path> -TimeoutSeconds <n> [-Tail <n>] -- <command> [args...]'

# The wrapper's own commentary goes to standard error, as gate.sh writes it, so a caller reading the command's output
# from the screen is not handed the gate's lines in the middle of it.
function Write-Gate {
    param([string]$Line)
    [Console]::Error.WriteLine($Line)
}

# Start-Process joins -ArgumentList with spaces and quotes nothing itself (Start-Process, Example 7:
# https://learn.microsoft.com/powershell/module/microsoft.powershell.management/start-process), so an argument holding
# a space - a test filter, a path under Program Files, a -Command script - is quoted here or the command reads it as
# two arguments.
function Format-Argument {
    param([string]$Value)
    if ($Value -match '\s' -and $Value -notmatch '"') {
        return '"' + $Value + '"'
    }
    return $Value
}

# taskkill walks the tree itself with /T, in one documented call, and reaches what a gate leaves behind that killing
# the parent alone does not: `dotnet test` starts a test host per project and `dotnet run` a server that outlives the
# run it was started as. A CIM walk of Win32_Process.ParentProcessId would do the same by hand and lose a branch whose
# parent has already gone.
function Stop-Tree {
    param([int]$ProcessId)
    & taskkill.exe /PID $ProcessId /T /F 2>&1 | Out-Null
}

# On Windows a bare command name is resolved to the first file on PATH that Windows can start (an extension PATHEXT
# lists). An extensionless script earlier on PATH - the bash shims a Claude Code plugin puts in front of `uv` and
# `python` for the Bash tool, which a pwsh started from Bash inherits - is otherwise what the name resolves to, and
# Start-Process cannot run it. A name with an extension or a directory is taken as written.
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

$options = @{ Log = ''; TimeoutSeconds = ''; Tail = '20' }
$words = @($args)
$command = @()
$read = 0
while ($read -lt $words.Count) {
    $word = [string]$words[$read]
    if ($word -eq '--') {
        $command = @($words | Select-Object -Skip ($read + 1))
        break
    }
    $name = $word -replace '^-', ''
    if ($word.StartsWith('-') -and $options.ContainsKey($name) -and $read + 1 -lt $words.Count) {
        $options[$name] = [string]$words[$read + 1]
        $read += 2
        continue
    }
    if ($word.StartsWith('-')) {
        Write-Gate "gate: cannot read the option $word"
        Write-Gate $usage
        exit 2
    }
    # A caller in its own session had the -- eaten by PowerShell's parser, so the command starts at the first word that
    # is not an option of this script's.
    $command = @($words | Select-Object -Skip $read)
    break
}

$Log = $options['Log']
$TimeoutSeconds = 0
[void][int]::TryParse($options['TimeoutSeconds'], [ref]$TimeoutSeconds)
$Tail = 20
[void][int]::TryParse($options['Tail'], [ref]$Tail)
if (-not $Log -or $TimeoutSeconds -le 0 -or $command.Count -lt 1) {
    Write-Gate $usage
    exit 2
}

$logDir = Split-Path -Parent $Log
if ($logDir -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Force $logDir | Out-Null
}
$errLog = "$Log.err"
Remove-Item $Log, $errLog -ErrorAction SilentlyContinue

$spoken = $command -join ' '
$start = @{
    FilePath               = Resolve-Runnable ([string]$command[0])
    NoNewWindow            = $true
    PassThru               = $true
    RedirectStandardOutput = $Log
    RedirectStandardError  = $errLog
}
if ($command.Count -gt 1) {
    $start['ArgumentList'] = @($command | Select-Object -Skip 1 | ForEach-Object { Format-Argument ([string]$_) })
}
$process = Start-Process @start

$timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) {
    Stop-Tree -ProcessId $process.Id
    # The kill is asynchronous, so the redirected files are written for a moment after it.
    $null = $process.WaitForExit(5000)
}

# Two files while the process runs, because Start-Process refuses one for both; one file to read afterwards.
if (Test-Path $errLog) {
    $errors = Get-Content -Path $errLog -Raw
    if ($errors) { Add-Content -Path $Log -Value $errors }
    Remove-Item $errLog -ErrorAction SilentlyContinue
}

if ($timedOut) {
    $status = 124
    $line = "gate: TIMED OUT after $TimeoutSeconds s; killed $spoken and its children"
    Add-Content -Path $Log -Value $line
    Write-Gate $line
}
else {
    $status = $process.ExitCode
    if ($status -eq 0 -and (Select-String -Path $Log -Pattern $markers -Quiet)) {
        Write-Gate "gate: command exited 0 but its log reports a failure -> $Log"
        $status = 1
    }
    if ($status -ne 0) {
        Select-String -Path $Log -Pattern $markers |
            Select-Object -First 40 |
            ForEach-Object { Write-Host "$($_.LineNumber):$($_.Line)" }
    }
}

Get-Content -Path $Log -Tail $Tail | ForEach-Object { Write-Host $_ }

if ($status -eq 0) {
    Write-Host "gate: passed. Full output: $Log"
}
else {
    Write-Gate "gate: FAILED (status $status). Full output: $Log"
}
exit $status
