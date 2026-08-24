[CmdletBinding()]
param(
    [ValidateSet("All", "Rhino8", "Rhino7")]
    [Alias("Host")]
    [string]$Target = "All",

    [string]$Rhino8Exe = "C:\Program Files\Rhino 8\System\Rhino.exe",

    [string]$Rhino7Exe = "C:\Program Files\Rhino 7\System\Rhino.exe",

    [ValidateRange(15, 600)]
    [int]$TimeoutSeconds = 60,

    [switch]$SkipPluginBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $toolRoot "..\.."))
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
$runRoot = Join-Path $repoRoot "temp\grasshopper-smoke\run-$runStamp"
[IO.Directory]::CreateDirectory($runRoot) | Out-Null

function Resolve-DotNet {
    $local = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $local -PathType Leaf) {
        return $local
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "dotnet was not found. Run setup.cmd before the Grasshopper host gate."
    }

    return $command.Source
}

function Require-File([string]$Path, [string]$Label) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "$Label was not found: $full"
    }

    return $full
}

function Invoke-DotNetLogged([string[]]$Arguments, [string]$LogName) {
    $logPath = Join-Path $runRoot $LogName
    & $script:dotnet @Arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE. See $logPath"
    }
}

function ConvertTo-ProcessArguments([string[]]$Arguments) {
    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + $_.Replace('"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Invoke-BoundedHost(
    [string]$FilePath,
    [string[]]$Arguments,
    [string]$OutputDirectory,
    [hashtable]$EnvironmentVariables) {
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    $stdoutPath = Join-Path $OutputDirectory "stdout.log"
    $stderrPath = Join-Path $OutputDirectory "stderr.log"

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = ConvertTo-ProcessArguments $Arguments
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
        $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Host process did not start: $FilePath"
    }

    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try {
            $process.Kill()
            $process.WaitForExit()
        }
        finally {
            [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result)
            [IO.File]::WriteAllText($stderrPath, $stderrTask.Result)
            $process.Dispose()
        }

        throw "Host process exceeded the $TimeoutSeconds-second limit and was terminated. See $OutputDirectory"
    }

    $process.WaitForExit()
    $exitCode = $process.ExitCode
    [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result)
    [IO.File]::WriteAllText($stderrPath, $stderrTask.Result)
    $process.Dispose()

    Get-Content -LiteralPath $stdoutPath
    if ($exitCode -ne 0) {
        Get-Content -LiteralPath $stderrPath
        throw "Host process failed with exit code $exitCode. See $OutputDirectory"
    }
}

function Build-Plugin([string]$Project, [string]$Framework, [string]$Label) {
    if ($SkipPluginBuild) {
        return
    }

    Invoke-DotNetLogged @(
        "build", $Project,
        "--configuration", "Release",
        "--framework", $Framework,
        "--no-restore",
        "--nologo"
    ) "build-$Label.log"
}

try {
    $script:dotnet = Resolve-DotNet
    $invisibleProject = Join-Path $repoRoot "src\InvisibleDragon\GonieGonie.InvisibleDragon.GH\GonieGonie.InvisibleDragon.GH.csproj"
    $simpleProject = Join-Path $repoRoot "src\SimpleDragon\GonieGonie.SimpleDragon.GH\GonieGonie.SimpleDragon.GH.csproj"

    if ($Target -in @("All", "Rhino8")) {
        $rhino8Exe = Require-File $Rhino8Exe "Rhino 8 executable"
        Build-Plugin $invisibleProject "net8.0-windows" "invisible-rhino8"
        Build-Plugin $simpleProject "net8.0-windows" "simple-rhino8"

        $rhino8Project = Join-Path $toolRoot "Rhino8\GonieGonie.Dragons.Grasshopper.Rhino8Smoke.csproj"
        Invoke-DotNetLogged @("restore", $rhino8Project, "--locked-mode", "--nologo") "restore-rhino8-host.log"
        Invoke-DotNetLogged @("build", $rhino8Project, "--configuration", "Release", "--no-restore", "--nologo") "build-rhino8-host.log"

        $rhino8Runner = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.Dragons.Grasshopper.Rhino8Smoke\Release\net8.0-windows\GonieGonie.Dragons.Grasshopper.Rhino8Smoke.dll"
        ) "Rhino 8 host runner"
        $invisible8 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.InvisibleDragon.GH\Release\net8.0-windows\GonieGonie.InvisibleDragon.GH.gha"
        ) "InvisibleDragon Rhino 8 GHA"
        $simple8 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.SimpleDragon.GH\Release\net8.0-windows\GonieGonie.SimpleDragon.GH.gha"
        ) "SimpleDragon Rhino 8 GHA"
        $rhino8Output = Join-Path $runRoot "rhino8"
        Invoke-BoundedHost $script:dotnet @($rhino8Runner) $rhino8Output @{
            DRAGONS_RHINO8_EXE = $rhino8Exe
            DRAGONS_INVISIBLE_GHA = $invisible8
            DRAGONS_SIMPLE_GHA = $simple8
            DRAGONS_GRASSHOPPER_SMOKE_OUTPUT = $rhino8Output
        }
        Require-File (Join-Path $rhino8Output "summary.json") "Rhino 8 host summary" | Out-Null
    }

    if ($Target -in @("All", "Rhino7")) {
        $rhino7Exe = Require-File $Rhino7Exe "Rhino 7 executable"
        $rhino7System = Split-Path -Parent $rhino7Exe
        $rhino7Grasshopper = [IO.Path]::GetFullPath((Join-Path $rhino7System "..\Plug-ins\Grasshopper"))
        Require-File (Join-Path $rhino7Grasshopper "Grasshopper.dll") "Rhino 7 Grasshopper" | Out-Null
        Build-Plugin $invisibleProject "net48" "invisible-rhino7"
        Build-Plugin $simpleProject "net48" "simple-rhino7"

        $rhino7Project = Join-Path $toolRoot "Rhino7Probe\Rhino7Probe.csproj"
        $rhino7Properties = @(
            "-p:Rhino7SystemDir=$rhino7System",
            "-p:Rhino7GrasshopperDir=$rhino7Grasshopper"
        )
        Invoke-DotNetLogged (@("restore", $rhino7Project, "--locked-mode", "--nologo") + $rhino7Properties) "restore-rhino7-host.log"
        Invoke-DotNetLogged (@("build", $rhino7Project, "--configuration", "Release", "--no-restore", "--nologo") + $rhino7Properties) "build-rhino7-host.log"

        $rhino7Runner = Require-File (
            Join-Path $repoRoot "temp\build\bin\Rhino7Probe\Release\net48\GonieGonie.Dragons.Grasshopper.Rhino7Probe.exe"
        ) "Rhino 7 host runner"
        $invisible7 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.InvisibleDragon.GH\Release\net48\GonieGonie.InvisibleDragon.GH.gha"
        ) "InvisibleDragon Rhino 7 GHA"
        $simple7 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.SimpleDragon.GH\Release\net48\GonieGonie.SimpleDragon.GH.gha"
        ) "SimpleDragon Rhino 7 GHA"
        $rhino7Output = Join-Path $runRoot "rhino7"
        $rhino7Document = Join-Path $rhino7Output "dragons-host-gate.gh"
        Invoke-BoundedHost $rhino7Runner @($invisible7, $simple7, $rhino7Document) $rhino7Output @{
            DRAGONS_RHINO7_EXE = $rhino7Exe
        }
        Require-File ($rhino7Document + ".summary.txt") "Rhino 7 host summary" | Out-Null
    }

    [IO.File]::WriteAllText(
        (Join-Path $runRoot "PASS.txt"),
        "Grasshopper host gate passed for $Target at $([DateTimeOffset]::UtcNow.ToString('O'))." + [Environment]::NewLine)
    Write-Host "Grasshopper host gate passed. Logs and documents: $runRoot"
}
catch {
    [IO.File]::WriteAllText((Join-Path $runRoot "FAIL.txt"), $_.Exception.ToString())
    Write-Error "Grasshopper host gate failed. Logs: $runRoot`n$($_.Exception.Message)"
    exit 1
}
