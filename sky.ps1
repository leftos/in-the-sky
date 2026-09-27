#Requires -Version 7.0
<#
.SYNOPSIS
    Sky dev script: one entry point for the build, the tests, the formatters, the hooks and clean.

.DESCRIPTION
    Subcommands (`.\sky.ps1 help` prints the same list at runtime):

      build      dotnet build InTheSky.slnx with warnings as errors. -Release for Release.
      test       dotnet test InTheSky.slnx. -Project P runs one test project, P in Engine, Content, Scripting, Session,
                 Sim, SimConnect, Voice, Client; -Filter "*X" one class (a wildcard on the full class name). The rest
                 forwards.
      format     dotnet csharpier format ., then dotnet format style and dotnet format analyzers at --severity info.
                 -Check only verifies.
      analysis   ruff format, ruff check, ty and pytest over every project under tools/ that carries a pyproject.toml,
                 through uv. -Check only verifies.
      provenance uv run python -m provenance credits, then check: regenerates CREDITS.md from the ledger and
                 gates every asset on it. -Check only checks.
      hooks      prek run --all-files (anything after `hooks` forwards to prek instead).
      client     The Godot client from a fresh clone: dotnet build of the client (Debug, -warnaserror), then
                 --import --quit (up to three passes while a pass ends red on the pre-import lines alone), then
                 --build-solutions --quit.
      play       Builds the client (Debug; the whole client order on a tree Godot has never imported), then opens the
                 game in a window without waiting for it.
      clean      dotnet clean InTheSky.slnx.
      help       This list.

    MSBuild switches are written in dash form (-warnaserror, -p:Name=Value) rather than with a slash: Git Bash's path
    conversion rewrites /warnaserror into a Windows path and MSBuild then reports MSB1008, while the dash form reads the
    same from every shell.

    Every gate - build, test, the formatters - runs through tools/gate.ps1 under a ceiling of its own, writing the whole
    output to a log under .tmp and printing its tail; one that reaches its ceiling is killed with its children and exits
    124.

    Godot is $env:GODOT_PATH when it is set, F:\Godot\Godot_console.exe otherwise.

.EXAMPLE
    .\sky.ps1 build -Release
    .\sky.ps1 test -Project Engine -Filter "*ReferenceTests"
    .\sky.ps1 format -Check
    .\sky.ps1 provenance -Check
    .\sky.ps1 hooks
    .\sky.ps1 client
    .\sky.ps1 play
#>
[CmdletBinding()]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '',
    Justification = 'Interactive dev script; coloured status to the console is the UX.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '',
    Justification = 'Top-level params are read from script scope by the subcommands.')]
param(
    [Parameter(Position = 0)]
    [ValidateSet('', 'help', 'build', 'test', 'format', 'analysis', 'provenance', 'hooks', 'client', 'play', 'clean')]
    [string]$Command = '',

    # Everything after the subcommand. Each subcommand takes the options it knows and forwards the rest.
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Root = $PSScriptRoot
$script:Tmp = Join-Path $script:Root '.tmp'
# Every build, test and format run this script makes on the caller's behalf goes through tools/gate.ps1: the whole
# output to a log, the tail on the screen, the command's own exit status, and 124 when the ceiling below killed it and
# its children. A ceiling is a few times what the command takes today, so a run that reaches one has hung rather than
# slowed - read its log, do not raise it.
$script:GateScript = Join-Path $script:Root 'tools\gate.ps1'
$script:BuildSeconds = 300
$script:FormatSeconds = 180
$script:TestSeconds = 180
$script:FilteredTestSeconds = 60
$script:AnalysisSeconds = 120
$script:ProvenanceSeconds = 60
$script:ImportSeconds = 300
$script:BuildSolutionsSeconds = 180

$script:GodotConsole = if ($env:GODOT_PATH) { $env:GODOT_PATH } else { 'F:\Godot\Godot_console.exe' }
$script:ClientProject = 'src/Sky.Client'
# The test projects -Project accepts, each one tests/Sky.<P>.Tests.
$script:TestProjects = 'Engine', 'Content', 'Scripting', 'Session', 'Sim', 'SimConnect', 'Voice', 'Client'
# How many times a cold import may run. Two is not enough: a second pass has ended on the same pre-import lines as the
# first and the third was clean, so the import gets three tries at a cache it is still filling.
$script:ImportPasses = 3
# What a cold import complains about before it has imported anything: the imported data it cannot open yet and art whose
# .import file the pass is only now writing.
$script:ImportMarkers = @(
    '.godot/imported/',
    'No loader found for resource: res://Art/',
    'Failed loading resource: res://Art/'
)

Set-Location $script:Root
New-Item -ItemType Directory -Force $script:Tmp | Out-Null

# --- shared helpers ---

function Write-Section {
    param([string]$Title)
    Write-Host ''
    Write-Host "== $Title ==" -ForegroundColor Cyan
}

# Takes the options a subcommand knows out of $Rest: `-Name value` for names in $Values, bare `-Name` for names in
# $Switches. Every other word is left in order, so it can be forwarded untouched or read as a positional.
function Split-Rest {
    param([string[]]$Values = @(), [string[]]$Switches = @())
    $options = @{}
    $left = [System.Collections.Generic.List[string]]::new()
    $words = @($script:Rest)
    $i = 0
    while ($i -lt $words.Count) {
        $word = $words[$i]
        $name = if ($word -match '^-([A-Za-z][A-Za-z0-9]*)$') { $Matches[1] } else { $null }
        if ($name -and $name -in $Switches) {
            $options[$name] = $true
            $i += 1
        }
        elseif ($name -and $name -in $Values -and $i + 1 -lt $words.Count) {
            $options[$name] = $words[$i + 1]
            $i += 2
        }
        else {
            $left.Add($word)
            $i += 1
        }
    }
    return @{ Options = $options; Rest = $left.ToArray() }
}

# Runs one gate under tools/gate.ps1 and stops the script when it fails, so a chain of steps ends at the first red one.
# The command is handed over as an array rather than as words on this line: PowerShell's parser eats a bare -- as its
# own end-of-parameters marker, and `dotnet test ... -- --timeout 2m` needs the separator to reach the runner.
function Invoke-Gate {
    param([string]$Title, [string]$Log, [int]$Seconds, [string[]]$Gate, [string]$Hint)
    Write-Section $Title
    & $script:GateScript -Log $Log -TimeoutSeconds $Seconds -- @Gate
    if ($LASTEXITCODE -eq 124) {
        $message = "$Title reached its ceiling of $Seconds s and was killed with its children; read $Log rather than raise it."
        # A gate whose stall has a known first thing to try says so here, where the ceiling is reported.
        if ($Hint) { $message = "$message $Hint" }
        throw $message
    }
    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed (exit $LASTEXITCODE); the whole output is in $Log"
    }
}

# --- help ---

function Invoke-Help {
    Write-Host @'
Sky dev script

Usage: .\sky.ps1 <subcommand> [args]

Build and check
  build [-Release]                    dotnet build InTheSky.slnx -warnaserror; -Release builds Release
  test [-Project P] [-Filter "*X"]    dotnet test; P in Engine, Content, Scripting, Session, Sim, SimConnect,
                                      Voice, Client; the rest forwards
  format [-Check]                     csharpier, then dotnet format style and analyzers (--severity info);
                                      -Check verifies only and writes nothing
  analysis [-Check]                   ruff format, ruff check, ty and pytest over every project under tools/ with a
                                      pyproject.toml (uv); -Check changes nothing; with none present it prints
                                      `analysis: no uv projects` and passes
  provenance [-Check]                 uv run python -m provenance credits, then check: regenerates CREDITS.md
                                      from the ledger and gates every asset on it; -Check only checks
  hooks [prek args]                   prek run --all-files
  clean                               dotnet clean InTheSky.slnx
  help                                This list

Godot client (Godot from $env:GODOT_PATH or F:\Godot\Godot_console.exe)
  client                              dotnet build of the client, Godot --import --quit (up to three passes
                                      on a cold cache), then --build-solutions --quit; the fresh-clone order
  play                                Builds the client (client in full on a fresh tree), then opens the game
                                      in a window

MSBuild switches are written in dash form (-warnaserror, -p:Name=Value): Git Bash rewrites a /switch into a Windows
path, and MSBuild then reports MSB1008.

Every gate - build, test, the formatters - runs through tools/gate.ps1 under a ceiling of its own, writing the whole
output to a log under .tmp and printing its tail; one that reaches its ceiling is killed with its children and exits
124.
'@
}

# --- build / test / format / hooks / clean ---

function Invoke-Build {
    $options = (Split-Rest -Switches 'Release').Options
    $config = if ($options['Release']) { 'Release' } else { 'Debug' }
    Invoke-Gate -Title "dotnet build InTheSky.slnx -c $config -warnaserror" `
        -Log (Join-Path $script:Tmp 'build.log') -Seconds $script:BuildSeconds `
        -Gate @('dotnet', 'build', 'InTheSky.slnx', '-c', $config, '-warnaserror')
}

function Invoke-Test {
    $parsed = Split-Rest -Values 'Project', 'Filter'
    $project = $parsed.Options['Project']
    if ($project -and $project -notin $script:TestProjects) {
        throw "test -Project takes one of $($script:TestProjects -join ', '), not '$project'"
    }
    $log = Join-Path $script:Tmp 'test.log'
    $target = if ($project) { @('--project', (Join-Path $script:Root "tests\Sky.$project.Tests")) } else { @('InTheSky.slnx') }
    $filter = if ($parsed.Options['Filter']) { @('--filter-class', $parsed.Options['Filter']) } else { @() }
    $forwarded = @($parsed.Rest)
    # The runner's own guard beside the ceiling: --timeout is a global test execution timeout the platform forwards to
    # every test module, so a single test that never completes ends its module rather than holding the whole run. A
    # caller who forwarded a separator of their own has already said what they want after it.
    if ($forwarded -notcontains '--') { $forwarded += @('--', '--timeout', '2m') }
    $seconds = if ($parsed.Options['Filter']) { $script:FilteredTestSeconds } else { $script:TestSeconds }
    Invoke-Gate -Title "dotnet test $target $filter $forwarded" -Log $log -Seconds $seconds `
        -Gate (@('dotnet', 'test') + $target + $filter + $forwarded)
}

function Invoke-Format {
    $check = (Split-Rest -Switches 'Check').Options['Check']
    $csharpierLog = Join-Path $script:Tmp 'csharpier.log'
    $formatLog = Join-Path $script:Tmp 'format.log'
    $analyzersLog = Join-Path $script:Tmp 'analyzers.log'
    # style and analyzers are named rather than left to a bare `dotnet format`: its whitespace pass undoes what
    # csharpier just wrote, and the two then fight over every file.
    if ($check) {
        Invoke-Gate -Title 'dotnet csharpier check .' -Log $csharpierLog -Seconds $script:FormatSeconds `
            -Gate @('dotnet', 'csharpier', 'check', '.')
        Invoke-Gate -Title 'dotnet format style InTheSky.slnx --verify-no-changes --severity info' `
            -Log $formatLog -Seconds $script:FormatSeconds `
            -Gate @('dotnet', 'format', 'style', 'InTheSky.slnx', '--verify-no-changes', '--severity', 'info')
        Invoke-Gate -Title 'dotnet format analyzers InTheSky.slnx --verify-no-changes --severity info' `
            -Log $analyzersLog -Seconds $script:FormatSeconds `
            -Gate @('dotnet', 'format', 'analyzers', 'InTheSky.slnx', '--verify-no-changes', '--severity', 'info')
        return
    }
    Invoke-Gate -Title 'dotnet csharpier format .' -Log $csharpierLog -Seconds $script:FormatSeconds `
        -Gate @('dotnet', 'csharpier', 'format', '.')
    Invoke-Gate -Title 'dotnet format style InTheSky.slnx --severity info' -Log $formatLog -Seconds $script:FormatSeconds `
        -Gate @('dotnet', 'format', 'style', 'InTheSky.slnx', '--severity', 'info')
    Invoke-Gate -Title 'dotnet format analyzers InTheSky.slnx --severity info' -Log $analyzersLog `
        -Seconds $script:FormatSeconds `
        -Gate @('dotnet', 'format', 'analyzers', 'InTheSky.slnx', '--severity', 'info')
}

# Every project under tools/ that carries a pyproject.toml, as repo-relative paths. Read at run time, so a project
# landing under tools/ later is checked without an edit here.
function Get-UvProject {
    [OutputType([string[]])]
    param()
    return @(
        Get-ChildItem -Path (Join-Path $script:Root 'tools/*/pyproject.toml') -File -ErrorAction SilentlyContinue |
            ForEach-Object { "tools/$($_.Directory.Name)" }
    )
}

# uv is given --directory rather than --project: --project picks the project uv resolves but leaves the working
# directory where the caller stands, and ruff, ty and pytest each read their configuration from the directory they run
# in or above it (ty's --project defaults to the working directory and walks up from there, ty docs reference/cli.md).
# From the repo root, which holds no pyproject.toml, a project's pyproject.toml would then never be read. Each project
# gets its own logs.
function Invoke-Analysis {
    $check = (Split-Rest -Switches 'Check').Options['Check']
    $format = if ($check) { @('ruff', 'format', '--check', '.') } else { @('ruff', 'format', '.') }
    # Without -Check the lint pass fixes what it can, which is why it runs after the formatter and before the types.
    $lint = if ($check) { @('ruff', 'check', '.') } else { @('ruff', 'check', '.', '--fix') }
    $projects = @(Get-UvProject)
    if ($projects.Count -eq 0) {
        Write-Host 'analysis: no uv projects'
        exit 0
    }
    foreach ($project in $projects) {
        $uv = @('uv', 'run', '--directory', $project)
        $name = $project -replace '^tools/', ''
        Invoke-Gate -Title "uv run --directory $project $($format -join ' ')" `
            -Log (Join-Path $script:Tmp "$name-format.log") -Seconds $script:AnalysisSeconds -Gate ($uv + $format)
        Invoke-Gate -Title "uv run --directory $project $($lint -join ' ')" `
            -Log (Join-Path $script:Tmp "$name-lint.log") -Seconds $script:AnalysisSeconds -Gate ($uv + $lint)
        Invoke-Gate -Title "uv run ty check $project" -Log (Join-Path $script:Tmp "$name-types.log") `
            -Seconds $script:AnalysisSeconds -Gate ($uv + @('ty', 'check', '.'))
        Invoke-Gate -Title "uv run pytest $project -q" -Log (Join-Path $script:Tmp "$name-tests.log") `
            -Seconds $script:AnalysisSeconds -Gate ($uv + @('pytest', '.', '-q'))
    }
}

# The asset ledger gate. Without -Check, `credits` writes CREDITS.md from the ledger and `check` then gates every asset
# git carries on it; -Check runs the gate alone and writes nothing. uv is given --project rather than analysis's
# --directory: the checker finds the repository root from the working directory, so it must run from the repo root.
function Invoke-Provenance {
    $check = (Split-Rest -Switches 'Check').Options['Check']
    $uv = @('uv', 'run', '--project', 'tools/provenance', 'python', '-m', 'provenance')
    if (-not $check) {
        Invoke-Gate -Title 'uv run --project tools/provenance python -m provenance credits' `
            -Log (Join-Path $script:Tmp 'provenance-credits.log') -Seconds $script:ProvenanceSeconds `
            -Gate ($uv + @('credits'))
    }
    Invoke-Gate -Title 'uv run --project tools/provenance python -m provenance check' `
        -Log (Join-Path $script:Tmp 'provenance-check.log') -Seconds $script:ProvenanceSeconds `
        -Gate ($uv + @('check'))
}

function Invoke-Hook {
    # The cast keeps one word an array: an if expression emits to the pipeline, which unrolls a one-element array into
    # a bare string, and prek then reads its characters as hook selectors one by one.
    [string[]]$words = if ($script:Rest.Count -gt 0) { $script:Rest } else { '--all-files' }
    Write-Section "prek run $($words -join ' ')"
    & prek run @words
    exit $LASTEXITCODE
}

# --- client / play ---

# Whether a red import failed at nothing but the lines $script:ImportMarkers names, the block a cold cache always
# prints. A log holding no ERROR: line at all passes the test, since nothing in it goes unexplained.
function Test-ImportRedOnMarker {
    [OutputType([bool])]
    param([string]$Log)
    if (-not (Test-Path -LiteralPath $Log)) {
        return $false
    }
    foreach ($line in @(Get-Content -LiteralPath $Log)) {
        $text = ([string]$line).Trim()
        if (-not $text.StartsWith('ERROR:')) {
            continue
        }
        $explained = $false
        foreach ($marker in $script:ImportMarkers) {
            if ($text.Contains($marker)) {
                $explained = $true
                break
            }
        }
        if (-not $explained) {
            return $false
        }
    }
    return $true
}

# The client's Debug build on its own, so a run that follows opens with a compiling assembly: a Godot import or
# solutions pass against source that no longer compiles reports on the last good one instead.
function Invoke-ClientBuild {
    Invoke-Gate -Title 'dotnet build src/Sky.Client -c Debug -warnaserror' `
        -Log (Join-Path $script:Tmp 'client-dotnet-build.log') -Seconds $script:BuildSeconds `
        -Gate @('dotnet', 'build', "$($script:ClientProject)/Sky.Client.csproj", '-c', 'Debug', '-warnaserror')
}

# The fresh-clone order: --import before --build-solutions, because with nothing under .godot/imported the editor cannot
# load the project and the solutions pass can die before it builds. The first import can end red on the pre-import lines
# alone, so a pass that ends red on those lines alone is run again, up to three passes; a ceiling or any other red stops
# here. The client is built first: until the assembly exists the importer reports the C# scripts as not compiling.
function Invoke-Client {
    Invoke-ClientBuild
    $import = @($script:GodotConsole, '--headless', '--path', $script:ClientProject, '--import', '--quit')
    for ($pass = 1; $pass -le $script:ImportPasses; $pass++) {
        $name = if ($pass -eq 1) { 'client-import' } else { "client-import-$pass" }
        $log = Join-Path $script:Tmp "$name.log"
        Write-Section "Godot --import --quit (pass $pass of $($script:ImportPasses))"
        & $script:GateScript -Log $log -TimeoutSeconds $script:ImportSeconds -- @import
        $imported = $LASTEXITCODE
        if ($imported -eq 0) {
            break
        }
        if ($imported -eq 124) {
            throw "Godot --import reached its ceiling of $($script:ImportSeconds) s and was killed; read $log rather than raise it."
        }
        if (-not (Test-ImportRedOnMarker -Log $log)) {
            throw "Godot --import failed (exit $imported) on lines the pre-import markers do not explain; read $log"
        }
        if ($pass -eq $script:ImportPasses) {
            throw "Godot --import still failed after $($script:ImportPasses) passes (exit $imported); read $log"
        }
        Write-Host "the import ended red on the pre-import lines alone; running pass $($pass + 1)" -ForegroundColor Yellow
    }

    # Godot_console.exe is a job-object wrapper that returns only when every process in its job has exited, and a build
    # Godot runs on a tree with no live build servers births MSBuild's worker nodes and the compiler server inside that
    # job, where they idle for up to fifteen minutes after Godot itself has quit. With node reuse and shared compilation
    # off for the launch, the job empties when Godot does.
    $nodeReuse = $env:MSBUILDDISABLENODEREUSE
    $shared = $env:UseSharedCompilation
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:UseSharedCompilation = 'false'
    try {
        Invoke-Gate -Title 'Godot --build-solutions --quit' -Log (Join-Path $script:Tmp 'client-build.log') `
            -Seconds $script:BuildSolutionsSeconds `
            -Gate @($script:GodotConsole, '--headless', '--path', $script:ClientProject, '--build-solutions', '--quit')
    }
    finally {
        $env:MSBUILDDISABLENODEREUSE = $nodeReuse
        $env:UseSharedCompilation = $shared
    }
}

# The game in a window, for playing it by hand: the client's Debug build (the full fresh-clone order on a tree Godot has
# never imported), then Godot launched on the project's main scene without waiting for it. Started directly rather than
# through the godot MCP server, so it is an ordinary focused window with sound and real input, and no bridge.
function Invoke-Play {
    if ($script:Rest.Count -gt 0) {
        throw "play takes no arguments, not: $($script:Rest -join ' ')"
    }
    if (Test-Path -LiteralPath (Join-Path $script:ClientProject '.godot\imported')) {
        Invoke-ClientBuild
    }
    else {
        Invoke-Client
    }
    Write-Section 'Godot (windowed)'
    Start-Process -FilePath $script:GodotConsole -ArgumentList '--path', (Join-Path $script:Root $script:ClientProject)
    Write-Host 'launched; the game opens in its own window'
}

function Invoke-Clean {
    Write-Section 'dotnet clean InTheSky.slnx'
    dotnet clean InTheSky.slnx
    exit $LASTEXITCODE
}

# --- dispatch ---

switch ($Command) {
    '' { Invoke-Help }
    'help' { Invoke-Help }
    'build' { Invoke-Build }
    'test' { Invoke-Test }
    'format' { Invoke-Format }
    'analysis' { Invoke-Analysis }
    'provenance' { Invoke-Provenance }
    'hooks' { Invoke-Hook }
    'client' { Invoke-Client }
    'play' { Invoke-Play }
    'clean' { Invoke-Clean }
}
