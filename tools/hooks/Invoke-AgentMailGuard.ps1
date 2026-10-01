#Requires -Version 7.0
<#
.SYNOPSIS
    Refuses a commit that stages a file another Claude Code session holds an exclusive Agent Mail lease on.

.DESCRIPTION
    Hands the staged paths prek passes to the machine's Agent Mail guard, `~/.claude/tools/agent-mail/guard-check.ps1`,
    and exits with its code. Where that guard is not installed (CI, a contributor's clone), it says so in one line and
    exits 0, so the hook never blocks a commit on a machine without Agent Mail.

.EXAMPLE
    pwsh tools/hooks/Invoke-AgentMailGuard.ps1 src/Sky.Engine/Flight/FlightWorld.cs
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Files
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Files) {
    exit 0
}

$guard = Join-Path $HOME '.claude/tools/agent-mail/guard-check.ps1'
if (-not (Test-Path -LiteralPath $guard)) {
    Write-Output "agent-mail guard not installed at $guard; lease check skipped."
    exit 0
}

& pwsh -NoProfile -File $guard @Files
exit $LASTEXITCODE
