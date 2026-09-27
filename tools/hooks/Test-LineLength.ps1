#Requires -Version 7.0
<#
.SYNOPSIS
    Refuses a commit whose staged C#, PowerShell or Python has a line over 150 characters.

.DESCRIPTION
    CSharpier and ruff wrap code at the 150-character line the repository sets (.csharpierrc, ruff.toml,
    .editorconfig), but neither wraps a comment or a string literal, so this reads every line of the files prek hands
    it. Each line over 150 characters prints as <path>:<line>: <n> characters, over the 150 the repository allows, and
    the hook exits 1; a clean run exits 0 in silence. With no file given there is nothing to read and it exits 0.

.PARAMETER Path
    The files to read, as prek passes them: the staged C#, PowerShell and Python files, one per argument.

.EXAMPLE
    pwsh tools/hooks/Test-LineLength.ps1 tools/test-all.ps1
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

$over = @()
foreach ($file in $Path) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        continue
    }
    $full = [System.IO.Path]::GetFullPath($file)
    $relative = [System.IO.Path]::GetRelativePath($RepoRoot, $full).Replace('\', '/')
    $lines = [System.IO.File]::ReadAllLines($full)
    for ($number = 1; $number -le $lines.Length; $number++) {
        $length = $lines[$number - 1].Length
        if ($length -gt 150) {
            $over += "${relative}:${number}: $length characters, over the 150 the repository allows"
        }
    }
}

foreach ($line in $over) {
    Write-Host $line
}

if ($over.Count -gt 0) {
    exit 1
}
exit 0
