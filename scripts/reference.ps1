#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [ValidateSet('Generate', 'Verify')]
    [string] $Mode = 'Generate',

    [string] $OutputDirectory,

    [string] $BaselineDirectory,

    [string] $UpstreamPath,

    [switch] $RefreshDependencies,

    [switch] $UpdateBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-RepositoryRoot -ScriptDirectory $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot '.config\local.settings.json'
$lockPath = Join-Path $repositoryRoot 'upstream\upstream.lock.json'
$requirementsPath = Join-Path $repositoryRoot 'tools\python-reference\requirements.lock.txt'
$bootstrapPath = Join-Path $repositoryRoot 'tools\python-reference\bootstrap_reference.py'
$generatorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_reference.py'
$profileGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_usage_profile_schedule_oracle.py'
$usageProfileCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_usage_profile_core_oracle.py'
$utilsCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_utils_core_oracle.py'
$commonCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_common_core_oracle.py'
$constantsMetadataGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_constants_metadata_oracle.py'
$constantsEngineeringGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_constants_engineering_oracle.py'
$epsimpleConstantsNumericGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_constants_numeric_oracle.py'
$epsimpleConstructionCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_construction_core_oracle.py'
$epsimpleHvacEnumsBaseGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_hvac_enums_base_oracle.py'
$epsimpleHvacOtherSystemsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_hvac_other_systems_oracle.py'
$epsimpleHvacThermalSourceGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_hvac_thermal_source_oracle.py'
$epsimpleHvacSupplySystemGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_hvac_supply_system_oracle.py'
$epsimpleIdentifierConventionsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_identifier_conventions_oracle.py'
$epsimpleModelCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_model_core_oracle.py'
$epsimpleModelResultGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_model_result_oracle.py'
$epsimpleShapeCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_epsimple_shape_core_oracle.py'
$dragonConstructionAirBoundaryCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_construction_air_boundary_core_oracle.py'
$dragonConstructionCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_construction_core_oracle.py'
$dragonConstructionToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_construction_to_idf_object_oracle.py'
$dragonHvacAppendersControllersGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_appenders_controllers_oracle.py'
$dragonHvacMiscSystemsCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_misc_systems_core_oracle.py'
$dragonHvacPhotovoltaicToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_photovoltaic_to_idf_object_oracle.py'
$dragonHvacSourceSystemToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_source_system_to_idf_object_oracle.py'
$dragonHvacSourceTowerCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_source_tower_core_oracle.py'
$dragonHvacSupplyCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_supply_core_oracle.py'
$dragonHvacSupplyGroupCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_supply_group_core_oracle.py'
$dragonHvacSupplyGroupToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_hvac_supply_group_to_idf_object_oracle.py'
$dragonShapeGeometryCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_geometry_core_oracle.py'
$dragonShapeOpeningAdjacencyCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_opening_adjacency_core_oracle.py'
$dragonShapeShadingMaterialToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_shading_material_to_idf_object_oracle.py'
$dragonShapeSurfaceToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_surface_to_idf_object_oracle.py'
$dragonShapeZoneCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_zone_core_oracle.py'
$dragonShapeZoneToIdfObjectGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_shape_zone_to_idf_object_oracle.py'
$dragonModelAddSupplySystemGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_add_supply_system_oracle.py'
$dragonModelAssemblyGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_assembly_oracle.py'
$dragonModelClassGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_class_oracle.py'
$dragonModelConditioningGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_conditioning_oracle.py'
$dragonModelConstructionDefaultsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_construction_defaults_oracle.py'
$dragonModelProjectionsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_projections_oracle.py'
$dragonModelTerrainGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_dragon_model_terrain_oracle.py'
$imugiIddDefinitionsCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_imugi_idd_definitions_core_oracle.py'
$launcherResultParserGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_launcher_result_parser_oracle.py'
$launcherRuntimeGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_launcher_runtime_oracle.py'
$iddGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_idd_schema_oracle.py'
$constructionEqualityGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_construction_equality_hash_oracle.py'
$scheduleTypeGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_schedule_type_oracle.py'
$dayScheduleCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_day_schedule_core_oracle.py'
$dayScheduleMetricsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_day_schedule_metrics_oracle.py'
$dayScheduleOperationsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_day_schedule_operations_oracle.py'
$ruleSetCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_rule_set_core_oracle.py'
$ruleSetOperationsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_rule_set_operations_oracle.py'
$scheduleCoreGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_schedule_core_oracle.py'
$scheduleOperationsGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_schedule_operations_oracle.py'
$profileResidualGeneratorPath = Join-Path $repositoryRoot 'tools\python-reference\generate_profile_residual_oracle.py'
$constructionEqualityTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_construction_equality_hash_oracle.py'
$scheduleTypeTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_schedule_type_oracle.py'
$dayScheduleCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_day_schedule_core_oracle.py'
$dayScheduleMetricsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_day_schedule_metrics_oracle.py'
$dayScheduleOperationsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_day_schedule_operations_oracle.py'
$ruleSetCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_rule_set_core_oracle.py'
$ruleSetOperationsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_rule_set_operations_oracle.py'
$scheduleCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_schedule_core_oracle.py'
$scheduleOperationsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_schedule_operations_oracle.py'
$profileResidualTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_profile_residual_oracle.py'
$usageProfileCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_usage_profile_core_oracle.py'
$utilsCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_utils_core_oracle.py'
$commonCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_common_core_oracle.py'
$constantsMetadataTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_constants_metadata_oracle.py'
$constantsEngineeringTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_constants_engineering_oracle.py'
$epsimpleConstantsNumericTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_constants_numeric_oracle.py'
$epsimpleConstructionCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_construction_core_oracle.py'
$epsimpleHvacEnumsBaseTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_hvac_enums_base_oracle.py'
$epsimpleHvacOtherSystemsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_hvac_other_systems_oracle.py'
$epsimpleHvacThermalSourceTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_hvac_thermal_source_oracle.py'
$epsimpleHvacSupplySystemTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_hvac_supply_system_oracle.py'
$epsimpleIdentifierConventionsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_identifier_conventions_oracle.py'
$epsimpleModelCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_model_core_oracle.py'
$epsimpleModelResultTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_model_result_oracle.py'
$epsimpleShapeCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_epsimple_shape_core_oracle.py'
$dragonConstructionAirBoundaryCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_construction_air_boundary_core_oracle.py'
$dragonConstructionCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_construction_core_oracle.py'
$dragonConstructionToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_construction_to_idf_object_oracle.py'
$dragonHvacAppendersControllersTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_appenders_controllers_oracle.py'
$dragonHvacMiscSystemsCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_misc_systems_core_oracle.py'
$dragonHvacPhotovoltaicToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_photovoltaic_to_idf_object_oracle.py'
$dragonHvacSourceSystemToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_source_system_to_idf_object_oracle.py'
$dragonHvacSourceTowerCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_source_tower_core_oracle.py'
$dragonHvacSupplyCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_supply_core_oracle.py'
$dragonHvacSupplyGroupCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_supply_group_core_oracle.py'
$dragonHvacSupplyGroupToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_hvac_supply_group_to_idf_object_oracle.py'
$dragonShapeGeometryCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_geometry_core_oracle.py'
$dragonShapeOpeningAdjacencyCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_opening_adjacency_core_oracle.py'
$dragonShapeShadingMaterialToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_shading_material_to_idf_object_oracle.py'
$dragonShapeSurfaceToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_surface_to_idf_object_oracle.py'
$dragonShapeZoneCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_zone_core_oracle.py'
$dragonShapeZoneToIdfObjectTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_shape_zone_to_idf_object_oracle.py'
$dragonModelAddSupplySystemTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_add_supply_system_oracle.py'
$dragonModelAssemblyTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_assembly_oracle.py'
$dragonModelClassTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_class_oracle.py'
$dragonModelConditioningTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_conditioning_oracle.py'
$dragonModelConstructionDefaultsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_construction_defaults_oracle.py'
$dragonModelProjectionsTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_projections_oracle.py'
$dragonModelTerrainTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_dragon_model_terrain_oracle.py'
$imugiIddDefinitionsCoreTestPath = Join-Path $repositoryRoot 'tests\PythonReference\test_imugi_idd_definitions_core_oracle.py'
$publicSymbolInventoryPath = Join-Path $repositoryRoot 'upstream\public-symbol-inventory.json'
$tempRoot = Join-Path $repositoryRoot 'temp'
$referenceTempRoot = Join-Path $tempRoot 'reference'
$logsRoot = Join-Path $referenceTempRoot 'logs'
$dependencyParent = Join-Path $repositoryRoot '.tools\python-reference\3.12.7'
$dependencyRoot = Join-Path $dependencyParent 'site-packages'
$dependencyStamp = Join-Path $dependencyParent 'installed.json'
$pipCache = Join-Path $referenceTempRoot 'pip-cache'
$pipWheel = Join-Path $repositoryRoot '.tools\python-reference\bootstrap\pip-24.3.1-py3-none-any.whl'
$pipWheelUri = 'https://files.pythonhosted.org/packages/ef/7d/500c9ad20238fcfcb4cb9243eede163594d7020ce87bd9610c9e02771876/pip-24.3.1-py3-none-any.whl'
$pipWheelSha256 = '3790624780082365f47549d032f3770eeb2b1e8bd1f7b2e02dace1afa361b4ed'
$requiredPythonVersion = '3.12.7'
$requiredEnergyPlusVersion = '24.2.0'
$requiredEnergyPlusBuild = '94a887817b'
$requiredEnergyPlusIddSha256 = '3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2'
$requiredEnergyPlusEpJsonSchemaSha256 = 'aefb16d63495d170468ecab3c935f1aeb68eb07c6551403dd11cbba61cb136fa'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $referenceTempRoot 'python-output'
}
if ([string]::IsNullOrWhiteSpace($BaselineDirectory)) {
    $BaselineDirectory = Join-Path $repositoryRoot 'fixtures\reference\python-0.7.0'
}

$outputRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $OutputDirectory `
    -AllowedTopLevelNames @('temp')

$mutexHash = $null
$mutexHasher = [System.Security.Cryptography.SHA256]::Create()
try {
    $mutexBytes = [System.Text.Encoding]::UTF8.GetBytes(
        $repositoryRoot.ToLowerInvariant())
    $mutexHash = ([System.BitConverter]::ToString(
        $mutexHasher.ComputeHash($mutexBytes)) -replace '-', '').ToLowerInvariant()
}
finally {
    $mutexHasher.Dispose()
}
$referenceMutex = [System.Threading.Mutex]::new(
    $false,
    "Local\GonieGonie.Reference.$mutexHash")
$referenceMutexAcquired = $false
try {
    $referenceMutexAcquired = $referenceMutex.WaitOne(0)
}
catch [System.Threading.AbandonedMutexException] {
    $referenceMutexAcquired = $true
}
if (-not $referenceMutexAcquired) {
    $referenceMutex.Dispose()
    throw 'Another reference-oracle command is already running for this repository.'
}

$baselineRoot = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $BaselineDirectory `
    -AllowedTopLevelNames @('fixtures')
$dependencyParent = Assert-RepositoryChildPath `
    -RepositoryRoot $repositoryRoot `
    -Path $dependencyParent `
    -AllowedTopLevelNames @('.tools')

if ($Mode -eq 'Verify' -and $UpdateBaseline) {
    throw '-UpdateBaseline cannot be combined with -Mode Verify.'
}

foreach ($requiredFile in @(
    $settingsPath,
    $lockPath,
    $requirementsPath,
    $bootstrapPath,
    $generatorPath,
    $profileGeneratorPath,
    $usageProfileCoreGeneratorPath,
    $utilsCoreGeneratorPath,
    $commonCoreGeneratorPath,
    $constantsMetadataGeneratorPath,
    $constantsEngineeringGeneratorPath,
    $epsimpleConstantsNumericGeneratorPath,
    $epsimpleConstructionCoreGeneratorPath,
    $epsimpleHvacEnumsBaseGeneratorPath,
    $epsimpleHvacOtherSystemsGeneratorPath,
    $epsimpleHvacThermalSourceGeneratorPath,
    $epsimpleHvacSupplySystemGeneratorPath,
    $epsimpleIdentifierConventionsGeneratorPath,
    $epsimpleModelCoreGeneratorPath,
    $epsimpleModelResultGeneratorPath,
    $epsimpleShapeCoreGeneratorPath,
    $dragonConstructionAirBoundaryCoreGeneratorPath,
    $dragonConstructionCoreGeneratorPath,
    $dragonConstructionToIdfObjectGeneratorPath,
    $dragonHvacAppendersControllersGeneratorPath,
    $dragonHvacMiscSystemsCoreGeneratorPath,
    $dragonHvacPhotovoltaicToIdfObjectGeneratorPath,
    $dragonHvacSourceSystemToIdfObjectGeneratorPath,
    $dragonHvacSourceTowerCoreGeneratorPath,
    $dragonHvacSupplyCoreGeneratorPath,
    $dragonHvacSupplyGroupCoreGeneratorPath,
    $dragonHvacSupplyGroupToIdfObjectGeneratorPath,
    $dragonShapeGeometryCoreGeneratorPath,
    $dragonShapeOpeningAdjacencyCoreGeneratorPath,
    $dragonShapeShadingMaterialToIdfObjectGeneratorPath,
    $dragonShapeSurfaceToIdfObjectGeneratorPath,
    $dragonShapeZoneCoreGeneratorPath,
    $dragonShapeZoneToIdfObjectGeneratorPath,
    $dragonModelAddSupplySystemGeneratorPath,
    $dragonModelAssemblyGeneratorPath,
    $dragonModelClassGeneratorPath,
    $dragonModelConditioningGeneratorPath,
    $dragonModelConstructionDefaultsGeneratorPath,
    $dragonModelProjectionsGeneratorPath,
    $dragonModelTerrainGeneratorPath,
    $imugiIddDefinitionsCoreGeneratorPath,
    $iddGeneratorPath,
    $constructionEqualityGeneratorPath,
    $scheduleTypeGeneratorPath,
    $dayScheduleCoreGeneratorPath,
    $dayScheduleMetricsGeneratorPath,
    $dayScheduleOperationsGeneratorPath,
    $ruleSetCoreGeneratorPath,
    $ruleSetOperationsGeneratorPath,
    $scheduleCoreGeneratorPath,
    $scheduleOperationsGeneratorPath,
    $profileResidualGeneratorPath,
    $constructionEqualityTestPath,
    $scheduleTypeTestPath,
    $dayScheduleCoreTestPath,
    $dayScheduleMetricsTestPath,
    $dayScheduleOperationsTestPath,
    $ruleSetCoreTestPath,
    $ruleSetOperationsTestPath,
    $scheduleCoreTestPath,
    $scheduleOperationsTestPath,
    $profileResidualTestPath,
    $usageProfileCoreTestPath,
    $utilsCoreTestPath,
    $commonCoreTestPath,
    $constantsMetadataTestPath,
    $constantsEngineeringTestPath,
    $epsimpleConstantsNumericTestPath,
    $epsimpleConstructionCoreTestPath,
    $epsimpleHvacEnumsBaseTestPath,
    $epsimpleHvacOtherSystemsTestPath,
    $epsimpleHvacThermalSourceTestPath,
    $epsimpleHvacSupplySystemTestPath,
    $epsimpleIdentifierConventionsTestPath,
    $epsimpleModelCoreTestPath,
    $epsimpleModelResultTestPath,
    $epsimpleShapeCoreTestPath,
    $dragonConstructionAirBoundaryCoreTestPath,
    $dragonConstructionCoreTestPath,
    $dragonConstructionToIdfObjectTestPath,
    $dragonHvacAppendersControllersTestPath,
    $dragonHvacMiscSystemsCoreTestPath,
    $dragonHvacPhotovoltaicToIdfObjectTestPath,
    $dragonHvacSourceSystemToIdfObjectTestPath,
    $dragonHvacSourceTowerCoreTestPath,
    $dragonHvacSupplyCoreTestPath,
    $dragonHvacSupplyGroupCoreTestPath,
    $dragonHvacSupplyGroupToIdfObjectTestPath,
    $dragonShapeGeometryCoreTestPath,
    $dragonShapeOpeningAdjacencyCoreTestPath,
    $dragonShapeShadingMaterialToIdfObjectTestPath,
    $dragonShapeSurfaceToIdfObjectTestPath,
    $dragonShapeZoneCoreTestPath,
    $dragonShapeZoneToIdfObjectTestPath,
    $dragonModelAddSupplySystemTestPath,
    $dragonModelAssemblyTestPath,
    $dragonModelClassTestPath,
    $dragonModelConditioningTestPath,
    $dragonModelConstructionDefaultsTestPath,
    $dragonModelProjectionsTestPath,
    $dragonModelTerrainTestPath,
    $imugiIddDefinitionsCoreTestPath,
    $publicSymbolInventoryPath
)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required reference-oracle input is missing: '$requiredFile'. Run 'dev.cmd setup' if local.settings.json is absent."
    }
}

$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$pythonSettings = $settings.PSObject.Properties['pythonOracle']
if ($null -eq $pythonSettings -or [string] $pythonSettings.Value.status -ne 'ready') {
    throw "The exact Python oracle is not configured. Run 'dev.cmd setup' without -SkipPythonInstall."
}

$pythonExecutable = [string] $pythonSettings.Value.executable
if (-not (Test-Path -LiteralPath $pythonExecutable -PathType Leaf)) {
    throw "The setup-selected Python executable no longer exists: '$pythonExecutable'. Re-run 'dev.cmd setup'."
}

$pythonIdentity = @(& $pythonExecutable -c "import sys; print('%d.%d.%d' % sys.version_info[:3])" 2>$null)
if ($LASTEXITCODE -ne 0 -or $pythonIdentity.Count -eq 0 -or [string] $pythonIdentity[-1] -ne $requiredPythonVersion) {
    $reported = if ($pythonIdentity.Count -gt 0) { [string] $pythonIdentity[-1] } else { '<none>' }
    throw "Python $requiredPythonVersion is required for the reference oracle; configured interpreter reported '$reported'."
}

$energyPlusSettings = $settings.PSObject.Properties['energyPlus']
if ($null -eq $energyPlusSettings -or [string] $energyPlusSettings.Value.status -ne 'ready') {
    throw "The EnergyPlus IDD oracle is not configured. Run 'dev.cmd setup -InstallEnergyPlus'."
}

$energyPlusIddPath = [string] $energyPlusSettings.Value.idd
if (-not (Test-Path -LiteralPath $energyPlusIddPath -PathType Leaf)) {
    throw "The setup-selected EnergyPlus IDD no longer exists: '$energyPlusIddPath'. Re-run 'dev.cmd setup'."
}
$energyPlusEpJsonSchemaPath = [string] $energyPlusSettings.Value.epJsonSchema
if (-not (Test-Path -LiteralPath $energyPlusEpJsonSchemaPath -PathType Leaf)) {
    throw "The setup-selected official EnergyPlus epJSON schema no longer exists: '$energyPlusEpJsonSchemaPath'. Re-run 'dev.cmd setup'."
}
if ([string] $energyPlusSettings.Value.version -ne $requiredEnergyPlusVersion -or
    [string] $energyPlusSettings.Value.build -ne $requiredEnergyPlusBuild) {
    throw "EnergyPlus $requiredEnergyPlusVersion build $requiredEnergyPlusBuild is required for the IDD oracle."
}
$actualEnergyPlusIddSha256 = Get-Sha256 -Path $energyPlusIddPath
if ($actualEnergyPlusIddSha256 -ne $requiredEnergyPlusIddSha256) {
    throw "EnergyPlus IDD hash mismatch. Expected '$requiredEnergyPlusIddSha256', found '$actualEnergyPlusIddSha256'."
}
$actualEnergyPlusEpJsonSchemaSha256 = Get-Sha256 -Path $energyPlusEpJsonSchemaPath
if ($actualEnergyPlusEpJsonSchemaSha256 -ne $requiredEnergyPlusEpJsonSchemaSha256) {
    throw "Official EnergyPlus epJSON schema hash mismatch. Expected '$requiredEnergyPlusEpJsonSchemaSha256', found '$actualEnergyPlusEpJsonSchemaSha256'."
}

$upstreamLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$upstreamRepository = [string] $upstreamLock.repository
$upstreamCommit = [string] $upstreamLock.commit
$manageUpstream = [string]::IsNullOrWhiteSpace($UpstreamPath)
if ($manageUpstream) {
    $UpstreamPath = Join-Path $referenceTempRoot 'upstream\eplussimple'
    $UpstreamPath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $UpstreamPath `
        -AllowedTopLevelNames @('temp')
}
else {
    $UpstreamPath = [System.IO.Path]::GetFullPath($UpstreamPath)
}

function Invoke-CapturedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $ArgumentList = @(),

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $FilePath @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode): $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function Reset-ReferenceOwnedTree {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedTopLevelNames
    )

    $safePath = Assert-RepositoryChildPath `
        -RepositoryRoot $repositoryRoot `
        -Path $Path `
        -AllowedTopLevelNames $AllowedTopLevelNames
    if (-not (Test-Path -LiteralPath $safePath)) {
        return
    }

    Assert-NoReparsePoints -Path $safePath -AnchorPath $repositoryRoot
    if ($WhatIfPreference) {
        Write-Host "What if: remove reference-owned directory '$safePath'."
        return
    }

    Remove-Item -LiteralPath $safePath -Recurse -Force
}

function Get-CanonicalRemoteUrl {
    param([Parameter(Mandatory = $true)][string] $Url)

    return $Url.Trim().TrimEnd('/').ToLowerInvariant() -replace '\.git$', ''
}

function Test-UpstreamCheckoutAuthority {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Commit,
        [Parameter(Mandatory = $true)][string] $Repository
    )

    if (-not (Test-Path -LiteralPath (Join-Path $Path '.git') -PathType Container)) {
        return $false
    }
    $trackerRoot = Join-Path $repositoryRoot 'tools\upstream-tracker'
    $program = @'
from pathlib import Path
import sys
sys.path.insert(0, sys.argv[1])
from goniegonie_upstream_tracker.classifier import inspect_source_identity
identity = inspect_source_identity(
    Path(sys.argv[2]),
    expected_commit=sys.argv[3],
    expected_repository=sys.argv[4],
)
raise SystemExit(0 if identity.pin_verified else 1)
'@
    $previousBytecodePolicy = [Environment]::GetEnvironmentVariable(
        'PYTHONDONTWRITEBYTECODE',
        [EnvironmentVariableTarget]::Process)
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $env:PYTHONDONTWRITEBYTECODE = '1'
        $ErrorActionPreference = 'Continue'
        $null = @(& $pythonExecutable -I -X utf8 -c $program `
            $trackerRoot $Path $Commit $Repository 2>&1)
        return $LASTEXITCODE -eq 0
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            'PYTHONDONTWRITEBYTECODE',
            $previousBytecodePolicy,
            [EnvironmentVariableTarget]::Process)
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Initialize-UpstreamCheckout {
    $gitCommand = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -eq $gitCommand) {
        $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    }
    if ($null -eq $gitCommand) {
        throw 'Git is required to materialize the pinned Python reference source.'
    }

    if (Test-Path -LiteralPath $UpstreamPath) {
        $authoritative = Test-UpstreamCheckoutAuthority `
            -Path $UpstreamPath `
            -Commit $upstreamCommit `
            -Repository $upstreamRepository
        if (-not $authoritative) {
            if (-not $manageUpstream) {
                throw "The explicitly selected upstream checkout is not byte-exact at the pinned commit: '$UpstreamPath'."
            }
            Write-Host "Recreating non-authoritative managed reference checkout: $UpstreamPath"
            Reset-ReferenceOwnedTree -Path $UpstreamPath -AllowedTopLevelNames @('temp')
        }
    }

    $newCheckout = $false
    if (-not (Test-Path -LiteralPath $UpstreamPath -PathType Container)) {
        if (-not $manageUpstream) {
            throw "The explicitly selected upstream checkout does not exist: '$UpstreamPath'."
        }

        Ensure-Directory -Path (Split-Path -Parent $UpstreamPath)
        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Clone pinned upstream repository $upstreamRepository")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('clone', '--filter=blob:none', '--no-checkout', $upstreamRepository, $UpstreamPath) `
                -LogPath (Join-Path $logsRoot 'upstream-clone.log') `
                -FailureMessage 'Cloning the Python reference source failed'
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'config', '--local', 'core.autocrlf', 'false') `
                -LogPath (Join-Path $logsRoot 'upstream-git-config.log') `
                -FailureMessage 'Configuring the reference checkout for byte-exact verification failed'
            $newCheckout = $true
        }
    }

    if ($WhatIfPreference -and -not (Test-Path -LiteralPath (Join-Path $UpstreamPath '.git'))) {
        Write-Host "What if: fetch and check out upstream commit $upstreamCommit."
        return
    }

    if (-not (Test-Path -LiteralPath (Join-Path $UpstreamPath '.git') -PathType Container)) {
        throw "The upstream path is not a Git checkout: '$UpstreamPath'."
    }

    $remote = [string] (@(Invoke-CapturedNativeCommand `
        -FilePath $gitCommand.Source `
        -ArgumentList @('-C', $UpstreamPath, 'remote', 'get-url', 'origin') `
        -FailureMessage 'Reading the upstream origin failed')[-1])
    if ((Get-CanonicalRemoteUrl -Url $remote) -ne (Get-CanonicalRemoteUrl -Url $upstreamRepository)) {
        throw "The selected checkout origin '$remote' does not match the pinned repository '$upstreamRepository'."
    }

    if ($newCheckout) {
        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Fetch and check out pinned upstream commit $upstreamCommit")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'fetch', '--depth', '1', 'origin', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-fetch.log') `
                -FailureMessage 'Fetching the pinned Python reference commit failed'
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'checkout', '--detach', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-checkout.log') `
                -FailureMessage 'Checking out the pinned Python reference commit failed'
        }
    }
    else {
        $dirty = @(Invoke-CapturedNativeCommand `
            -FilePath $gitCommand.Source `
            -ArgumentList @('-C', $UpstreamPath, 'status', '--porcelain', '--untracked-files=normal') `
            -FailureMessage 'Checking the upstream worktree failed')
        if ($dirty.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace(($dirty -join ''))) {
            throw "The selected upstream checkout has local changes. The oracle will not overwrite them: '$UpstreamPath'."
        }
    }

    $currentCommit = [string] (@(Invoke-CapturedNativeCommand `
        -FilePath $gitCommand.Source `
        -ArgumentList @('-C', $UpstreamPath, 'rev-parse', 'HEAD') `
        -FailureMessage 'Reading the upstream commit failed')[-1])
    if (-not $newCheckout -and -not $currentCommit.Equals($upstreamCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (-not $manageUpstream) {
            throw "Explicit upstream checkout is at $currentCommit; expected $upstreamCommit."
        }

        if ($PSCmdlet.ShouldProcess($UpstreamPath, "Fetch and check out pinned upstream commit $upstreamCommit")) {
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'fetch', '--depth', '1', 'origin', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-fetch.log') `
                -FailureMessage 'Fetching the pinned Python reference commit failed'
            Invoke-LoggedNativeCommand `
                -FilePath $gitCommand.Source `
                -ArgumentList @('-C', $UpstreamPath, 'checkout', '--detach', $upstreamCommit) `
                -LogPath (Join-Path $logsRoot 'upstream-checkout.log') `
                -FailureMessage 'Checking out the pinned Python reference commit failed'
        }
    }

    if (-not $WhatIfPreference) {
        $verifiedCommit = [string] (@(Invoke-CapturedNativeCommand `
            -FilePath $gitCommand.Source `
            -ArgumentList @('-C', $UpstreamPath, 'rev-parse', 'HEAD') `
            -FailureMessage 'Verifying the upstream commit failed')[-1])
        if (-not $verifiedCommit.Equals($upstreamCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Pinned upstream checkout verification failed: expected $upstreamCommit, found $verifiedCommit."
        }
        if (-not (Test-UpstreamCheckoutAuthority `
            -Path $UpstreamPath `
            -Commit $upstreamCommit `
            -Repository $upstreamRepository)) {
            throw "Pinned upstream checkout is not byte-exact and free of extra files: '$UpstreamPath'."
        }
    }
}

function Install-ReferenceDependencies {
    $requirementsSha256 = Get-Sha256 -Path $requirementsPath
    $dependencyReady = $false
    if (-not $RefreshDependencies -and
        (Test-Path -LiteralPath $dependencyRoot -PathType Container) -and
        (Test-Path -LiteralPath $dependencyStamp -PathType Leaf)) {
        try {
            $stamp = Get-Content -LiteralPath $dependencyStamp -Raw | ConvertFrom-Json
            $dependencyReady = `
                ([string] $stamp.pythonVersion -eq $requiredPythonVersion) -and `
                ([string] $stamp.requirementsSha256 -eq $requirementsSha256) -and `
                ([string] $stamp.pipWheelSha256 -eq $pipWheelSha256)
        }
        catch {
            $dependencyReady = $false
        }
    }

    if ($dependencyReady) {
        Write-Host "Python reference dependencies: ready ($dependencyRoot)"
        return
    }

    $stagingRoot = Join-Path $dependencyParent 'site-packages.staging'
    Reset-ReferenceOwnedTree -Path $stagingRoot -AllowedTopLevelNames @('.tools')
    Ensure-Directory -Path $stagingRoot
    Ensure-Directory -Path (Split-Path -Parent $pipWheel)
    Ensure-Directory -Path $pipCache

    $downloadPip = -not (Test-Path -LiteralPath $pipWheel -PathType Leaf)
    if (-not $downloadPip) {
        $downloadPip = (Get-Sha256 -Path $pipWheel) -ne $pipWheelSha256
    }
    if ($downloadPip) {
        if ($PSCmdlet.ShouldProcess($pipWheel, 'Download the pinned bootstrap pip wheel')) {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri $pipWheelUri -OutFile $pipWheel
        }
    }

    if ($WhatIfPreference) {
        Write-Host "What if: verify pip wheel and install pinned requirements into '$dependencyRoot'."
        return
    }

    $actualPipHash = Get-Sha256 -Path $pipWheel
    if ($actualPipHash -ne $pipWheelSha256) {
        throw "Bootstrap pip wheel SHA-256 mismatch. Expected $pipWheelSha256; got $actualPipHash."
    }

    $pipCode = @'
import sys
sys.path.insert(0, sys.argv[1])
from pip._internal.cli.main import main
raise SystemExit(main(sys.argv[2:]))
'@
    $pipArguments = @(
        '-X', 'utf8',
        '-c', $pipCode,
        $pipWheel,
        'install',
        '--disable-pip-version-check',
        '--no-input',
        '--no-deps',
        '--requirement', $requirementsPath,
        '--target', $stagingRoot,
        '--cache-dir', $pipCache
    )
    Invoke-LoggedNativeCommand `
        -FilePath $pythonExecutable `
        -ArgumentList $pipArguments `
        -LogPath (Join-Path $logsRoot 'python-dependencies.log') `
        -FailureMessage 'Installing Python reference dependencies failed'

    Reset-ReferenceOwnedTree -Path $dependencyRoot -AllowedTopLevelNames @('.tools')
    Move-Item -LiteralPath $stagingRoot -Destination $dependencyRoot
    $stamp = [ordered] @{
        schema = 'goniegonie.python-reference.dependencies.v1'
        pythonVersion = $requiredPythonVersion
        requirementsSha256 = $requirementsSha256
        pipVersion = '24.3.1'
        pipWheelSha256 = $pipWheelSha256
    }
    Write-Utf8JsonIfChanged -InputObject $stamp -Path $dependencyStamp -Depth 4
}

function Reset-OutputDirectory {
    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
        Ensure-Directory -Path $outputRoot
        return
    }

    Assert-NoReparsePoints -Path $outputRoot -AnchorPath $repositoryRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $outputRoot -Force)) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('temp')
        if ($WhatIfPreference) {
            Write-Host "What if: remove prior reference output '$safeItem'."
        }
        else {
            Remove-Item -LiteralPath $safeItem -Recurse -Force
        }
    }
}

function Get-TreeHashes {
    param([Parameter(Mandatory = $true)][string] $Root)

    $result = [ordered] @{}
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return $result
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName)) {
        $relative = $file.FullName.Substring(([System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')).Length).TrimStart('\', '/') -replace '\\', '/'
        $result[$relative] = Get-Sha256 -Path $file.FullName
    }
    return $result
}

function Assert-ReferenceMatchesBaseline {
    if (-not (Test-Path -LiteralPath $baselineRoot -PathType Container)) {
        throw "Reference baseline is missing: '$baselineRoot'. Generate and review it, then run 'dev.cmd reference -UpdateBaseline'."
    }

    $actual = Get-TreeHashes -Root $outputRoot
    $expected = Get-TreeHashes -Root $baselineRoot
    $actualKeys = @($actual.Keys)
    $expectedKeys = @($expected.Keys)
    $differences = New-Object System.Collections.Generic.List[string]

    foreach ($path in $expectedKeys) {
        if (-not $actual.Contains($path)) {
            $differences.Add("missing output: $path")
        }
        elseif ([string] $actual[$path] -ne [string] $expected[$path]) {
            $differences.Add("content differs: $path")
        }
    }
    foreach ($path in $actualKeys) {
        if (-not $expected.Contains($path)) {
            $differences.Add("unexpected output: $path")
        }
    }

    if ($differences.Count -gt 0) {
        throw "Python reference output differs from the reviewed baseline:`n - $($differences -join "`n - ")"
    }

    Write-Host "Reference baseline verified: $($actualKeys.Count) files match."
}

function Update-ReferenceBaseline {
    Ensure-Directory -Path $baselineRoot
    Assert-NoReparsePoints -Path $baselineRoot -AnchorPath $repositoryRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $baselineRoot -Force)) {
        $safeItem = Assert-RepositoryChildPath `
            -RepositoryRoot $repositoryRoot `
            -Path $item.FullName `
            -AllowedTopLevelNames @('fixtures')
        if ($WhatIfPreference) {
            Write-Host "What if: remove superseded baseline '$safeItem'."
        }
        else {
            Remove-Item -LiteralPath $safeItem -Recurse -Force
        }
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $outputRoot -File -Recurse)) {
        $relative = $file.FullName.Substring($outputRoot.Length).TrimStart('\', '/')
        $destination = Join-Path $baselineRoot $relative
        if ($PSCmdlet.ShouldProcess($destination, 'Copy reviewed Python reference output into the tracked baseline')) {
            Ensure-Directory -Path (Split-Path -Parent $destination)
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
    }
    Write-Host "Reference baseline updated: $baselineRoot"
}

Ensure-Directory -Path $referenceTempRoot
Ensure-Directory -Path $logsRoot
Initialize-UpstreamCheckout
Install-ReferenceDependencies
Reset-OutputDirectory

if ($WhatIfPreference) {
    Write-Host "What if: run the pinned Python profile schedule/core, utils core, common core, constants metadata/engineering, epsimple numeric constants, construction core, HVAC enum/base, other-systems, thermal-source, and supply-system, identifier conventions, model core/result, and shape core, dragon construction AirBoundary core, construction core, and to-IDF-object, dragon HVAC appenders/controllers, misc-systems core, photovoltaic/source-system/source-tower-core/supply-core/SupplyGroup core/to-IDF-object, dragon shape geometry/opening-adjacency core and shading-material/surface/Zone to-IDF-object, dragon shape Zone core, dragon model add-supply-system/assembly/conditioning/construction-defaults/projections/Terrain, launcher result-parser/runtime, Imugi IDD definitions, IDD, construction equality/hash, ScheduleType, DaySchedule core/metrics/operations, RuleSet core/operations, Schedule core/operations, profile residual, and reference generators into '$outputRoot'."
    exit 0
}

$env:PYTHONHASHSEED = '0'
$env:PYTHONUTF8 = '1'
$env:PYTHONDONTWRITEBYTECODE = '1'
$upstreamSource = Join-Path $UpstreamPath 'src'
$constructionEqualityTestArguments = @(
    '-B',
    '-X', 'utf8',
    '-m', 'unittest',
    'discover',
    '-s', (Split-Path -Parent $constructionEqualityTestPath),
    '-p', 'test_*.py',
    '-v'
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $constructionEqualityTestArguments `
    -LogPath (Join-Path $logsRoot 'python-reference-generator-tests.log') `
    -FailureMessage 'Python reference generator tests failed'

$profileOraclePath = Join-Path $outputRoot 'usage-profile-schedule-oracle.json'
$profileGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $profileGeneratorPath,
    '--',
    '--output', $profileOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $profileGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-usage-profile-reference.log') `
    -FailureMessage 'Generating the Python usage-profile reference oracle failed'

$usageProfileCoreOraclePath = Join-Path $outputRoot 'usage-profile-core-oracle.json'
$usageProfileCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $usageProfileCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $usageProfileCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $usageProfileCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-usage-profile-core-reference.log') `
    -FailureMessage 'Generating the Python usage-profile core reference oracle failed'

$utilsCoreOraclePath = Join-Path $outputRoot 'utils-core-oracle.json'
$utilsCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $utilsCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $utilsCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $utilsCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-utils-core-reference.log') `
    -FailureMessage 'Generating the Python utils core reference oracle failed'

$commonCoreOraclePath = Join-Path $outputRoot 'common-core-oracle.json'
$commonCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $commonCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $commonCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $commonCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-common-core-reference.log') `
    -FailureMessage 'Generating the Python common core reference oracle failed'

$constantsMetadataOraclePath = Join-Path $outputRoot 'constants-metadata-oracle.json'
$constantsMetadataGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $constantsMetadataGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $constantsMetadataOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $constantsMetadataGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-constants-metadata-reference.log') `
    -FailureMessage 'Generating the Python constants metadata oracle failed'

$constantsEngineeringOraclePath = Join-Path $outputRoot 'constants-engineering-oracle.json'
$constantsEngineeringGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $constantsEngineeringGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $constantsEngineeringOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $constantsEngineeringGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-constants-engineering-reference.log') `
    -FailureMessage 'Generating the Python constants engineering oracle failed'

$epsimpleConstantsNumericOraclePath = Join-Path $outputRoot 'epsimple-constants-numeric-oracle.json'
$epsimpleConstantsNumericGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleConstantsNumericGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleConstantsNumericOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleConstantsNumericGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-constants-numeric-reference.log') `
    -FailureMessage 'Generating the Python epsimple numeric-constants oracle failed'

$epsimpleConstructionCoreOraclePath = Join-Path $outputRoot 'epsimple-construction-core-oracle.json'
$epsimpleConstructionCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleConstructionCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleConstructionCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleConstructionCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-construction-core-reference.log') `
    -FailureMessage 'Generating the Python epsimple construction-core oracle failed'

$epsimpleHvacEnumsBaseOraclePath = Join-Path $outputRoot 'epsimple-hvac-enums-base-oracle.json'
$epsimpleHvacEnumsBaseGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleHvacEnumsBaseGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleHvacEnumsBaseOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleHvacEnumsBaseGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-hvac-enums-base-reference.log') `
    -FailureMessage 'Generating the Python epsimple HVAC enum/base oracle failed'

$epsimpleHvacOtherSystemsOraclePath = Join-Path $outputRoot 'epsimple-hvac-other-systems-oracle.json'
$epsimpleHvacOtherSystemsGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleHvacOtherSystemsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleHvacOtherSystemsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleHvacOtherSystemsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-hvac-other-systems-reference.log') `
    -FailureMessage 'Generating the Python epsimple HVAC other-systems oracle failed'

$epsimpleHvacThermalSourceOraclePath = Join-Path $outputRoot 'epsimple-hvac-thermal-source-oracle.json'
$epsimpleHvacThermalSourceGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleHvacThermalSourceGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleHvacThermalSourceOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleHvacThermalSourceGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-hvac-thermal-source-reference.log') `
    -FailureMessage 'Generating the Python epsimple HVAC thermal-source oracle failed'

$epsimpleHvacSupplySystemOraclePath = Join-Path $outputRoot 'epsimple-hvac-supply-system-oracle.json'
$epsimpleHvacSupplySystemGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleHvacSupplySystemGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleHvacSupplySystemOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleHvacSupplySystemGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-hvac-supply-system-reference.log') `
    -FailureMessage 'Generating the Python epsimple HVAC supply-system oracle failed'

$epsimpleIdentifierConventionsOraclePath = Join-Path $outputRoot 'epsimple-identifier-conventions-oracle.json'
$epsimpleIdentifierConventionsGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleIdentifierConventionsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleIdentifierConventionsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleIdentifierConventionsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-identifier-conventions-reference.log') `
    -FailureMessage 'Generating the Python epsimple identifier-conventions oracle failed'

$epsimpleModelCoreOraclePath = Join-Path $outputRoot 'epsimple-model-core-oracle.json'
$epsimpleModelCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleModelCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleModelCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleModelCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-model-core-reference.log') `
    -FailureMessage 'Generating the Python epsimple model-core oracle failed'

$epsimpleModelResultOraclePath = Join-Path $outputRoot 'epsimple-model-result-oracle.json'
$epsimpleModelResultGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleModelResultGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleModelResultOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleModelResultGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-model-result-reference.log') `
    -FailureMessage 'Generating the Python epsimple model-result oracle failed'

$epsimpleShapeCoreOraclePath = Join-Path $outputRoot 'epsimple-shape-core-oracle.json'
$epsimpleShapeCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $epsimpleShapeCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $epsimpleShapeCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $epsimpleShapeCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-epsimple-shape-core-reference.log') `
    -FailureMessage 'Generating the Python epsimple shape-core oracle failed'

$dragonConstructionAirBoundaryCoreOraclePath = Join-Path $outputRoot 'dragon-construction-air-boundary-core-oracle.json'
$dragonConstructionAirBoundaryCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonConstructionAirBoundaryCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonConstructionAirBoundaryCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonConstructionAirBoundaryCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-construction-air-boundary-core-reference.log') `
    -FailureMessage 'Generating the Python dragon construction AirBoundary core oracle failed'

$dragonConstructionCoreOraclePath = Join-Path $outputRoot 'dragon-construction-core-oracle.json'
$dragonConstructionCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonConstructionCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonConstructionCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonConstructionCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-construction-core-reference.log') `
    -FailureMessage 'Generating the Python dragon construction core oracle failed'

$dragonConstructionToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-construction-to-idf-object-oracle.json'
$dragonConstructionToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonConstructionToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonConstructionToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonConstructionToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-construction-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon construction-family to-IDF-object oracle failed'

$dragonHvacAppendersControllersOraclePath = Join-Path $outputRoot 'dragon-hvac-appenders-controllers-oracle.json'
$dragonHvacAppendersControllersGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacAppendersControllersGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacAppendersControllersOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacAppendersControllersGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-appenders-controllers-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC appenders/controllers oracle failed'

$dragonHvacMiscSystemsCoreOraclePath = Join-Path $outputRoot 'dragon-hvac-misc-systems-core-oracle.json'
$dragonHvacMiscSystemsCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacMiscSystemsCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacMiscSystemsCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacMiscSystemsCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-misc-systems-core-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC misc-systems core oracle failed'

$dragonHvacPhotovoltaicToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-hvac-photovoltaic-to-idf-object-oracle.json'
$dragonHvacPhotovoltaicToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacPhotovoltaicToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacPhotovoltaicToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacPhotovoltaicToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-photovoltaic-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC photovoltaic to-IDF-object oracle failed'

$dragonHvacSourceSystemToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-hvac-source-system-to-idf-object-oracle.json'
$dragonHvacSourceSystemToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacSourceSystemToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacSourceSystemToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacSourceSystemToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-source-system-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC source-system to-IDF-object oracle failed'

$dragonHvacSourceTowerCoreOraclePath = Join-Path $outputRoot 'dragon-hvac-source-tower-core-oracle.json'
$dragonHvacSourceTowerCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacSourceTowerCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacSourceTowerCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacSourceTowerCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-source-tower-core-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC source/tower core oracle failed'

$dragonHvacSupplyCoreOraclePath = Join-Path $outputRoot 'dragon-hvac-supply-core-oracle.json'
$dragonHvacSupplyCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacSupplyCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacSupplyCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacSupplyCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-supply-core-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC supply-core oracle failed'

$dragonHvacSupplyGroupCoreOraclePath = Join-Path $outputRoot 'dragon-hvac-supply-group-core-oracle.json'
$dragonHvacSupplyGroupCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacSupplyGroupCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacSupplyGroupCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacSupplyGroupCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-supply-group-core-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC SupplyGroup core oracle failed'

$dragonHvacSupplyGroupToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-hvac-supply-group-to-idf-object-oracle.json'
$dragonHvacSupplyGroupToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonHvacSupplyGroupToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonHvacSupplyGroupToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonHvacSupplyGroupToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-hvac-supply-group-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon HVAC SupplyGroup.to_idf_object oracle failed'

$dragonShapeGeometryCoreOraclePath = Join-Path $outputRoot 'dragon-shape-geometry-core-oracle.json'
$dragonShapeGeometryCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeGeometryCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeGeometryCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeGeometryCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-geometry-core-reference.log') `
    -FailureMessage 'Generating the Python dragon shape geometry core oracle failed'

$dragonShapeOpeningAdjacencyCoreOraclePath = Join-Path $outputRoot 'dragon-shape-opening-adjacency-core-oracle.json'
$dragonShapeOpeningAdjacencyCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeOpeningAdjacencyCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeOpeningAdjacencyCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeOpeningAdjacencyCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-opening-adjacency-core-reference.log') `
    -FailureMessage 'Generating the Python dragon shape opening-adjacency core oracle failed'

$dragonShapeShadingMaterialToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-shape-shading-material-to-idf-object-oracle.json'
$dragonShapeShadingMaterialToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeShadingMaterialToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeShadingMaterialToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeShadingMaterialToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-shading-material-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon shape shading-material to-IDF-object oracle failed'

$dragonShapeSurfaceToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-shape-surface-to-idf-object-oracle.json'
$dragonShapeSurfaceToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeSurfaceToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeSurfaceToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeSurfaceToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-surface-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon shape Surface.to_idf_object oracle failed'

$dragonShapeZoneCoreOraclePath = Join-Path $outputRoot 'dragon-shape-zone-core-oracle.json'
$dragonShapeZoneCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeZoneCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeZoneCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeZoneCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-zone-core-reference.log') `
    -FailureMessage 'Generating the Python dragon shape Zone core oracle failed'

$dragonShapeZoneToIdfObjectOraclePath = Join-Path $outputRoot 'dragon-shape-zone-to-idf-object-oracle.json'
$dragonShapeZoneToIdfObjectGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonShapeZoneToIdfObjectGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonShapeZoneToIdfObjectOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonShapeZoneToIdfObjectGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-shape-zone-to-idf-object-reference.log') `
    -FailureMessage 'Generating the Python dragon shape Zone.to_idf_object oracle failed'

$dragonModelAddSupplySystemOraclePath = Join-Path $outputRoot 'dragon-model-add-supply-system-oracle.json'
$dragonModelAddSupplySystemGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelAddSupplySystemGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelAddSupplySystemOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelAddSupplySystemGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-add-supply-system-reference.log') `
    -FailureMessage 'Generating the Python dragon model add-supply-system oracle failed'

$dragonModelAssemblyOraclePath = Join-Path $outputRoot 'dragon-model-assembly-oracle.json'
$dragonModelAssemblyGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelAssemblyGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelAssemblyOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelAssemblyGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-assembly-reference.log') `
    -FailureMessage 'Generating the Python dragon model assembly oracle failed'

$dragonModelClassOraclePath = Join-Path $outputRoot 'dragon-model-class-oracle.json'
$dragonModelClassGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelClassGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelClassOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelClassGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-class-reference.log') `
    -FailureMessage 'Generating the Python dragon model EnergyModel class oracle failed'

$dragonModelConditioningOraclePath = Join-Path $outputRoot 'dragon-model-conditioning-oracle.json'
$dragonModelConditioningGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelConditioningGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelConditioningOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelConditioningGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-conditioning-reference.log') `
    -FailureMessage 'Generating the Python dragon model conditioning oracle failed'

$dragonModelConstructionDefaultsOraclePath = Join-Path $outputRoot 'dragon-model-construction-defaults-oracle.json'
$dragonModelConstructionDefaultsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelConstructionDefaultsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelConstructionDefaultsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelConstructionDefaultsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-construction-defaults-reference.log') `
    -FailureMessage 'Generating the Python dragon model construction-defaults oracle failed'

$dragonModelProjectionsOraclePath = Join-Path $outputRoot 'dragon-model-projections-oracle.json'
$dragonModelProjectionsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelProjectionsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelProjectionsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelProjectionsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-projections-reference.log') `
    -FailureMessage 'Generating the Python dragon model projections oracle failed'

$dragonModelTerrainOraclePath = Join-Path $outputRoot 'dragon-model-terrain-oracle.json'
$dragonModelTerrainGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dragonModelTerrainGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dragonModelTerrainOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dragonModelTerrainGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-dragon-model-terrain-reference.log') `
    -FailureMessage 'Generating the Python dragon model Terrain oracle failed'

$launcherResultParserOraclePath = Join-Path $outputRoot 'launcher-result-parser-oracle.json'
$launcherResultParserGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $launcherResultParserGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $launcherResultParserOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $launcherResultParserGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-launcher-result-parser-reference.log') `
    -FailureMessage 'Generating the Python launcher result-parser oracle failed'

$launcherRuntimeOraclePath = Join-Path $outputRoot 'launcher-runtime-oracle.json'
$launcherRuntimeGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $launcherRuntimeGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $launcherRuntimeOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $launcherRuntimeGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-launcher-runtime-reference.log') `
    -FailureMessage 'Generating the Python launcher runtime oracle failed'

$imugiIddDefinitionsCoreOraclePath = Join-Path $outputRoot 'imugi-idd-definitions-core-oracle.json'
$imugiIddDefinitionsCoreGeneratorArguments = @(
    '-B',
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $imugiIddDefinitionsCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $imugiIddDefinitionsCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $imugiIddDefinitionsCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-imugi-idd-definitions-core-reference.log') `
    -FailureMessage 'Generating the Python Imugi IDD definitions core oracle failed'

$iddOraclePath = Join-Path $outputRoot 'idd-24.2.0.schema.json.gz'
$iddGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $iddGeneratorPath,
    '--',
    '--idd', $energyPlusIddPath,
    '--epjson-schema', $energyPlusEpJsonSchemaPath,
    '--output', $iddOraclePath,
    '--upstream-commit', $upstreamCommit,
    '--expected-sha256', $requiredEnergyPlusIddSha256,
    '--expected-epjson-sha256', $requiredEnergyPlusEpJsonSchemaSha256,
    '--expected-version', $requiredEnergyPlusVersion,
    '--expected-build', $requiredEnergyPlusBuild
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $iddGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-idd-reference.log') `
    -FailureMessage 'Generating the EnergyPlus IDD reference oracle failed'

$constructionEqualityOraclePath = Join-Path $outputRoot 'construction-equality-hash-oracle.json'
$constructionEqualityGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $constructionEqualityGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $constructionEqualityOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $constructionEqualityGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-construction-equality-reference.log') `
    -FailureMessage 'Generating the Python construction equality/hash oracle failed'

$scheduleTypeOraclePath = Join-Path $outputRoot 'schedule-type-oracle.json'
$scheduleTypeGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $scheduleTypeGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $scheduleTypeOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $scheduleTypeGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-schedule-type-reference.log') `
    -FailureMessage 'Generating the Python ScheduleType oracle failed'

$dayScheduleCoreOraclePath = Join-Path $outputRoot 'day-schedule-core-oracle.json'
$dayScheduleCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dayScheduleCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dayScheduleCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dayScheduleCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-day-schedule-core-reference.log') `
    -FailureMessage 'Generating the Python DaySchedule core oracle failed'

$dayScheduleMetricsOraclePath = Join-Path $outputRoot 'day-schedule-metrics-oracle.json'
$dayScheduleMetricsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dayScheduleMetricsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dayScheduleMetricsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dayScheduleMetricsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-day-schedule-metrics-reference.log') `
    -FailureMessage 'Generating the Python DaySchedule metrics oracle failed'

$dayScheduleOperationsOraclePath = Join-Path $outputRoot 'day-schedule-operations-oracle.json'
$dayScheduleOperationsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $dayScheduleOperationsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $dayScheduleOperationsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $dayScheduleOperationsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-day-schedule-operations-reference.log') `
    -FailureMessage 'Generating the Python DaySchedule operations oracle failed'

$ruleSetCoreOraclePath = Join-Path $outputRoot 'rule-set-core-oracle.json'
$ruleSetCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $ruleSetCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $ruleSetCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $ruleSetCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-rule-set-core-reference.log') `
    -FailureMessage 'Generating the Python RuleSet core oracle failed'

$ruleSetOperationsOraclePath = Join-Path $outputRoot 'rule-set-operations-oracle.json'
$ruleSetOperationsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $ruleSetOperationsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $ruleSetOperationsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $ruleSetOperationsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-rule-set-operations-reference.log') `
    -FailureMessage 'Generating the Python RuleSet operations oracle failed'

$scheduleCoreOraclePath = Join-Path $outputRoot 'schedule-core-oracle.json'
$scheduleCoreGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $scheduleCoreGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $scheduleCoreOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $scheduleCoreGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-schedule-core-reference.log') `
    -FailureMessage 'Generating the Python Schedule core oracle failed'

$scheduleOperationsOraclePath = Join-Path $outputRoot 'schedule-operations-oracle.json'
$scheduleOperationsGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $scheduleOperationsGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $scheduleOperationsOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $scheduleOperationsGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-schedule-operations-reference.log') `
    -FailureMessage 'Generating the Python Schedule operations oracle failed'

$profileResidualOraclePath = Join-Path $outputRoot 'profile-residual-oracle.json'
$profileResidualGeneratorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $profileResidualGeneratorPath,
    '--',
    '--inventory', $publicSymbolInventoryPath,
    '--output', $profileResidualOraclePath,
    '--upstream-commit', $upstreamCommit
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $profileResidualGeneratorArguments `
    -LogPath (Join-Path $logsRoot 'python-profile-residual-reference.log') `
    -FailureMessage 'Generating the Python profile residual oracle failed'

$generatorArguments = @(
    '-X', 'utf8',
    $bootstrapPath,
    '--dependency-root', $dependencyRoot,
    '--upstream-source', $upstreamSource,
    '--generator', $generatorPath,
    '--',
    '--repository-root', $repositoryRoot,
    '--upstream-root', $UpstreamPath,
    '--output', $outputRoot
)
Invoke-LoggedNativeCommand `
    -FilePath $pythonExecutable `
    -ArgumentList $generatorArguments `
    -LogPath (Join-Path $logsRoot 'python-reference.log') `
    -FailureMessage 'Generating the Python reference oracle failed'

if ($UpdateBaseline) {
    Update-ReferenceBaseline
}
if ($Mode -eq 'Verify') {
    Assert-ReferenceMatchesBaseline
}

Write-Host "Python reference output: $outputRoot"
$referenceMutex.ReleaseMutex()
$referenceMutex.Dispose()
