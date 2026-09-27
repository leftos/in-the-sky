#Requires -Version 7.0
<#
.SYNOPSIS
    Refuses a commit whose staged PowerShell raises a PSScriptAnalyzer finding.

.DESCRIPTION
    Runs Invoke-ScriptAnalyzer over the files prek hands it, under PSScriptAnalyzerSettings.psd1 at the repository
    root: errors and warnings fail, informationals are not reported, and the two rules the settings file excludes
    say there why. Every finding prints as `<path>:<line>: <RuleName>: <Message>` and the hook exits 1; a clean run
    exits 0 in silence. With no file given there is nothing to read and it exits 0.

    The analyzer is not part of PowerShell itself: without the PSScriptAnalyzer module the hook exits 1 saying how
    to install it, rather than passing a commit nothing checked.

.PARAMETER Path
    The files to analyze, as prek passes them: the staged PowerShell scripts, one per argument.

.EXAMPLE
    pwsh tools/hooks/Invoke-ScriptAnalyzerHook.ps1 tools/test-all.ps1
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Path) {
    exit 0
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Settings = Join-Path $RepoRoot 'PSScriptAnalyzerSettings.psd1'

if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
    Write-Host (
        'Invoke-ScriptAnalyzerHook: PSScriptAnalyzer is not installed; run Install-Module PSScriptAnalyzer ' +
        '-Scope CurrentUser and commit again.'
    )
    exit 1
}
Import-Module PSScriptAnalyzer

$findings = @()
foreach ($file in $Path) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        continue
    }
    $findings += @(Invoke-ScriptAnalyzer -Path $file -Settings $Settings)
}

foreach ($finding in $findings) {
    $relative = [System.IO.Path]::GetRelativePath($RepoRoot, $finding.ScriptPath).Replace('\', '/')
    Write-Host "${relative}:$($finding.Line): $($finding.RuleName): $($finding.Message)"
}

if ($findings.Count -gt 0) {
    exit 1
}
exit 0
