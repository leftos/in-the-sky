#Requires -Version 7.0
<#
.SYNOPSIS
    Refuses a commit whose docs no longer name what the tree holds.

.DESCRIPTION
    Default mode checks the structure the docs promise to describe and prints one line per miss:
      docs/TEST_ALMANAC.md   names every tests/Sky.*.Tests project, as its backticked directory or its
                             backticked name, and every class whose name ends in Tests under tests/ in
                             backticks.
      docs/ARCHITECTURE.md   names every src/Sky.* project, in backticks.
    Exit 1 when a name is missing, 0 when the docs are current.

    -CommitMessage <path> runs the commit-message check instead: a commit that stages anything under
    src/Sky.Content/ must also stage a design doc under docs/design/, or carry a line starting
    "Docs: unchanged, " in the message body. A commit that stages no content exits 0 without reading the
    message. The commit-msg stage hands the message file as the last argument, so a bare path works too.

    -Update rewrites the "## Counts" rows of docs/TEST_ALMANAC.md from the real counts, changing nothing else.

.PARAMETER CommitMessage
    Path to the commit message file. Its presence selects the commit-message check.

.PARAMETER Update
    Rewrite the "## Counts" table of docs/TEST_ALMANAC.md from the tree.

.PARAMETER Root
    The repository to read. Defaults to the checkout this script lives in.

.PARAMETER StagedPaths
    Test-only override for `git diff --cached --name-only`, so the refusal path can be exercised without
    staging a commit. Production runs never pass it.

.EXAMPLE
    pwsh tools/hooks/Test-DocDrift.ps1

.EXAMPLE
    pwsh tools/hooks/Test-DocDrift.ps1 -Update

.EXAMPLE
    pwsh tools/hooks/Test-DocDrift.ps1 -CommitMessage .git/COMMIT_EDITMSG
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$CommitMessage = '',
    [switch]$Update,
    [string]$Root = (Join-Path $PSScriptRoot '../..'),
    [string[]]$StagedPaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Backtick = [string][char]96
$ClassPattern = '(?m)^\s*(public|internal)?\s*(sealed\s+|static\s+)?class\s+(\w+Tests)\b'
$SkipPattern = '(^|[\\/])(bin|obj|\.tmp|\.git|\.claude)([\\/]|$)'
$AlmanacName = 'docs/TEST_ALMANAC.md'
$ArchitectureName = 'docs/ARCHITECTURE.md'
$ContentPrefix = 'src/Sky.Content/'
$ContentDocPrefix = 'docs/design/'
$ContentMessage = 'doc-drift: a commit touching src/Sky.Content/ stages a design doc under ' +
'docs/design/ or says why on a line starting "Docs: unchanged, " ' +
'(each design doc''s owning agent is listed in CLAUDE.md, "Workflow")'

function Test-BacktickedName {
    param([string]$Text, [string]$Name)
    $quoted = [regex]::Escape($Name)
    [regex]::IsMatch($Text, "$Backtick(?:tests/)?$quoted$Backtick")
}

function Test-SkippedPath {
    param([string]$FullName)
    [regex]::IsMatch([System.IO.Path]::GetRelativePath($Root, $FullName), $SkipPattern)
}

function Resolve-InputPath {
    param([string]$Path, [string]$What)
    if (-not (Test-Path -LiteralPath $Path)) { throw "$What not found: $Path" }
    (Resolve-Path -LiteralPath $Path).Path
}

function Get-SourceFile {
    param([string]$Directory)
    if (-not (Test-Path -LiteralPath $Directory)) { return @() }
    @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.cs' |
        Where-Object { -not (Test-SkippedPath -FullName $_.FullName) } |
        Sort-Object FullName)
}

function Get-ChildDirectory {
    param([string]$Directory)
    if (-not (Test-Path -LiteralPath $Directory)) { return @() }
    @(Get-ChildItem -LiteralPath $Directory -Directory |
        Where-Object { -not (Test-SkippedPath -FullName $_.FullName) } |
        Sort-Object Name)
}

function Get-TestProject {
    Get-ChildDirectory -Directory (Join-Path $Root 'tests') | Where-Object { $_.Name -like 'Sky.*.Tests' }
}

function Get-SourceProject {
    Get-ChildDirectory -Directory (Join-Path $Root 'src') | Where-Object { $_.Name -like 'Sky.*' }
}

function Get-TestClassName {
    param([string]$Directory)
    $found = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-SourceFile -Directory $Directory) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        foreach ($match in [regex]::Matches($text, $ClassPattern)) {
            $name = $match.Groups[3].Value
            if (-not $found.Contains($name)) { $found.Add($name) }
        }
    }
    $found
}

function Format-Miss {
    param([string]$What, [string]$Name, [string]$Doc)
    "doc-drift: $What '$Name' is not named in $Doc"
}

function Get-StructureMiss {
    $almanac = [System.IO.File]::ReadAllText((Resolve-InputPath -Path (Join-Path $Root $AlmanacName) -What 'Test almanac'))
    $architecture = [System.IO.File]::ReadAllText((Resolve-InputPath -Path (Join-Path $Root $ArchitectureName) -What 'Architecture doc'))
    $missing = [System.Collections.Generic.List[string]]::new()
    $checked = 0

    foreach ($project in Get-TestProject) {
        $checked++
        if (-not (Test-BacktickedName -Text $almanac -Name $project.Name)) {
            $missing.Add((Format-Miss -What 'test project' -Name $project.Name -Doc $AlmanacName))
        }
    }
    foreach ($class in Get-TestClassName -Directory (Join-Path $Root 'tests')) {
        $checked++
        if (-not $almanac.Contains("$Backtick$class$Backtick")) {
            $missing.Add((Format-Miss -What 'test class' -Name $class -Doc $AlmanacName))
        }
    }
    foreach ($project in Get-SourceProject) {
        $checked++
        if (-not (Test-BacktickedName -Text $architecture -Name $project.Name)) {
            $missing.Add((Format-Miss -What 'project' -Name $project.Name -Doc $ArchitectureName))
        }
    }
    [pscustomobject]@{ Checked = $checked; Missing = $missing }
}

function Get-StagedPath {
    $staged = @(git -C $Root diff --cached --name-only)
    if ($LASTEXITCODE -ne 0) { throw 'git diff --cached --name-only failed' }
    , $staged
}

function Get-CommitMessageMiss {
    param([string]$Path, [string[]]$Staged)
    $touched = @($Staged | Where-Object { $_.StartsWith($ContentPrefix) })
    if ($touched.Count -eq 0) { return @() }
    if (@($Staged | Where-Object { $_.StartsWith($ContentDocPrefix) }).Count -gt 0) { return @() }
    $file = Resolve-InputPath -Path $Path -What 'Commit message'
    foreach ($line in [System.IO.File]::ReadAllLines($file)) {
        if ([regex]::IsMatch($line, '^Docs: unchanged, \S+')) { return @() }
    }
    @($ContentMessage)
}

function Get-ProjectCount {
    $rows = @()
    foreach ($project in Get-TestProject) {
        $classes = [System.Collections.Generic.HashSet[string]]::new()
        $facts = 0
        $theories = 0
        foreach ($file in Get-SourceFile -Directory $project.FullName) {
            $text = [System.IO.File]::ReadAllText($file.FullName)
            foreach ($match in [regex]::Matches($text, $ClassPattern)) { [void]$classes.Add($match.Groups[3].Value) }
            $facts += ([regex]::Matches($text, '\[Fact\b')).Count
            $theories += ([regex]::Matches($text, '\[Theory\b')).Count
        }
        $rows += [pscustomobject]@{ Project = $project.Name; Classes = $classes.Count; Facts = $facts; Theories = $theories }
    }
    $rows
}

function Update-CountTable {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Path)
    $raw = [System.IO.File]::ReadAllText($Path)
    $newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = @($raw -split '\r?\n')

    $header = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^\|\s*Project\s*\|\s*Classes\s*\|') {
            $header = $index
            break
        }
    }
    if ($header -lt 0) { throw "No counts table (a line starting with '| Project | Classes |') in $Path" }
    if ($header + 1 -ge $lines.Count) { throw "The counts table in $Path has no separator line" }

    $stop = $header + 2
    while ($stop -lt $lines.Count -and $lines[$stop].Trim() -ne '') { $stop++ }

    $rows = @(Get-ProjectCount | ForEach-Object {
            "| $($_.Project) | $($_.Classes) | $($_.Facts) | $($_.Theories) | $($_.Facts + $_.Theories) |"
        })
    $rebuilt = @($lines[0..($header + 1)]) + $rows
    if ($stop -lt $lines.Count) { $rebuilt += $lines[$stop..($lines.Count - 1)] }

    if ($PSCmdlet.ShouldProcess($Path, 'Rewrite the counts table')) {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        [System.IO.File]::WriteAllText($Path, ($rebuilt -join $newline), [System.Text.UTF8Encoding]::new($bom))
    }
    Write-Output "doc-drift: counts updated ($($rows.Count) projects)"
}

$Root = Resolve-InputPath -Path $Root -What 'Repository root'

if ($Update) {
    Update-CountTable -Path (Resolve-InputPath -Path (Join-Path $Root $AlmanacName) -What 'Test almanac')
    exit 0
}

if ($CommitMessage -ne '') {
    $staged = @(if ($PSBoundParameters.ContainsKey('StagedPaths')) { $StagedPaths } else { Get-StagedPath })
    $message = @(Get-CommitMessageMiss -Path $CommitMessage -Staged $staged)
    if ($message.Count -gt 0) {
        foreach ($miss in $message) { Write-Output $miss }
        exit 1
    }
    exit 0
}

$drift = Get-StructureMiss
if ($drift.Missing.Count -gt 0) {
    foreach ($miss in $drift.Missing) { Write-Output $miss }
    Write-Output "doc-drift: $($drift.Missing.Count) names missing; name them in the doc, or run tools/hooks/Test-DocDrift.ps1 -Update for the counts"
    exit 1
}

Write-Output "doc-drift: $($drift.Checked) names checked, no drift"
exit 0
