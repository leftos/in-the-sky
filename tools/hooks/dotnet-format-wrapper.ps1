#Requires -Version 7.0
<#
.SYNOPSIS
    Applies `dotnet format <subcommand>` fixers to the C# files prek hands this hook and stages what changed.

.DESCRIPTION
    `dotnet format` takes a project or solution in its positional slot, never files, so the staged files go in
    through `--include`. That option is variadic and therefore comes last. Fixers run at info severity, the same
    level the CI `--verify-no-changes` check uses. Modified files are re-staged so prek does not fail the commit
    on "files were modified by this hook"; the dotnet-build hook runs last and gates the result.

.EXAMPLE
    pwsh tools/hooks/dotnet-format-wrapper.ps1 style src/Foo/Bar.cs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('style', 'analyzers')]
    [string]$Subcommand,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Files
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Files) {
    exit 0
}

# IDE0059 (unused value) and IDE0060 (unused parameter) are never auto-fixed: the fixer deletes the dead store,
# and a dead store is usually a bug (a computed value that was meant to be used). CI keeps flagging them.
# IDE0130 (namespace matches folder) is excluded because its fixer crashes dotnet format ("Changing document
# properties is not supported"); the warnings-as-errors build reports it instead.
dotnet format $Subcommand --severity info --no-restore --exclude-diagnostics IDE0059 IDE0060 IDE0130 --include @Files
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

git add -- @Files
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
