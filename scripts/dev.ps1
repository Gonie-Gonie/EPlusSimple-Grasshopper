#requires -Version 5.1

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$commands = [ordered]@{
    setup = Join-Path $PSScriptRoot 'setup.ps1'
    build = Join-Path $PSScriptRoot 'build.ps1'
    reference = Join-Path $PSScriptRoot 'reference.ps1'
    package = Join-Path $PSScriptRoot 'package.ps1'
    install = Join-Path $PSScriptRoot 'install.ps1'
    examples = Join-Path $repositoryRoot 'tools\example-definitions\run.ps1'
    smoke = Join-Path $repositoryRoot 'tools\grasshopper-smoke\run.ps1'
    release = Join-Path $PSScriptRoot 'release.ps1'
    clean = Join-Path $PSScriptRoot 'clean.ps1'
    icons = Join-Path $PSScriptRoot 'generate-icons.ps1'
    compatibility = Join-Path $PSScriptRoot 'compatibility.ps1'
    upstream = Join-Path $repositoryRoot 'tools\upstream-tracker\run.ps1'
}

function Show-Help {
    Write-Host @'
Dragon development commands

Usage:
  dev.cmd <command> [arguments]

Commands:
  setup       Prepare SDK/Python/Rhino detection and verified embedded archives
  build       Build both Dragon products and run the test gates
  reference   Generate or verify the historical Python compatibility oracle
  package     Create portable ZIP and Yak packages
  install     Replace local Rhino 7/8 Dragon packages with the current build
  examples    Generate or validate tracked Grasshopper and Rhino examples
  smoke       Run Grasshopper host loading scenarios
  release     Create a fully verified local release candidate
  clean       Remove disposable temp and generated artifact content
  icons       Regenerate component and package icons
  compatibility  Run paired Python/C# IDF, EnergyPlus, warning, and GRR parity
  upstream    Validate, hash, or compare the pinned upstream source
  help        Show this command list

Examples:
  dev.cmd setup
  dev.cmd build -NoRestore
  dev.cmd examples -Generate
  dev.cmd install -UseExistingPackages
  dev.cmd compatibility -AllowDifferences
  dev.cmd clean -TempOnly
'@
}

if ($args.Count -eq 0) {
    Show-Help
    exit 0
}

$command = ([string] $args[0]).Trim().ToLowerInvariant()
if ($command -in @('help', '-h', '--help', '/?')) {
    Show-Help
    exit 0
}

if (-not $commands.Contains($command)) {
    [Console]::Error.WriteLine(
        "Unknown dev command '$($args[0])'. Run 'dev.cmd help' for the available commands.")
    exit 2
}

$target = [string] $commands[$command]
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    [Console]::Error.WriteLine("The '$command' implementation is missing: '$target'.")
    exit 3
}

[string[]] $forwardedArguments = @()
if ($args.Count -gt 1) {
    $forwardedArguments = @($args[1..($args.Count - 1)] | ForEach-Object { [string] $_ })
}

# Start a child Windows PowerShell process so forwarded strings such as
# -NoRestore and -Target bind as named parameters in the selected script.
$powerShell = Join-Path $PSHOME 'powershell.exe'
& $powerShell `
    -NoLogo `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File $target `
    @forwardedArguments
exit $LASTEXITCODE
