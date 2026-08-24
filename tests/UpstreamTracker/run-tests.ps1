[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = (Resolve-Path (Join-Path $testRoot '..\..')).Path
$toolRoot = Join-Path $repositoryRoot 'tools\upstream-tracker'
$candidates = @()
$py = Get-Command py.exe -ErrorAction SilentlyContinue
if ($null -ne $py) {
    $candidates += [pscustomobject]@{ Executable = $py.Source; Prefix = @('-3.12') }
}

$python = Get-Command python.exe -ErrorAction SilentlyContinue
if ($null -ne $python) {
    $candidates += [pscustomobject]@{ Executable = $python.Source; Prefix = @() }
}

$selected = $null
foreach ($candidate in $candidates) {
    $version = & $candidate.Executable @($candidate.Prefix) -c 'import sys; print(sys.version_info.major, sys.version_info.minor, sep=chr(46))' 2>$null
    if ($LASTEXITCODE -eq 0 -and $version -eq '3.12') {
        $selected = $candidate
        break
    }
}

if ($null -eq $selected) {
    throw 'Python 3.12 is required. Run setup.cmd or install Python 3.12.'
}

$env:PYTHONDONTWRITEBYTECODE = '1'
$env:PYTHONHASHSEED = '0'
$env:PYTHONPATH = $toolRoot
& $selected.Executable @($selected.Prefix) -m unittest discover -s $testRoot -p 'test_*.py' -v
exit $LASTEXITCODE
