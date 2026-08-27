using System.Security.Cryptography;
using System.Text.Json;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.UpstreamTracker;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;

namespace GonieGonie.InvisibleDragon.Tests;

public sealed class ConstructionTests
{
    private const string EqualityOracleRepositoryPath =
        "fixtures/reference/python-0.7.0/construction-equality-hash-oracle.json";
    private const string EqualityOracleSha256 =
        "sha256:6a1de9268675565ab6a14467717ac38799c45d9fdbba4230b4be403a9b79dbe7";
    private const string ConstructionTestOwner =
        "GonieGonie.InvisibleDragon.Tests.ConstructionTests";

    [Fact]
    public void MaterialPreservesValidatedThermophysicalProperties()
    {
        var material = new Material(
            "Brick",
            0.72,
            1920,
            840,
            0.88,
            0.65,
            0.61,
            MaterialRoughness.MediumRough);

        Assert.Equal("Brick", material.Name);
        Assert.Equal(0.72, material.ConductivityWattsPerMetreKelvin);
        Assert.Equal(1920, material.DensityKilogramsPerCubicMetre);
        Assert.Equal(840, material.SpecificHeatJoulesPerKilogramKelvin);
        Assert.Equal(MaterialRoughness.MediumRough, material.Roughness);
    }

    [Theory]
    [InlineData(0, 1000, 1000)]
    [InlineData(1, 0, 1000)]
    [InlineData(1, 1000, 99)]
    [InlineData(double.NaN, 1000, 1000)]
    public void MaterialRejectsInvalidPhysicalValues(double conductivity, double density, double specificHeat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Material("Bad", conductivity, density, specificHeat));
    }

    [Fact]
    public void MaterialEqualityMatchesPinnedInvisibleDragonProperties()
    {
        var oracleResult = LoadEqualityOracle();
        using JsonDocument oracle = oracleResult.Document;
        JsonElement materialOracle = RequiredEqualityOracleSymbol(
            oracle.RootElement,
            "Material.__eq__",
            "sha256:6ef680a2e300bcb56672f0d036de1b4aea3630cb90a635a3e45002ff8535dbf5");
        var first = new Material(
            "Brick",
            0.72,
            1920,
            840,
            thermalAbsorptance: 0.1,
            solarAbsorptance: 0.2,
            visibleAbsorptance: 0.3,
            roughness: MaterialRoughness.VeryRough);
        var samePinnedProperties = new Material(
            "Brick",
            0.72,
            1920,
            840,
            thermalAbsorptance: 0.9,
            solarAbsorptance: 0.8,
            visibleAbsorptance: 0.7,
            roughness: MaterialRoughness.Smooth);
        var renamed = new Material("Other", 0.72, 1920, 840);
        var changedConductivity = new Material("Brick", 0.73, 1920, 840);
        var changedDensity = new Material("Brick", 0.72, 1919, 840);
        var changedSpecificHeat = new Material("Brick", 0.72, 1920, 841);

        bool ignoredOpticalAndRoughnessEqual = first.Equals(samePinnedProperties);
        bool operatorEqual = first == samePinnedProperties;
        bool changedNameEqual = first.Equals(renamed);
        bool changedConductivityEqual = first.Equals(changedConductivity);
        bool changedDensityEqual = first.Equals(changedDensity);
        bool changedSpecificHeatEqual = first.Equals(changedSpecificHeat);
        bool unrelatedOperandEqual = first.Equals(new object());
        bool samePinnedPropertiesHashEqual =
            first.GetHashCode() == samePinnedProperties.GetHashCode();
        bool nullOperandEqual = first.Equals((Material?)null);

        Assert.Equal(
            RequiredEqualityCaseBoolean(
                materialOracle,
                "same-core-ignore-optical-and-roughness"),
            ignoredOpticalAndRoughnessEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(materialOracle, "different-name"),
            changedNameEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(materialOracle, "different-conductivity"),
            changedConductivityEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(materialOracle, "different-density"),
            changedDensityEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(materialOracle, "different-specific-heat"),
            changedSpecificHeatEqual);
        AssertNullOperandOracle(
            materialOracle,
            "TypeError",
            "Cannot compare Material with <class 'NoneType'>");
        Assert.True(ignoredOpticalAndRoughnessEqual);
        Assert.True(operatorEqual);
        Assert.True(samePinnedPropertiesHashEqual);
        Assert.False(changedNameEqual);
        Assert.False(changedConductivityEqual);
        Assert.False(changedDensityEqual);
        Assert.False(changedSpecificHeatEqual);
        Assert.False(nullOperandEqual);
        Assert.False(unrelatedOperandEqual);

        TrustedEvidenceRecorder.Record(
            "idragon-material-equality-native-null-adaptation",
            $"{ConstructionTestOwner}.MaterialEqualityMatchesPinnedInvisibleDragonProperties",
            "not_applicable",
            new
            {
                fixture = new
                {
                    path = EqualityOracleRepositoryPath,
                    sha256 = oracleResult.Sha256,
                },
                observations = new
                {
                    changed_conductivity_equal = changedConductivityEqual,
                    changed_density_equal = changedDensityEqual,
                    changed_name_equal = changedNameEqual,
                    changed_specific_heat_equal = changedSpecificHeatEqual,
                    dotnet_null_operand = new
                    {
                        outcome = "returned",
                        value = nullOperandEqual,
                    },
                    ignored_optical_and_roughness_equal = ignoredOpticalAndRoughnessEqual,
                    python_null_operand = materialOracle.GetProperty("null_operand").Clone(),
                },
                upstream_symbol = "Material.__eq__",
            });
    }

    [Fact]
    public void LayerCalculatesConductanceResistanceAndArealHeatCapacity()
    {
        var layer = new Layer("Concrete 200 mm", TestDomainFactory.Concrete(), 0.2);

        Assert.Equal(7, layer.UValue, 12);
        Assert.Equal(1d / 7, layer.ThermalResistance, 12);
        Assert.Equal(387200, layer.HeatCapacityJoulesPerSquareMetreKelvin, 8);
    }

    [Fact]
    public void LayerEqualityAndHashMatchPinnedInvisibleDragonBehavior()
    {
        var oracleResult = LoadEqualityOracle();
        using JsonDocument oracle = oracleResult.Document;
        JsonElement equalityOracle = RequiredEqualityOracleSymbol(
            oracle.RootElement,
            "Layer.__eq__",
            "sha256:b3fd4452af62f2d402279427187e70e6165f14be9a0b0543f8702dee39a473e6");
        JsonElement hashOracle = RequiredEqualityOracleSymbol(
            oracle.RootElement,
            "Layer.__hash__",
            "sha256:5994dd14a598a335d7945a1e39b59d93fd6bed9afbaff1308019b57bf22d0889");
        var first = new Layer(
            "Exterior concrete",
            new Material("Concrete", 1.4, 2300, 880, thermalAbsorptance: 0.1),
            0.2);
        var renamedWithIgnoredMaterialProperties = new Layer(
            "Interior concrete",
            new Material("Concrete", 1.4, 2300, 880, thermalAbsorptance: 0.9),
            0.2);
        var changedThickness = new Layer("Exterior concrete", first.Material, 0.21);
        var changedMaterial = new Layer(
            "Exterior concrete",
            new Material("Concrete", 1.5, 2300, 880),
            0.2);

        bool renamedLayerEqual = first.Equals(renamedWithIgnoredMaterialProperties);
        bool operatorEqual = first == renamedWithIgnoredMaterialProperties;
        bool changedThicknessEqual = first.Equals(changedThickness);
        bool changedMaterialEqual = first.Equals(changedMaterial);
        bool unrelatedOperandEqual = first.Equals(new object());
        int firstHash = first.GetHashCode();
        int changedThicknessHash = changedThickness.GetHashCode();
        int renamedHash = renamedWithIgnoredMaterialProperties.GetHashCode();
        bool dotnetHashUsesNameOnly =
            firstHash == StringComparer.Ordinal.GetHashCode(first.Name);
        bool sameNameChangedThicknessSameHash = firstHash == changedThicknessHash;
        bool renamedHashUsesNameOnly = renamedHash == StringComparer.Ordinal.GetHashCode(
            renamedWithIgnoredMaterialProperties.Name);
        bool differentNameObserved = !StringComparer.Ordinal.Equals(
            first.Name,
            renamedWithIgnoredMaterialProperties.Name);
        bool nullOperandEqual = first.Equals((Layer?)null);
        JsonElement hashDependency = hashOracle.GetProperty("hash_dependency");
        JsonElement hashChecks = hashDependency.GetProperty("checks");

        Assert.Equal(
            RequiredEqualityCaseBoolean(
                equalityOracle,
                "renamed-layer-same-material-and-thickness"),
            renamedLayerEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(equalityOracle, "different-thickness"),
            changedThicknessEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(equalityOracle, "different-material"),
            changedMaterialEqual);
        AssertNullOperandOracle(
            equalityOracle,
            "AttributeError",
            "'NoneType' object has no attribute 'material'");
        Assert.Equal(
            hashChecks.GetProperty("object_hash_equals_name_hash").GetBoolean(),
            dotnetHashUsesNameOnly);
        Assert.Equal(
            hashChecks.GetProperty("same_name_changed_thickness_same_hash").GetBoolean(),
            sameNameChangedThicknessSameHash);
        Assert.Equal(
            hashChecks.GetProperty("equal_different_name").GetBoolean(),
            renamedLayerEqual);
        Assert.True(
            hashChecks.GetProperty("equal_different_name_hashes_differ").GetBoolean());
        Assert.Equal(
            hashChecks.GetProperty("different_name_observed").GetBoolean(),
            differentNameObserved);
        Assert.True(renamedLayerEqual);
        Assert.True(operatorEqual);
        Assert.False(changedThicknessEqual);
        Assert.False(changedMaterialEqual);
        Assert.True(dotnetHashUsesNameOnly);
        Assert.True(sameNameChangedThicknessSameHash);
        Assert.True(renamedHashUsesNameOnly);
        Assert.False(nullOperandEqual);
        Assert.False(unrelatedOperandEqual);

        TrustedEvidenceRecorder.Record(
            "idragon-layer-equality-native-null-adaptation",
            $"{ConstructionTestOwner}.LayerEqualityAndHashMatchPinnedInvisibleDragonBehavior",
            "not_applicable",
            new
            {
                fixture = new
                {
                    path = EqualityOracleRepositoryPath,
                    sha256 = oracleResult.Sha256,
                },
                observations = new
                {
                    changed_material_equal = changedMaterialEqual,
                    changed_thickness_equal = changedThicknessEqual,
                    dotnet_null_operand = new
                    {
                        outcome = "returned",
                        value = nullOperandEqual,
                    },
                    python_null_operand = equalityOracle.GetProperty("null_operand").Clone(),
                    renamed_layer_equal = renamedLayerEqual,
                },
                upstream_symbol = "Layer.__eq__",
            });
        TrustedEvidenceRecorder.Record(
            "idragon-layer-hash-native-runtime-adaptation",
            $"{ConstructionTestOwner}.LayerEqualityAndHashMatchPinnedInvisibleDragonBehavior",
            "not_applicable",
            new
            {
                fixture = new
                {
                    path = EqualityOracleRepositoryPath,
                    sha256 = oracleResult.Sha256,
                },
                observations = new
                {
                    dotnet_equal_different_name = renamedLayerEqual,
                    dotnet_hash_uses_name_only = dotnetHashUsesNameOnly,
                    dotnet_renamed_hash_uses_name_only = renamedHashUsesNameOnly,
                    python_equal_different_name = hashChecks.GetProperty("equal_different_name").GetBoolean(),
                    python_equal_different_name_hashes_differ = hashChecks
                        .GetProperty("equal_different_name_hashes_differ")
                        .GetBoolean(),
                    python_object_hash_equals_name_hash = hashChecks
                        .GetProperty("object_hash_equals_name_hash")
                        .GetBoolean(),
                    python_same_name_changed_thickness_same_hash = hashChecks
                        .GetProperty("same_name_changed_thickness_same_hash")
                        .GetBoolean(),
                    same_name_changed_thickness_same_hash = sameNameChangedThicknessSameHash,
                },
                upstream_symbol = "Layer.__hash__",
            });
    }

    [Fact]
    public void ConstructionAggregatesLayersAndDefensivelyCopiesInput()
    {
        var concrete = new Layer("Concrete", TestDomainFactory.Concrete(), 0.2);
        var insulation = new Layer(
            "Insulation",
            new Material("Insulation", 0.04, 30, 1400),
            0.1);
        var source = new List<Layer> { concrete, insulation };

        var construction = new OpaqueConstruction("Exterior Wall", source);
        source.Clear();

        Assert.Equal(2, construction.Layers.Count);
        Assert.Equal(0.3, construction.ThicknessMetres, 12);
        Assert.Equal(1d / ((0.2 / 1.4) + (0.1 / 0.04)), construction.UValue, 12);
        Assert.Equal(
            concrete.HeatCapacityJoulesPerSquareMetreKelvin
                + insulation.HeatCapacityJoulesPerSquareMetreKelvin,
            construction.HeatCapacityJoulesPerSquareMetreKelvin,
            8);
    }

    [Fact]
    public void ConstructionUValueMatchesPinnedLayerConductanceOperationOrder()
    {
        var material = new Material("Witness", 0.03, 1, 100);
        var construction = new OpaqueConstruction(
            "Operation-order witness",
            new[]
            {
                new Layer("Thin", material, 0.001),
                new Layer("Thick", material, 0.01),
            });

        const long expectedUpstreamBits = 0x4005D1745D1745D2;
        const long directResistanceBits = 0x4005D1745D1745D1;

        Assert.Equal(expectedUpstreamBits, BitConverter.DoubleToInt64Bits(construction.UValue));
        Assert.Equal(
            directResistanceBits,
            BitConverter.DoubleToInt64Bits(1 / construction.ThermalResistance));
    }

    [Fact]
    public void ReversedConstructionPreservesPropertiesAndReversesLayerOrder()
    {
        var first = new Layer("First", TestDomainFactory.Concrete(), 0.1);
        var second = new Layer("Second", TestDomainFactory.Concrete("Other"), 0.2);
        var construction = new OpaqueConstruction("Wall", new[] { first, second });

        OpaqueConstruction reversed = construction.Reverse();

        Assert.Equal("Wall_reversed", reversed.Name);
        Assert.Equal(new[] { second, first }, reversed.Layers);
        Assert.Equal(construction.UValue, reversed.UValue, 12);
        Assert.Equal(construction.HeatCapacityJoulesPerSquareMetreKelvin, reversed.HeatCapacityJoulesPerSquareMetreKelvin, 8);
    }

    [Fact]
    public void ConstructionEqualityAndHashMatchPinnedInvisibleDragonBehavior()
    {
        var oracleResult = LoadEqualityOracle();
        using JsonDocument oracle = oracleResult.Document;
        JsonElement equalityOracle = RequiredEqualityOracleSymbol(
            oracle.RootElement,
            "Construction.__eq__",
            "sha256:8bf568b5f76ed813063ea04fd2eedf087e8f2525c2be9b9febbdb150a906b019");
        JsonElement hashOracle = RequiredEqualityOracleSymbol(
            oracle.RootElement,
            "Construction.__hash__",
            "sha256:5994dd14a598a335d7945a1e39b59d93fd6bed9afbaff1308019b57bf22d0889");
        var concrete = new Material("Concrete", 1.4, 2300, 880);
        var insulation = new Material("Insulation", 0.04, 30, 1400);
        var first = new OpaqueConstruction(
            "Wall",
            new[]
            {
                new Layer("Concrete outside", concrete, 0.2),
                new Layer("Insulation inside", insulation, 0.1),
            });
        var samePinnedProperties = new OpaqueConstruction(
            "Wall",
            new[]
            {
                new Layer("Renamed concrete", concrete, 0.2),
                new Layer("Renamed insulation", insulation, 0.1),
            });
        var renamed = new OpaqueConstruction("Other", samePinnedProperties.Layers);
        var reversed = new OpaqueConstruction("Wall", samePinnedProperties.Layers.Reverse());
        var fewerLayers = new OpaqueConstruction("Wall", samePinnedProperties.Layers.Take(1));

        bool sameOrderedLayersEqual = first.Equals(samePinnedProperties);
        bool renamedEqual = first.Equals(renamed);
        bool reversedLayersEqual = first.Equals(reversed);
        bool fewerLayersEqual = first.Equals(fewerLayers);
        bool unrelatedOperandEqual = first.Equals(new object());
        int firstHash = first.GetHashCode();
        int reversedHash = reversed.GetHashCode();
        int renamedHash = renamed.GetHashCode();
        bool dotnetHashUsesNameOnly = firstHash == StringComparer.Ordinal.GetHashCode(first.Name);
        bool sameNameReversedLayersSameHash = firstHash == reversedHash;
        bool renamedHashUsesNameOnly = renamedHash == StringComparer.Ordinal.GetHashCode(renamed.Name);
        bool nullOperandEqual = first.Equals((OpaqueConstruction?)null);
        JsonElement hashDependency = hashOracle.GetProperty("hash_dependency");
        JsonElement hashChecks = hashDependency.GetProperty("checks");

        Assert.Equal(
            RequiredEqualityCaseBoolean(
                equalityOracle,
                "same-name-same-ordered-layer-values"),
            sameOrderedLayersEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(equalityOracle, "different-name"),
            renamedEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(equalityOracle, "reversed-layer-order"),
            reversedLayersEqual);
        Assert.Equal(
            RequiredEqualityCaseBoolean(equalityOracle, "fewer-layers"),
            fewerLayersEqual);
        AssertNullOperandOracle(
            equalityOracle,
            "AttributeError",
            "'NoneType' object has no attribute 'name'");
        Assert.Equal(
            hashChecks.GetProperty("object_hash_equals_name_hash").GetBoolean(),
            dotnetHashUsesNameOnly);
        Assert.Equal(
            hashChecks.GetProperty("same_name_reversed_layers_same_hash").GetBoolean(),
            sameNameReversedLayersSameHash);
        Assert.True(sameOrderedLayersEqual);
        Assert.False(renamedEqual);
        Assert.False(reversedLayersEqual);
        Assert.False(fewerLayersEqual);
        Assert.True(dotnetHashUsesNameOnly);
        Assert.True(sameNameReversedLayersSameHash);
        Assert.True(renamedHashUsesNameOnly);
        Assert.False(nullOperandEqual);
        Assert.False(unrelatedOperandEqual);

        TrustedEvidenceRecorder.Record(
            "idragon-construction-equality-native-null-adaptation",
            $"{ConstructionTestOwner}.ConstructionEqualityAndHashMatchPinnedInvisibleDragonBehavior",
            "not_applicable",
            new
            {
                fixture = new
                {
                    path = EqualityOracleRepositoryPath,
                    sha256 = oracleResult.Sha256,
                },
                observations = new
                {
                    dotnet_null_operand = new
                    {
                        outcome = "returned",
                        value = nullOperandEqual,
                    },
                    fewer_layers_equal = fewerLayersEqual,
                    python_null_operand = equalityOracle.GetProperty("null_operand").Clone(),
                    renamed_equal = renamedEqual,
                    reversed_layers_equal = reversedLayersEqual,
                    same_ordered_layers_equal = sameOrderedLayersEqual,
                },
                upstream_symbol = "Construction.__eq__",
            });
        TrustedEvidenceRecorder.Record(
            "idragon-construction-hash-native-runtime-adaptation",
            $"{ConstructionTestOwner}.ConstructionEqualityAndHashMatchPinnedInvisibleDragonBehavior",
            "not_applicable",
            new
            {
                fixture = new
                {
                    path = EqualityOracleRepositoryPath,
                    sha256 = oracleResult.Sha256,
                },
                observations = new
                {
                    dotnet_hash_uses_name_only = dotnetHashUsesNameOnly,
                    dotnet_renamed_hash_uses_name_only = renamedHashUsesNameOnly,
                    python_object_hash_equals_name_hash = hashChecks
                        .GetProperty("object_hash_equals_name_hash")
                        .GetBoolean(),
                    python_same_name_reversed_layers_same_hash = hashChecks
                        .GetProperty("same_name_reversed_layers_same_hash")
                        .GetBoolean(),
                    same_name_reversed_layers_same_hash = sameNameReversedLayersSameHash,
                },
                upstream_symbol = "Construction.__hash__",
            });
    }

    [Fact]
    public void ConstructionRequiresAtLeastOneLayer()
    {
        Assert.Throws<ArgumentException>(() => new OpaqueConstruction("Empty", Array.Empty<Layer>()));
    }

    [Fact]
    public void SimpleConstructionsExposeDerivedValues()
    {
        var noMass = new NoMassConstruction("Partition", 2.5);
        var glazing = new Glazing("Double glazing", 1.2, 0.42);
        var air = new AirBoundary("Air wall", 0.5);

        Assert.Equal(0.4, noMass.ThermalResistance, 12);
        Assert.Equal(0.42, glazing.SolarHeatGainCoefficient);
        Assert.Equal(0.5, air.AirChangesPerHour);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void GlazingRejectsInvalidSolarHeatGainCoefficient(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Glazing("Bad", 1, value));
    }

    private static (JsonDocument Document, string Sha256) LoadEqualityOracle()
    {
        string path = FindRepositoryFile(EqualityOracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(EqualityOracleSha256, sha256);

        JsonDocument document = JsonDocument.Parse(bytes);
        try
        {
            JsonElement root = document.RootElement;
            Assert.Equal(
                "goniegonie.invisibledragon.construction-equality-hash-oracle.v1",
                root.GetProperty("schema").GetString());
            Assert.Equal(
                "847b01f68f438f560a986072bcaa7768fbf67897",
                root.GetProperty("upstream_commit").GetString());
            Assert.Equal(
                "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
                root.GetProperty("inventory_sha256").GetString());
            JsonElement source = root.GetProperty("source");
            Assert.Equal("src/idragon/dragon/construction.py", source.GetProperty("path").GetString());
            Assert.Equal(
                "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622",
                source.GetProperty("content_sha256").GetString());
            JsonElement runtime = root.GetProperty("runtime");
            Assert.Equal("3.12.7", runtime.GetProperty("python_version").GetString());
            Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
            Assert.Equal("siphash13", runtime.GetProperty("python_hash_algorithm").GetString());
            Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
            return (document, sha256);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static JsonElement RequiredEqualityOracleSymbol(
        JsonElement root,
        string symbol,
        string expectedHash)
    {
        JsonElement[] matches = root.GetProperty("symbols")
            .EnumerateArray()
            .Where(item => item.GetProperty("symbol").GetString() == symbol)
            .ToArray();
        JsonElement match = Assert.Single(matches);
        Assert.Equal(expectedHash, match.GetProperty("symbol_hash").GetString());
        return match;
    }

    private static bool RequiredEqualityCaseBoolean(JsonElement symbol, string caseId)
    {
        JsonElement[] matches = symbol.GetProperty("same_type_cases")
            .EnumerateArray()
            .Where(item => item.GetProperty("case").GetString() == caseId)
            .ToArray();
        return Assert.Single(matches).GetProperty("equal").GetBoolean();
    }

    private static void AssertNullOperandOracle(
        JsonElement symbol,
        string exceptionType,
        string exceptionMessage)
    {
        JsonElement nullOperand = symbol.GetProperty("null_operand");
        Assert.Equal("raised", nullOperand.GetProperty("outcome").GetString());
        Assert.Equal(exceptionType, nullOperand.GetProperty("exception_type").GetString());
        Assert.Equal(exceptionMessage, nullOperand.GetProperty("exception_message").GetString());
    }

    private static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}
