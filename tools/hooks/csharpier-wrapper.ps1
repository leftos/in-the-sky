#Requires -Version 7.0
<#
.SYNOPSIS
    Formats the C# files prek hands this hook and stages what changed.

.DESCRIPTION
    Runs `dotnet csharpier format` on the files passed by prek, then re-stages any modifications so prek does
    not fail the commit on "files were modified by this hook". The dotnet-build hook runs last and gates the
    commit on the formatted result still compiling.

    It runs under pwsh rather than bash because a bare `dotnet` in a bash script is resolved by whichever bash
    prek's parent process finds, which from pwsh can be the WSL stub that has no dotnet on its path.

.EXAMPLE
    pwsh tools/hooks/csharpier-wrapper.ps1 src/Sky.Engine/Decision.cs
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

dotnet csharpier format @Files
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

git add -- @Files
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
