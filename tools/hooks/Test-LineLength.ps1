#Requires -Version 7.0
<#
.SYNOPSIS
    Refuses a commit whose staged C#, PowerShell or Python has a line over 150 characters; with -All it reads the whole
    tree instead.

.DESCRIPTION
    CSharpier and ruff wrap code at the 150-character line the repository sets (.csharpierrc, ruff.toml,
    .editorconfig), but neither wraps a comment or a string literal, so this reads every line of the files prek hands
    it. Each line over 150 characters prints as <path>:<line>: <n> characters, over the 150 the repository allows, and
    the hook exits 1; a clean run exits 0 in silence. With no file given there is nothing to read and it exits 0.

    -All reads every C#, PowerShell and Python file git tracks or would track, whatever Path holds, so the whole gate
    can run the same check between commits and not only prek at commit time.

.PARAMETER Path
    The files to read, as prek passes them: the staged C#, PowerShell and Python files, one per argument.

.PARAMETER All
    Read every C#, PowerShell and Python file git tracks or would track under the repository root, ignoring Path.

.EXAMPLE
    pwsh tools/hooks/Test-LineLength.ps1 tools/test-all.ps1

.EXAMPLE
    pwsh tools/hooks/Test-LineLength.ps1 -All
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$Path,
    [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

if ($All) {
    # The file types prek's line-length hook is tagged for - types_or = ["c#", "powershell", "python"] in prek.toml -
    # which for the extensions this tree carries are these five and no other. --others adds the files git would track,
    # so a file is read before its first commit as well as after it.
    $Path = @(
        & git -C $RepoRoot ls-files --cached --others --exclude-standard -- '*.cs' '*.ps1' '*.psm1' '*.psd1' '*.py'
    )
    if ($LASTEXITCODE -ne 0) {
        Write-Host "git ls-files failed with exit $LASTEXITCODE, so no file was read"
        exit 1
    }
}

if (-not $Path) {
    exit 0
}

$over = @()
foreach ($file in $Path) {
    # git lists its paths relative to the repository root and prek runs its hooks there; resolving against the root
    # rather than the directory this ran from keeps -All whole from wherever it is called.
    $full = [System.IO.Path]::GetFullPath($file, $RepoRoot)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        continue
    }
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
