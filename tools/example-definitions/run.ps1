[CmdletBinding()]
param(
    [ValidateSet("All", "Rhino8", "Rhino7")]
    [Alias("Host")]
    [string]$Target = "All",

    [switch]$Generate,

    [string]$Rhino8Exe = "C:\Program Files\Rhino 8\System\Rhino.exe",

    [string]$Rhino7Exe = "C:\Program Files\Rhino 7\System\Rhino.exe",

    [ValidateRange(15, 600)]
    [int]$TimeoutSeconds = 90,

    [switch]$SkipPluginBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $toolRoot "..\.."))
$examplesRoot = Join-Path $repoRoot "examples"
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
$runRoot = Join-Path $repoRoot "temp\example-definitions\run-$runStamp"
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$hostWorkingDirectory = [IO.Path]::GetFullPath((
    Join-Path $systemTempRoot "GonieGonie-Dragons-example-host-$runStamp"))
if (-not $hostWorkingDirectory.StartsWith($systemTempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Host working directory escaped the system temp root: $hostWorkingDirectory"
}
[IO.Directory]::CreateDirectory($hostWorkingDirectory) | Out-Null

function Resolve-DotNet {
    $local = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $local -PathType Leaf) {
        return $local
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "dotnet was not found. Run 'dev.cmd setup' before the example-definition gate."
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
    $startInfo.WorkingDirectory = $hostWorkingDirectory
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

function Build-Rhino7Host([string]$RhinoSystem, [string]$GrasshopperDirectory) {
    $project = Join-Path $toolRoot "Rhino7\GonieGonie.Dragons.ExampleDefinitions.Rhino7.csproj"
    $properties = @(
        "-p:Rhino7SystemDir=$RhinoSystem",
        "-p:Rhino7GrasshopperDir=$GrasshopperDirectory"
    )
    Invoke-DotNetLogged (@("restore", $project, "--locked-mode", "--nologo") + $properties) "restore-rhino7-host.log"
    Invoke-DotNetLogged (@("build", $project, "--configuration", "Release", "--no-restore", "--nologo") + $properties) "build-rhino7-host.log"
}

function Build-Rhino8Host {
    $project = Join-Path $toolRoot "Rhino8\GonieGonie.Dragons.ExampleDefinitions.Rhino8.csproj"
    Invoke-DotNetLogged @("restore", $project, "--locked-mode", "--nologo") "restore-rhino8-host.log"
    Invoke-DotNetLogged @("build", $project, "--configuration", "Release", "--no-restore", "--nologo") "build-rhino8-host.log"
}

function Invoke-ExampleHost(
    [string]$HostName,
    [string]$Action,
    [string]$Runner,
    [string[]]$Arguments,
    [string]$InvisibleGha,
    [string]$SimpleGha,
    [string]$RhinoExecutable) {
    $output = Join-Path $runRoot ($Action.ToLowerInvariant() + "-" + $HostName.ToLowerInvariant())
    $environment = @{
        DRAGONS_EXAMPLE_ACTION = $Action
        DRAGONS_INVISIBLE_GHA = $InvisibleGha
        DRAGONS_SIMPLE_GHA = $SimpleGha
        DRAGONS_EXAMPLES_ROOT = $examplesRoot
        DRAGONS_EXAMPLES_OUTPUT = $output
    }
    if ($HostName -eq "Rhino7") {
        $environment.DRAGONS_RHINO7_EXE = $RhinoExecutable
    }
    else {
        $environment.DRAGONS_RHINO8_EXE = $RhinoExecutable
    }

    Invoke-BoundedHost $Runner $Arguments $output $environment
    Require-File (Join-Path $output "summary.json") "$HostName $Action summary" | Out-Null
}

try {
    if ($Generate -and $Target -ne "All") {
        throw "-Generate requires -Target All so every Rhino 7-authored binary is immediately validated in Rhino 8."
    }

    $script:dotnet = Resolve-DotNet
    $invisibleProject = Join-Path $repoRoot "src\InvisibleDragon\GonieGonie.InvisibleDragon.GH\GonieGonie.InvisibleDragon.GH.csproj"
    $simpleProject = Join-Path $repoRoot "src\SimpleDragon\GonieGonie.SimpleDragon.GH\GonieGonie.SimpleDragon.GH.csproj"

    $needRhino7 = $Generate -or $Target -in @("All", "Rhino7")
    $needRhino8 = $Target -in @("All", "Rhino8")
    if ($needRhino7) {
        $rhino7Exe = Require-File $Rhino7Exe "Rhino 7 executable"
        $rhino7System = Split-Path -Parent $rhino7Exe
        $rhino7Grasshopper = [IO.Path]::GetFullPath((Join-Path $rhino7System "..\Plug-ins\Grasshopper"))
        Require-File (Join-Path $rhino7Grasshopper "Grasshopper.dll") "Rhino 7 Grasshopper" | Out-Null
        Build-Plugin $invisibleProject "net48" "invisible-rhino7"
        Build-Plugin $simpleProject "net48" "simple-rhino7"
        Build-Rhino7Host $rhino7System $rhino7Grasshopper
        $rhino7Runner = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.Dragons.ExampleDefinitions.Rhino7\Release\net48\GonieGonie.Dragons.ExampleDefinitions.Rhino7.exe"
        ) "Rhino 7 example host"
        $invisible7 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.InvisibleDragon.GH\Release\net48\GonieGonie.InvisibleDragon.GH.gha"
        ) "InvisibleDragon Rhino 7 GHA"
        $simple7 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.SimpleDragon.GH\Release\net48\GonieGonie.SimpleDragon.GH.gha"
        ) "SimpleDragon Rhino 7 GHA"
        if ($Generate) {
            Invoke-ExampleHost "Rhino7" "Generate" $rhino7Runner @() $invisible7 $simple7 $rhino7Exe
        }

        if ($Target -in @("All", "Rhino7")) {
            Invoke-ExampleHost "Rhino7" "Validate" $rhino7Runner @() $invisible7 $simple7 $rhino7Exe
        }
    }

    if ($needRhino8) {
        $rhino8Exe = Require-File $Rhino8Exe "Rhino 8 executable"
        Build-Plugin $invisibleProject "net8.0-windows" "invisible-rhino8"
        Build-Plugin $simpleProject "net8.0-windows" "simple-rhino8"
        Build-Rhino8Host
        $rhino8Runner = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.Dragons.ExampleDefinitions.Rhino8\Release\net8.0-windows\GonieGonie.Dragons.ExampleDefinitions.Rhino8.dll"
        ) "Rhino 8 example host"
        $invisible8 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.InvisibleDragon.GH\Release\net8.0-windows\GonieGonie.InvisibleDragon.GH.gha"
        ) "InvisibleDragon Rhino 8 GHA"
        $simple8 = Require-File (
            Join-Path $repoRoot "temp\build\bin\GonieGonie.SimpleDragon.GH\Release\net8.0-windows\GonieGonie.SimpleDragon.GH.gha"
        ) "SimpleDragon Rhino 8 GHA"
        Invoke-ExampleHost "Rhino8" "Validate" $script:dotnet @($rhino8Runner) $invisible8 $simple8 $rhino8Exe
    }

    [IO.File]::WriteAllText(
        (Join-Path $runRoot "PASS.txt"),
        "Grasshopper example-definition gate passed for $Target at $([DateTimeOffset]::UtcNow.ToString('O'))." + [Environment]::NewLine)
    Write-Host "Grasshopper example-definition gate passed. Logs: $runRoot"
}
catch {
    [IO.File]::WriteAllText((Join-Path $runRoot "FAIL.txt"), $_.Exception.ToString())
    Write-Error "Grasshopper example-definition gate failed. Logs: $runRoot`n$($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path -LiteralPath $hostWorkingDirectory -PathType Container) {
        Remove-Item -LiteralPath $hostWorkingDirectory -Recurse -Force
    }
}
