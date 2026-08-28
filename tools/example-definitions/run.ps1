[CmdletBinding()]
param(
    [ValidateSet("All", "Rhino8", "Rhino7")]
    [Alias("Host")]
    [string]$Target = "All",

    [switch]$Generate,

    [string]$Rhino8Exe = "C:\Program Files\Rhino 8\System\Rhino.exe",

    [string]$Rhino7Exe = "C:\Program Files\Rhino 7\System\Rhino.exe",

    [ValidateRange(30, 3600)]
    [int]$TimeoutSeconds = 600,

    [ValidateRange(15, 600)]
    [int]$WorkflowStageTimeoutSeconds = 180,

    [switch]$SkipPluginBuild,

    [string]$EnergyPlusRoot,

    [string]$WeatherPath,

    [switch]$SkipEnergyPlusWorkflow,

    [switch]$RequireEnergyPlusWorkflow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $toolRoot "..\.."))
$examplesRoot = Join-Path $repoRoot "examples"
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
$runToken = [Guid]::NewGuid().ToString("N").Substring(0, 8)
# Rhino 7 and EnergyPlus still exercise legacy MAX_PATH code paths. Keep the
# entire evidence tree in repository temp, but give simulation descendants a
# deliberately short absolute prefix.
$runRoot = Join-Path $repoRoot "temp\e\$runToken"
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

function Resolve-EnergyPlusWorkflow {
    if ($SkipEnergyPlusWorkflow) {
        return @{
            Status = "disabled"
            Reason = "EnergyPlus workflow execution was explicitly disabled."
            RuntimeRoot = ""
            WeatherPath = ""
        }
    }

    $runtimeRoot = $EnergyPlusRoot
    $configuredWeather = $WeatherPath
    $weatherArchivePath = Join-Path $repoRoot ".tools\distributions\weather\KoreanTMY-v1.zip"
    $expectedWeatherArchiveSize = 128349513L
    $expectedWeatherArchiveSha256 = "fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0"
    $settingsPath = Join-Path $repoRoot ".config\local.settings.json"
    if ([string]::IsNullOrWhiteSpace($runtimeRoot) -and (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
        try {
            $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            if ($null -ne $settings.energyPlus -and [string]$settings.energyPlus.status -eq "ready") {
                $runtimeRoot = [string]$settings.energyPlus.root
            }
        }
        catch {
            return @{
                Status = "unavailable"
                Reason = "Local settings could not be read: $($_.Exception.Message)"
                RuntimeRoot = ""
                WeatherPath = ""
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($runtimeRoot)) {
        return @{
            Status = "unavailable"
            Reason = "No EnergyPlus 24.2 runtime root was configured. Run 'dev.cmd setup -InstallEnergyPlus'."
            RuntimeRoot = ""
            WeatherPath = ""
        }
    }

    $runtimeRoot = [IO.Path]::GetFullPath($runtimeRoot)
    $runtimeExecutable = Join-Path $runtimeRoot "energyplus.exe"
    $runtimeIdd = Join-Path $runtimeRoot "Energy+.idd"
    if (-not (Test-Path -LiteralPath $runtimeExecutable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $runtimeIdd -PathType Leaf)) {
        return @{
            Status = "unavailable"
            Reason = "EnergyPlus root is incomplete: $runtimeRoot"
            RuntimeRoot = $runtimeRoot
            WeatherPath = ""
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($configuredWeather)) {
        try {
            $configuredWeather = Require-File $configuredWeather "Optional EPW override"
        }
        catch {
            return @{
                Status = "unavailable"
                Reason = $_.Exception.Message
                RuntimeRoot = $runtimeRoot
                WeatherPath = ""
            }
        }

        if (-not [string]::Equals(
                [IO.Path]::GetExtension($configuredWeather),
                ".epw",
                [StringComparison]::OrdinalIgnoreCase)) {
            return @{
                Status = "unavailable"
                Reason = "The optional weather override is not an EPW file: $configuredWeather"
                RuntimeRoot = $runtimeRoot
                WeatherPath = ""
            }
        }
    }

    if (-not (Test-Path -LiteralPath $weatherArchivePath -PathType Leaf)) {
        return @{
            Status = "unavailable"
            Reason = "The pinned SimpleDragon weather archive is missing. Run 'dev.cmd setup': $weatherArchivePath"
            RuntimeRoot = $runtimeRoot
            WeatherPath = if ([string]::IsNullOrWhiteSpace($configuredWeather)) { "" } else { $configuredWeather }
        }
    }

    $weatherArchive = Get-Item -LiteralPath $weatherArchivePath
    if ($weatherArchive.Length -ne $expectedWeatherArchiveSize) {
        return @{
            Status = "unavailable"
            Reason = "The pinned SimpleDragon weather archive has size $($weatherArchive.Length); expected $expectedWeatherArchiveSize bytes. Run 'dev.cmd setup'."
            RuntimeRoot = $runtimeRoot
            WeatherPath = if ([string]::IsNullOrWhiteSpace($configuredWeather)) { "" } else { $configuredWeather }
        }
    }

    try {
        $weatherArchiveSha256 = (Get-FileHash -LiteralPath $weatherArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    catch {
        return @{
            Status = "unavailable"
            Reason = "The pinned SimpleDragon weather archive could not be hashed: $($_.Exception.Message)"
            RuntimeRoot = $runtimeRoot
            WeatherPath = if ([string]::IsNullOrWhiteSpace($configuredWeather)) { "" } else { $configuredWeather }
        }
    }

    if (-not [string]::Equals(
            $weatherArchiveSha256,
            $expectedWeatherArchiveSha256,
            [StringComparison]::Ordinal)) {
        return @{
            Status = "unavailable"
            Reason = "The pinned SimpleDragon weather archive failed SHA-256 verification. Run 'dev.cmd setup'."
            RuntimeRoot = $runtimeRoot
            WeatherPath = if ([string]::IsNullOrWhiteSpace($configuredWeather)) { "" } else { $configuredWeather }
        }
    }

    $weatherReason = if ([string]::IsNullOrWhiteSpace($configuredWeather)) {
        "address-selected packaged EPW"
    }
    else {
        "address-selected packaged EPW plus optional override '$configuredWeather'"
    }

    return @{
        Status = "ready"
        Reason = "Verified EnergyPlus executable, IDD, and pinned SimpleDragon weather archive for $weatherReason."
        RuntimeRoot = $runtimeRoot
        WeatherPath = if ([string]::IsNullOrWhiteSpace($configuredWeather)) { "" } else { $configuredWeather }
    }
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

function Get-DescendantProcessIds([int]$RootProcessId) {
    try {
        $rows = @(Get-CimInstance Win32_Process -ErrorAction Stop |
            Select-Object ProcessId, ParentProcessId)
        $known = [Collections.Generic.HashSet[int]]::new()
        $frontier = [Collections.Generic.Queue[int]]::new()
        $frontier.Enqueue($RootProcessId)
        while ($frontier.Count -gt 0) {
            $parentId = $frontier.Dequeue()
            foreach ($row in $rows) {
                $candidateId = [int]$row.ProcessId
                if ([int]$row.ParentProcessId -eq $parentId -and $known.Add($candidateId)) {
                    $frontier.Enqueue($candidateId)
                }
            }
        }

        return @($known)
    }
    catch {
        Write-Verbose "Could not enumerate descendants of process $RootProcessId`: $($_.Exception.Message)"
        return @()
    }
}

function Save-ObservedDescendants([hashtable]$Observed, [int[]]$ProcessIds) {
    foreach ($descendantId in $ProcessIds) {
        if ($descendantId -le 0 -or $Observed.ContainsKey($descendantId)) {
            continue
        }

        $descendant = Get-Process -Id $descendantId -ErrorAction SilentlyContinue
        if ($null -eq $descendant) {
            continue
        }

        try {
            # Store a process identity, not only its reusable PID. A short-lived
            # child can exit while Rhino continues, and its PID must never make
            # a later unrelated process eligible for cleanup.
            $Observed[$descendantId] = $descendant.StartTime.ToUniversalTime().Ticks
        }
        catch {
            Write-Verbose "Could not identify descendant process $descendantId`: $($_.Exception.Message)"
        }
        finally {
            $descendant.Dispose()
        }
    }
}

function Stop-ProcessTree(
    [Diagnostics.Process]$Process,
    [string]$Reason,
    [hashtable]$KnownDescendants = @{}) {
    if ($null -eq $Process) {
        return
    }

    $processId = $null
    $processRunning = $false
    try {
        $processId = $Process.Id
        $processRunning = -not $Process.HasExited
    }
    catch {
        if ($KnownDescendants.Count -eq 0) {
            return
        }
    }

    $descendantSet = [Collections.Generic.HashSet[int]]::new()
    foreach ($entry in $KnownDescendants.GetEnumerator()) {
        $descendantId = [int]$entry.Key
        $descendant = Get-Process -Id $descendantId -ErrorAction SilentlyContinue
        if ($null -eq $descendant) {
            continue
        }

        try {
            if ($descendant.StartTime.ToUniversalTime().Ticks -eq [long]$entry.Value) {
                $null = $descendantSet.Add($descendantId)
            }
        }
        catch {
            Write-Verbose "Could not confirm descendant process $descendantId identity: $($_.Exception.Message)"
        }
        finally {
            $descendant.Dispose()
        }
    }

    # A Win32_Process row retains its creating parent ID even after that parent
    # exits, so take one last snapshot on every path. This catches EnergyPlus
    # descendants that outlive a Rhino host which has already terminated.
    if ($null -ne $processId) {
        foreach ($descendantId in @(Get-DescendantProcessIds $processId)) {
            $null = $descendantSet.Add([int]$descendantId)
        }
    }

    $taskKill = Join-Path $env:SystemRoot "System32\taskkill.exe"
    if ($processRunning -and (Test-Path -LiteralPath $taskKill -PathType Leaf)) {
        try {
            $taskKillOutput = & $taskKill /PID $processId /T /F 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Verbose ("taskkill could not terminate process tree {0} ({1}): {2}" -f `
                    $processId, $Reason, ($taskKillOutput -join " | "))
            }
        }
        catch {
            Write-Verbose "taskkill failed for process tree $processId ($Reason): $($_.Exception.Message)"
        }
    }

    if ($null -ne $processId) {
        foreach ($descendantId in @(Get-DescendantProcessIds $processId)) {
            $null = $descendantSet.Add([int]$descendantId)
        }
    }

    $descendantIds = @($descendantSet | Sort-Object)
    $descendantDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $remainingDescendants = @()
        foreach ($descendantId in $descendantIds) {
            $descendant = Get-Process -Id $descendantId -ErrorAction SilentlyContinue
            if ($null -eq $descendant) {
                continue
            }

            $remainingDescendants += $descendantId
            try {
                Stop-Process -Id $descendantId -Force -ErrorAction Stop
            }
            catch {
                Write-Verbose "Could not terminate descendant process $descendantId ($Reason): $($_.Exception.Message)"
            }
        }

        if ($remainingDescendants.Count -gt 0) {
            Start-Sleep -Milliseconds 100
        }
    } while ($remainingDescendants.Count -gt 0 -and [DateTime]::UtcNow -lt $descendantDeadline)

    $stillRunning = @($descendantIds | Where-Object {
        $null -ne (Get-Process -Id $_ -ErrorAction SilentlyContinue)
    })
    if ($stillRunning.Count -gt 0) {
        throw "Descendant processes survived bounded host cleanup: $($stillRunning -join ', ')"
    }

    if ($null -ne $processId) {
        try {
            if (-not $Process.HasExited) {
                $Process.Kill()
            }
        }
        catch {
            Write-Verbose "Fallback process termination failed for $processId ($Reason): $($_.Exception.Message)"
        }

        try {
            $null = $Process.WaitForExit(10000)
        }
        catch {
            Write-Verbose "Could not confirm process $processId termination ($Reason): $($_.Exception.Message)"
        }
    }
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
    $stdoutTask = $null
    $stderrTask = $null
    $timedOut = $false
    $observedDescendants = @{}
    try {
        if (-not $process.Start()) {
            throw "Host process did not start: $FilePath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $nextDescendantSnapshot = [DateTime]::UtcNow
        while (-not $process.WaitForExit(250)) {
            if ([DateTime]::UtcNow -ge $nextDescendantSnapshot) {
                Save-ObservedDescendants $observedDescendants @(Get-DescendantProcessIds $process.Id)
                $nextDescendantSnapshot = [DateTime]::UtcNow.AddSeconds(1)
            }

            if ([DateTime]::UtcNow -ge $deadline) {
                $timedOut = $true
                break
            }
        }

        if ($timedOut) {
            Save-ObservedDescendants $observedDescendants @(Get-DescendantProcessIds $process.Id)
            Stop-ProcessTree $process "host timeout" $observedDescendants
        }

        if (-not $process.HasExited -and -not $process.WaitForExit(10000)) {
            throw "Host process tree did not terminate after bounded cleanup. See $OutputDirectory"
        }

        $process.WaitForExit()
        $exitCode = $process.ExitCode
        [IO.File]::WriteAllText($stdoutPath, $stdoutTask.Result)
        [IO.File]::WriteAllText($stderrPath, $stderrTask.Result)
    }
    finally {
        if ($null -ne $process) {
            try {
                Stop-ProcessTree $process "host cleanup" $observedDescendants
            }
            finally {
                $process.Dispose()
            }
        }
    }

    if ($timedOut) {
        throw "Host process tree exceeded the $TimeoutSeconds-second limit and was terminated. See $OutputDirectory"
    }

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
    [string]$RhinoExecutable,
    [hashtable]$EnergyPlusWorkflow) {
    $outputKey = switch ("$Action/$HostName") {
        "Generate/Rhino7" { "g7" }
        "Validate/Rhino7" { "v7" }
        "Validate/Rhino8" { "v8" }
        default { throw "Unknown example host/action pair: $Action/$HostName" }
    }
    # Short host directory names keep Rhino 7 batch evidence below legacy MAX_PATH.
    $output = Join-Path $runRoot $outputKey
    $environment = @{
        DRAGONS_EXAMPLE_ACTION = $Action
        DRAGONS_INVISIBLE_GHA = $InvisibleGha
        DRAGONS_SIMPLE_GHA = $SimpleGha
        DRAGONS_EXAMPLES_ROOT = $examplesRoot
        DRAGONS_EXAMPLES_OUTPUT = $output
        DRAGONS_ENERGYPLUS_GATE_STATUS = $EnergyPlusWorkflow.Status
        DRAGONS_ENERGYPLUS_GATE_REASON = $EnergyPlusWorkflow.Reason
        DRAGONS_ENERGYPLUS_ROOT = $EnergyPlusWorkflow.RuntimeRoot
        DRAGONS_ENERGYPLUS_WEATHER = $EnergyPlusWorkflow.WeatherPath
        DRAGONS_ENERGYPLUS_WORKFLOW_TIMEOUT_SECONDS = $WorkflowStageTimeoutSeconds
    }
    if ($HostName -eq "Rhino7") {
        $environment.DRAGONS_RHINO7_EXE = $RhinoExecutable
    }
    else {
        $environment.DRAGONS_RHINO8_EXE = $RhinoExecutable
    }

    Invoke-BoundedHost $Runner $Arguments $output $environment
    $summaryPath = Require-File (Join-Path $output "summary.json") "$HostName $Action summary"
    if ($RequireEnergyPlusWorkflow) {
        $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        $workflowRows = @($summary.definitions | Where-Object {
            $_.fileName -eq "14-simpledragon-two-zone-run-results-csv.gh"
        })
        if ($workflowRows.Count -ne 1) {
            throw "$HostName $Action summary must contain exactly one executable EnergyPlus workflow row."
        }

        $workflow = $workflowRows[0]
        $requiredBooleans = @(
            "runtimeExecuted",
            "runtimeResultVerified",
            "runtimeCsvVerified",
            "runtimeCacheVerified",
            "runtimeCancellationVerified",
            "runtimeBatchVerified",
            "runtimeBatchCancellationVerified"
        )
        if ([string]$workflow.runtimeGateStatus -ne "ready") {
            throw "$HostName $Action did not use the ready EnergyPlus gate: $($workflow.runtimeGateStatus)"
        }

        foreach ($property in $requiredBooleans) {
            if ($workflow.$property -ne $true) {
                throw "$HostName $Action summary did not verify $property."
            }
        }

        $requiredStates = @{
            runtimeFirstRunState = "Succeeded"
            runtimeCachedRunState = "Cached"
            runtimeCancellationState = "Cancelled"
            runtimeFirstBatchState = "Succeeded"
            runtimeCachedBatchState = "Succeeded"
            runtimeBatchCancellationState = "Cancelled"
        }
        foreach ($property in $requiredStates.Keys) {
            if ([string]$workflow.$property -ne $requiredStates[$property]) {
                throw ("{0} {1} summary reported {2}='{3}', expected '{4}'." -f `
                    $HostName, $Action, $property, $workflow.$property, $requiredStates[$property])
            }
        }

        if ([string]$workflow.runtimeState -ne "Cancelled") {
            throw "$HostName $Action summary did not preserve the final intentional Run cancellation state."
        }

        if ($null -eq $workflow.runtimeAnnualResult) {
            throw "$HostName $Action summary contains no annual result."
        }

        $annualResult = [double]$workflow.runtimeAnnualResult
        if ([double]::IsNaN($annualResult) -or [double]::IsInfinity($annualResult)) {
            throw "$HostName $Action summary contains a non-finite annual result."
        }

        $evidenceDirectory = [IO.Path]::GetFullPath([string]$workflow.runtimeEvidenceDirectory)
        if (-not [string]::Equals(
                $evidenceDirectory.TrimEnd('\', '/'),
                ([IO.Path]::GetFullPath($output)).TrimEnd('\', '/'),
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "$HostName $Action runtime evidence escaped its host output directory: $evidenceDirectory"
        }

        $csvHashes = @($workflow.runtimeCsvSha256)
        if ($csvHashes.Count -lt 4 -or @($csvHashes | Where-Object {
                [string]$_ -notmatch '^[^=]+=[0-9a-f]{64}$'
            }).Count -ne 0) {
            throw "$HostName $Action summary did not contain complete CSV evidence hashes."
        }

        foreach ($property in @(
                "runtimeBatchCombinedCsvSha256",
                "runtimeBatchManifestSha256",
                "runtimeBatchCancellationCsvSha256",
                "runtimeBatchCancellationManifestSha256")) {
            if ([string]$workflow.$property -notmatch '^[0-9a-f]{64}$') {
                throw "$HostName $Action summary did not contain a valid $property hash."
            }
        }
    }
}

try {
    if ($SkipEnergyPlusWorkflow -and $RequireEnergyPlusWorkflow) {
        throw "-SkipEnergyPlusWorkflow and -RequireEnergyPlusWorkflow cannot be used together."
    }

    if ($Generate -and $Target -ne "All") {
        throw "-Generate requires -Target All so every Rhino 7-authored binary is immediately validated in Rhino 8."
    }

    $script:dotnet = Resolve-DotNet
    $energyPlusWorkflow = Resolve-EnergyPlusWorkflow
    Write-Host (
        "EnergyPlus example workflow gate: $($energyPlusWorkflow.Status) - " +
        $energyPlusWorkflow.Reason)
    if ($RequireEnergyPlusWorkflow -and $energyPlusWorkflow.Status -ne "ready") {
        throw "The required EnergyPlus example workflow is not ready: $($energyPlusWorkflow.Reason)"
    }
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
            Invoke-ExampleHost "Rhino7" "Generate" $rhino7Runner @() $invisible7 $simple7 $rhino7Exe $energyPlusWorkflow
        }

        if ($Target -in @("All", "Rhino7")) {
            Invoke-ExampleHost "Rhino7" "Validate" $rhino7Runner @() $invisible7 $simple7 $rhino7Exe $energyPlusWorkflow
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
        Invoke-ExampleHost "Rhino8" "Validate" $script:dotnet @($rhino8Runner) $invisible8 $simple8 $rhino8Exe $energyPlusWorkflow
    }

    $passDescription = if ($RequireEnergyPlusWorkflow) {
        "Grasshopper example-definition gate passed with required EnergyPlus execution"
    }
    else {
        "Grasshopper structural example-definition gate passed; EnergyPlus status was $($energyPlusWorkflow.Status)"
    }
    [IO.File]::WriteAllText(
        (Join-Path $runRoot "PASS.txt"),
        "$passDescription for $Target at $([DateTimeOffset]::UtcNow.ToString('O'))." + [Environment]::NewLine)
    if ($RequireEnergyPlusWorkflow) {
        [IO.File]::WriteAllText(
            (Join-Path $runRoot "ENERGYPLUS-WORKFLOW-PASS.txt"),
            "Every requested Rhino host reported ready, executed, result, CSV, cache, run cancellation, batch, and batch cancellation evidence." +
                [Environment]::NewLine)
    }
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
