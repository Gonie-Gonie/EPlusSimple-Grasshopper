using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.InvisibleDragon.Construction;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Construction;

public sealed class AirBoundaryCoreOracleParityTests
{
    private const string FixturePath =
        "fixtures/reference/python-0.7.0/dragon-construction-air-boundary-core-oracle.json";
    private const int FixtureBytes = 97_758;
    private const string FixtureSha256 =
        "sha256:16ad4d6d7a90e39a233d742d336d801e612c214360a5c1ac4c6853aec9f7ec03";
    private const string CasesSha256 =
        "sha256:996e6d45dbc2265ef078b6668fbcba423249a100329714031d64e09b3de30abc";
    private const string AdjacentExclusionsSha256 =
        "sha256:663f44cc25e1c3914cb534eecc32faa896fcab90e507b4b5a92e1e711d029516";

    private const string GeneratorPath =
        "tools/python-reference/generate_dragon_construction_air_boundary_core_oracle.py";
    private const int GeneratorBytes = 47_009;
    private const string GeneratorSha256 =
        "sha256:bb28f9e0a4e68684e4b7752fb127fc3be942d5c35eb3d1a9982a311bc26b4618";
    private const string ValidatorPath =
        "tests/PythonReference/test_dragon_construction_air_boundary_core_oracle.py";
    private const int ValidatorBytes = 14_390;
    private const string ValidatorSha256 =
        "sha256:ddf3a82eea5c9e13b7b8caec23574b3d4bc391ec21058c04aded53c03d5a3b8b";

    private const string InventoryPath = "upstream/public-symbol-inventory.json";
    private const int InventoryBytes = 518_067;
    private const string InventoryFileSha256 =
        "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8";
    private const string InventoryContentSha256 =
        "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02";

    private const string UpstreamPath = "src/idragon/dragon/construction.py";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const int UpstreamBytes = 11_652;
    private const string UpstreamSourceSha256 =
        "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622";
    private const string UpstreamAstSha256 =
        "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a";

    private const string InterfacePath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/ISurfaceConstruction.cs";
    private const int InterfaceBytes = 216;
    private const string InterfaceSha256 =
        "sha256:d960c1de9896e2b634df27979713fb484030abfc79f14d38e68286041df3e6a7";
    private const string SimpleConstructionsPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/SimpleConstructions.cs";
    private const int SimpleConstructionsBytes = 2_019;
    private const string SimpleConstructionsSha256 =
        "sha256:a72caa2d2c70ea18bf080bf623837ef3a0c7869a4991a7977255b00021d9e762";
    private const string DomainGuardPath =
        "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Internal/DomainGuard.cs";
    private const int DomainGuardBytes = 2_413;
    private const string DomainGuardSha256 =
        "sha256:5bb42189e091fb4ed17f3e242a0e22c32b47b2242c2e6b9a43da46ecaa929ac4";

    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Construction.AirBoundaryCoreOracleParityTests.MatchesPinnedAirBoundaryCoreThroughTypedNativeRoutes";

    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly ArtifactPin[] NativeArtifacts =
    {
        new(InterfacePath, InterfaceBytes, InterfaceSha256),
        new(SimpleConstructionsPath, SimpleConstructionsBytes, SimpleConstructionsSha256),
        new(DomainGuardPath, DomainGuardBytes, DomainGuardSha256),
    };

    private static readonly CaseBinding[] Cases =
    {
        new(
            "AB01",
            "dragon-construction-air-boundary-core.ab01-default-explicit-and-zero",
            "sha256:6d669081f161a03e6cb3fbe7cb05b460ad0eb01d7b650ed873cc67c125701a40",
            "sha256:d69af84241275e45f06154097869a0600425d6839f01a8fbc3ef22682dcb79fd"),
        new(
            "AB02",
            "dragon-construction-air-boundary-core.ab02-permissive-name-and-ach-domain",
            "sha256:119666fe70177310f1e1c7498c8fce06823137262cef4ca1049360489014269f",
            "sha256:801065b4f9b00f2a99d23442fe8130cba241ef14523d76c6f4a6cc535c1005f7"),
        new(
            "AB03",
            "dragon-construction-air-boundary-core.ab03-mutable-aliased-state",
            "sha256:708073e6903d388d2593a07a8ed7ec3b85689c33912be2cc6a1a6bfddd6a7eba",
            "sha256:33b0e8338b7043516fe3259b1a6cb13e71e00792814b681420dea5c523ec823c"),
        new(
            "AB04",
            "dragon-construction-air-boundary-core.ab04-call-shape-and-error-timing",
            "sha256:6e0c48b3b860153cd266814cb31b2c1c0064af6f998d318b7c3efd1e903d9c52",
            "sha256:d28af605dfb7d12bda5799822a32df51a837c84054783037ff73d095999cbe8d"),
    };

    private static readonly TargetBinding[] Targets =
    {
        new(
            588,
            "AirBoundary",
            "class",
            "sha256:fd8f9bb9fcc8a5676f77b8abaffdb0d4fc33ac1d8cdc9e1a6803a6b94e85eb0a",
            "sha256:39d8dd0e571aa6335663f4a30f26a7d6bb19bada7423f6c07c35ef3164638afc",
            "sha256:bd863bc3e852b36fd85133650c3e35281bab274515287b85d902c6731caac0d4",
            "dragon-construction-air-boundary-core-588-fd8f9bb9",
            "permissive-mutable-python-air-boundary-state-fd8f9bb9",
            "Dragons.InvisibleDragon.Construction.AirBoundary sealed typed record",
            "Dragons.InvisibleDragon.Construction.AirBoundary"),
        new(
            589,
            "AirBoundary.__init__",
            "function",
            "sha256:a69bf7074e3d95dfd347a13b8e35462ad11f92c5d45db4d58ca4dc3f1d7a026f",
            "sha256:ca98c4037f22c953f8768718d1e5c516e8f2e54bef701c6018bdb1b8b476d1df",
            "sha256:ef4465f1a137910234a3f54a2a658e0260ca8feea32ff97764c831bea0f84095",
            "dragon-construction-air-boundary-core-589-a69bf707",
            "unchecked-python-air-boundary-construction-a69bf707",
            "AirBoundary(string name, double airChangesPerHour = 0.5) validated constructor",
            "Dragons.InvisibleDragon.Construction.AirBoundary.AirBoundary"),
    };

    private static readonly NativePin[] ExpectedNativePins =
    {
        new(17, "sha256:20ef2c27651d8396ba6a4a4a29debc12e76b02f9773756a1cd82e9d0e7a83cbb"),
        new(13, "sha256:8b052843cb51c6cf333313ac744b4d45cd75749126a45e702daa923af87bf302"),
        new(13, "sha256:b310d876e7ea57a8f856a8b5ad395b3e3050cf1055de13bc26109ee5ce1b9d0b"),
        new(12, "sha256:741e3156921fd813d5136810c71fafbe6609477d630fe94cc7638b667d018c24"),
    };

    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:aeca690d5e7596cad9a368b8812bf18f885eab9ec0ac5881d717ca7842f25b72",
        "sha256:d6fe3435a79549e9cba78b44d7d73603cf3164899924df872fdb7a0e356c82f3",
    };

    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:3f46341428e2f78124303174fcc2606fb21b3654f8b20dcf36aa7f85f0375868",
        "sha256:3189ed8bdc344e24d857162ffacfd9af9016ed3d416606491e0c0894cd4df415",
    };

    private static bool DiscoverPins => string.Equals(
        Environment.GetEnvironmentVariable("DRAGONS_DISCOVER_AIR_BOUNDARY_CORE_PINS"),
        "1",
        StringComparison.Ordinal);

    [Fact]
    public void MatchesPinnedAirBoundaryCoreThroughTypedNativeRoutes()
    {
        ValidatePinnedArtifactsAndNativeApi();
        using JsonDocument oracle = ReadPinnedOracle();
        ValidateOracle(oracle.RootElement);

        NativeObservation[] observations = Enumerable.Range(0, Cases.Length)
            .Select(ObserveNativeCase)
            .ToArray();
        Assert.Equal(Cases.Select(item => item.Scenario), observations.Select(item => item.Scenario));
        Assert.All(observations, item =>
        {
            Assert.NotEmpty(item.Facts);
            Assert.Equal(item.Facts.Length, item.Facts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(item.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        });

        object[] receipts = Targets.Select(target => CreateReceipt(target, observations)).ToArray();
        string[] receiptHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(receipt)))
            .ToArray();
        string[] collectorOutputHashes = receipts
            .Select(receipt => CanonicalSha256(JsonSerializer.SerializeToElement(new
            {
                cases = new[]
                {
                    new
                    {
                        output = receipt,
                        test_case = EvidenceTestCase,
                    },
                },
            })))
            .ToArray();

        if (DiscoverPins)
        {
            throw new Xunit.Sdk.XunitException(
                "AIR_BOUNDARY_CORE_NATIVE_PINS\n" + JsonSerializer.Serialize(new
                {
                    cases = observations.Select(item => new
                    {
                        item.Scenario,
                        fact_count = item.Facts.Length,
                        facts_sha256 = item.FactsSha256,
                        facts = item.Facts,
                    }),
                    receipts = Targets.Select((target, index) => new
                    {
                        target.Symbol,
                        target.AssertionId,
                        receipt_sha256 = receiptHashes[index],
                        collector_output_sha256 = collectorOutputHashes[index],
                    }),
                }, DiscoveryJsonOptions));
        }

        Assert.Equal(ExpectedNativePins.Length, observations.Length);
        for (int index = 0; index < observations.Length; index++)
        {
            Assert.Equal(ExpectedNativePins[index].FactCount, observations[index].Facts.Length);
            Assert.Equal(ExpectedNativePins[index].FactsSha256, observations[index].FactsSha256);
        }
        Assert.Equal(ExpectedReceiptHashes, receiptHashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);

        for (int index = 0; index < Targets.Length; index++)
        {
            JsonElement receipt = JsonSerializer.SerializeToElement(receipts[index]);
            ValidateReceipt(receipt, Targets[index], observations);
            TrustedEvidenceRecorder.Record(
                Targets[index].AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipts[index]);
        }

        Assert.Equal(2, Targets.Length);
        Assert.Equal(2, Targets.Select(item => item.AssertionId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, Targets.Select(item => item.AdaptationId).Distinct(StringComparer.Ordinal).Count());
    }

    private static void ValidatePinnedArtifactsAndNativeApi()
    {
        AssertPinnedArtifact(GeneratorPath, GeneratorBytes, GeneratorSha256);
        AssertPinnedArtifact(ValidatorPath, ValidatorBytes, ValidatorSha256);
        AssertPinnedArtifact(InventoryPath, InventoryBytes, InventoryFileSha256);
        foreach (ArtifactPin artifact in NativeArtifacts)
        {
            AssertPinnedArtifact(artifact.Path, artifact.Bytes, artifact.Sha256);
        }

        Type type = typeof(AirBoundary);
        Assert.Equal("Dragons.InvisibleDragon.Construction.AirBoundary", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsSealed);
        Assert.True(typeof(ISurfaceConstruction).IsAssignableFrom(type));
        ConstructorInfo constructor = RequiredConstructor();
        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("name", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("airChangesPerHour", parameters[1].Name);
        Assert.Equal(typeof(double), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.True(parameters[1].HasDefaultValue);
        Assert.Equal(0.5d, Assert.IsType<double>(parameters[1].DefaultValue));
        ValidateProperty(type, "Name", typeof(string));
        ValidateProperty(type, "AirChangesPerHour", typeof(double));
    }

    private static JsonDocument ReadPinnedOracle()
    {
        byte[] fixture = File.ReadAllBytes(FindRepositoryFile(FixturePath));
        Assert.Equal(FixtureBytes, fixture.Length);
        Assert.Equal(FixtureSha256, Sha256(fixture));
        Assert.Equal((byte)'\n', fixture[^1]);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(fixture), StringComparison.Ordinal);
        return JsonDocument.Parse(fixture);
    }

    private static void ValidateOracle(JsonElement root)
    {
        AssertUniqueKeysRecursive(root);
        AssertNoHostPaths(root);
        AssertNoUnsafeIdentity(root);
        AssertNoNonFiniteJsonNumbers(root);
        AssertKeys(root,
            "case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256",
            "runtime", "schema", "symbols", "target_receipts", "upstream");
        Assert.Equal(
            "dragons.python-reference.dragon-construction-air-boundary-core.v1",
            RequiredString(root, "schema"));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));

        JsonElement[] fixtureCases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, fixtureCases.Length);
        JsonElement factHashes = root.GetProperty("fact_sha256");
        JsonElement caseHashes = root.GetProperty("case_sha256");
        for (int index = 0; index < Cases.Length; index++)
        {
            CaseBinding expected = Cases[index];
            JsonElement item = fixtureCases[index];
            Assert.Equal(expected.CaseId, RequiredString(item, "id"));
            Assert.Equal(expected.Scenario, RequiredString(item, "scenario"));
            AssertStringArray(item.GetProperty("target_symbols"), "AirBoundary", "AirBoundary.__init__");
            Assert.Empty(item.GetProperty("context_symbols").EnumerateArray());
            JsonElement python = item.GetProperty("python");
            Assert.Equal("observed", RequiredString(python, "outcome"));
            Assert.Equal(expected.FactsSha256, RequiredString(python, "facts_sha256"));
            Assert.Equal(expected.FactsSha256, CanonicalSha256(python.GetProperty("facts")));
            Assert.Equal(expected.FactsSha256, RequiredString(factHashes, expected.CaseId));
            Assert.Equal(expected.CaseSha256, CanonicalSha256(item));
            Assert.Equal(expected.CaseSha256, RequiredString(caseHashes, expected.CaseId));
            Assert.Equal(expected.Scenario, RequiredString(python.GetProperty("facts"), "scenario"));
            JsonElement dotnet = item.GetProperty("expected_dotnet");
            Assert.Equal("adapted-as-pinned", RequiredString(dotnet, "outcome"));
            Assert.Equal(2, dotnet.GetProperty("adaptations").GetArrayLength());
        }

        ValidateSourceReceipts(root.GetProperty("target_receipts"));
        JsonElement[] descriptors = root.GetProperty("symbols").EnumerateArray().ToArray();
        Assert.Equal(Targets.Length, descriptors.Length);
        for (int index = 0; index < Targets.Length; index++)
        {
            ValidateSourceDescriptor(descriptors[index], Targets[index]);
        }

        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(4, contract.GetProperty("case_count").GetInt32());
        Assert.Equal(
            "proposed-not-yet-cross-language-verified",
            RequiredString(contract, "native_binding_status"));
        JsonElement evidenceContract = contract.GetProperty("evidence_contract");
        Assert.False(evidenceContract.GetProperty("structural_only").GetBoolean());
        Assert.False(evidenceContract.GetProperty("full_idf_closure").GetBoolean());
        Assert.Equal(2, evidenceContract.GetProperty("expected_receipt_count").GetInt32());
        JsonElement closure = contract.GetProperty("closure");
        Assert.True(closure.GetProperty("target_coverage_complete").GetBoolean());
        Assert.False(closure.GetProperty("full_symbol_closure").GetBoolean());
        Assert.False(closure.GetProperty("full_construction_family_closure").GetBoolean());
        Assert.Equal(51, closure.GetProperty("adjacent_exclusions").GetArrayLength());
        Assert.Equal(8, closure.GetProperty("unresolved_boundaries").GetArrayLength());

        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal(UpstreamCommit, RequiredString(upstream, "commit"));
        Assert.Equal(InventoryContentSha256, RequiredString(upstream, "inventory_sha256"));
        JsonElement source = upstream.GetProperty("construction_source");
        Assert.Equal(UpstreamPath, RequiredString(source, "path"));
        Assert.Equal(UpstreamBytes, source.GetProperty("bytes").GetInt32());
        Assert.Equal(UpstreamSourceSha256, RequiredString(source, "source_sha256"));
        Assert.Equal(UpstreamAstSha256, RequiredString(source, "ast_sha256"));
        JsonElement inventoryFile = upstream.GetProperty("inventory_file");
        Assert.Equal(InventoryBytes, inventoryFile.GetProperty("bytes").GetInt32());
        Assert.Equal(InventoryFileSha256, RequiredString(inventoryFile, "file_sha256"));
        JsonElement exclusions = upstream.GetProperty("adjacent_exclusions");
        Assert.Equal(AdjacentExclusionsSha256, CanonicalSha256(exclusions));
        JsonElement[] exclusionItems = exclusions.EnumerateArray().ToArray();
        Assert.Equal(51, exclusionItems.Length);
        Assert.Equal(Enumerable.Range(590, 51),
            exclusionItems.Select(item => item.GetProperty("inventory_index").GetInt32()));
        Assert.Equal("AirBoundary.__repr__", RequiredString(exclusionItems[0], "symbol"));
        Assert.Equal("AirBoundary.__str__", RequiredString(exclusionItems[1], "symbol"));
        Assert.Equal("AirBoundary.to_idf_object", RequiredString(exclusionItems[2], "symbol"));
        Assert.Equal("Construction", RequiredString(exclusionItems[3], "symbol"));
        Assert.Equal("NoMassConstruction.to_idf_object", RequiredString(exclusionItems[^1], "symbol"));
        Assert.All(exclusionItems, item => Assert.Equal(UpstreamPath, RequiredString(item, "path")));
    }

    private static NativeObservation ObserveNativeCase(int index)
    {
        string[] facts = index switch
        {
            0 => ObserveAb01(),
            1 => ObserveAb02(),
            2 => ObserveAb03(),
            3 => ObserveAb04(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return new NativeObservation(
            Cases[index].Scenario,
            facts,
            CanonicalSha256(JsonSerializer.SerializeToElement(facts)));
    }

    private static string[] ObserveAb01()
    {
        Type type = typeof(AirBoundary);
        ConstructorInfo constructor = RequiredConstructor();
        ParameterInfo[] parameters = constructor.GetParameters();
        PropertyInfo nameProperty = RequiredProperty(type, "Name");
        PropertyInfo achProperty = RequiredProperty(type, "AirChangesPerHour");
        var defaultValue = new AirBoundary("Default");
        var explicitValue = new AirBoundary("Explicit", 1.25);
        var zeroValue = new AirBoundary("Zero", 0);
        var secondDefault = new AirBoundary("Default");
        Assert.NotSame(defaultValue, secondDefault);
        return new[]
        {
            "type.full_name=" + type.FullName,
            "type.is_class=" + Lower(type.IsClass),
            "type.is_sealed=" + Lower(type.IsSealed),
            "type.is_value_type=" + Lower(type.IsValueType),
            "type.implements_ISurfaceConstruction=" + Lower(typeof(ISurfaceConstruction).IsAssignableFrom(type)),
            "constructor.public_count=" + type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length,
            "constructor.parameter_count=" + parameters.Length,
            "constructor.parameter0=" + ParameterFact(parameters[0]),
            "constructor.parameter1=" + ParameterFact(parameters[1]),
            "property.Name=" + PropertyFact(nameProperty),
            "property.AirChangesPerHour=" + PropertyFact(achProperty),
            "default.state=" + StateFact(defaultValue),
            "explicit.state=" + StateFact(explicitValue),
            "zero.state=" + StateFact(zeroValue),
            "two_default_instances.reference_same=" + Lower(ReferenceEquals(defaultValue, secondDefault)),
            "two_default_instances.first_state=" + StateFact(defaultValue),
            "two_default_instances.second_state=" + StateFact(secondDefault),
        };
    }

    private static string[] ObserveAb02()
    {
        var baseline = new AirBoundary("Baseline", 0.5);
        string before = StateFact(baseline);
        var padded = new AirBoundary("  padded  ", 0.5);
        string nullName = CaptureConstruction(() => new AirBoundary(null!, 0.5));
        string blankName = CaptureConstruction(() => new AirBoundary(string.Empty, 0.5));
        string whitespaceName = CaptureConstruction(() => new AirBoundary("   ", 0.5));
        string negative = CaptureConstruction(() => new AirBoundary("negative", -1));
        string nan = CaptureConstruction(() => new AirBoundary("nan", double.NaN));
        string positiveInfinity = CaptureConstruction(
            () => new AirBoundary("positive-infinity", double.PositiveInfinity));
        string negativeInfinity = CaptureConstruction(
            () => new AirBoundary("negative-infinity", double.NegativeInfinity));
        string after = StateFact(baseline);
        Assert.Equal(before, after);
        return new[]
        {
            "source_state.before=" + before,
            "name.padded.state=" + StateFact(padded),
            "name.null=" + nullName,
            "name.blank=" + blankName,
            "name.whitespace=" + whitespaceName,
            "ach.negative=" + negative,
            "ach.nan=" + nan,
            "ach.positive_infinity=" + positiveInfinity,
            "ach.negative_infinity=" + negativeInfinity,
            "constructor.ach_parameter_type=" + TypeName(RequiredConstructor().GetParameters()[1].ParameterType),
            "constructor.ach_nullable_underlying=" +
                (Nullable.GetUnderlyingType(RequiredConstructor().GetParameters()[1].ParameterType)?.FullName ?? "<none>"),
            "constructor.name_parameter_type=" + TypeName(RequiredConstructor().GetParameters()[0].ParameterType),
            "source_state.after=" + after,
        };
    }

    private static string[] ObserveAb03()
    {
        PropertyInfo nameProperty = RequiredProperty(typeof(AirBoundary), "Name");
        PropertyInfo achProperty = RequiredProperty(typeof(AirBoundary), "AirChangesPerHour");
        var first = new AirBoundary("Stable", 0.5);
        var second = new AirBoundary("Stable", 0.5);
        string before = StateFact(first);
        string failure = CaptureConstruction(() => new AirBoundary("rejected", -0.01));
        string after = StateFact(first);
        Assert.NotSame(first, second);
        Assert.Equal(before, after);
        return new[]
        {
            "property.Name.can_read=" + Lower(nameProperty.CanRead),
            "property.Name.can_write=" + Lower(nameProperty.CanWrite),
            "property.Name.setter=" + (nameProperty.SetMethod?.Name ?? "<none>"),
            "property.Name.type=" + TypeName(nameProperty.PropertyType),
            "property.AirChangesPerHour.can_read=" + Lower(achProperty.CanRead),
            "property.AirChangesPerHour.can_write=" + Lower(achProperty.CanWrite),
            "property.AirChangesPerHour.setter=" + (achProperty.SetMethod?.Name ?? "<none>"),
            "property.AirChangesPerHour.type=" + TypeName(achProperty.PropertyType),
            "two_instances.reference_same=" + Lower(ReferenceEquals(first, second)),
            "two_instances.first_state=" + before,
            "two_instances.second_state=" + StateFact(second),
            "rejected_separate_construction=" + failure,
            "first_state.after_rejected_separate_construction=" + after,
        };
    }

    private static string[] ObserveAb04()
    {
        ConstructorInfo constructor = RequiredConstructor();
        ParameterInfo[] parameters = constructor.GetParameters();
        double declaredDefault = Assert.IsType<double>(parameters[1].DefaultValue);
        var defaultNamed = new AirBoundary(name: "named-default");
        var explicitNamed = new AirBoundary(name: "named-explicit", airChangesPerHour: 1.5);
        return new[]
        {
            "constructor.calling_convention=" + constructor.CallingConvention,
            "constructor.is_public=" + Lower(constructor.IsPublic),
            "constructor.required_parameter_count=" + parameters.Count(item => !item.IsOptional),
            "constructor.optional_parameter_count=" + parameters.Count(item => item.IsOptional),
            "constructor.parameter_order=" + string.Join("|", parameters.Select(item => item.Name)),
            "constructor.parameter_types=" + string.Join("|", parameters.Select(item => TypeName(item.ParameterType))),
            "constructor.name.has_default=" + Lower(parameters[0].HasDefaultValue),
            "constructor.ach.has_default=" + Lower(parameters[1].HasDefaultValue),
            "constructor.ach.default_type=" + TypeName(declaredDefault.GetType()),
            "constructor.ach.default_value=" + Double(declaredDefault),
            "typed_named_default.state=" + StateFact(defaultNamed),
            "typed_named_explicit.state=" + StateFact(explicitNamed),
        };
    }

    private static object CreateReceipt(
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations) => new
    {
        assertion_id = target.AssertionId,
        adaptation_id = target.AdaptationId,
        classification = "exception",
        target_symbol = target.Symbol,
        native_target = target.NativeTarget,
        native_implementation = target.NativeImplementation,
        native_type_fqn = "Dragons.InvisibleDragon.Construction.AirBoundary",
        source_receipt = SourceReceiptObject(target),
        artifacts = new
        {
            fixture = Artifact(FixturePath, FixtureBytes, FixtureSha256),
            generator = Artifact(GeneratorPath, GeneratorBytes, GeneratorSha256),
            python_validator = Artifact(ValidatorPath, ValidatorBytes, ValidatorSha256),
            public_inventory = Artifact(InventoryPath, InventoryBytes, InventoryFileSha256),
            native_sources = NativeArtifacts
                .Select(item => Artifact(item.Path, item.Bytes, item.Sha256))
                .ToArray(),
        },
        case_coverage = Cases.Select(item => item.CaseId).ToArray(),
        observations = observations.Select((item, index) => new
        {
            case_id = Cases[index].CaseId,
            python_facts_sha256 = Cases[index].FactsSha256,
            native_fact_count = item.Facts.Length,
            native_facts_sha256 = item.FactsSha256,
            native_facts = item.Facts,
        }).ToArray(),
        verification = new
        {
            claims_active_load = false,
            exercised_load = "not_applicable",
            kind = "cross_language",
            native_route = "typed-public-constructor-with-reflection-limited-to-public-abi-metadata",
            structural_only = false,
        },
        scope = new
        {
            exact_target_count = 2,
            equivalent_target_count = 0,
            exception_target_count = 2,
            target_inventory_indices = "588-589",
            adjacent_inventory_indices_not_retargeted = "590-640",
            adjacent_exclusion_count = 51,
            adjacent_exclusions_sha256 = AdjacentExclusionsSha256,
            full_symbol_closure = false,
            full_construction_family_closure = false,
            full_idf_closure = false,
            excluded_air_boundary_symbols = new[]
            {
                "AirBoundary.__repr__",
                "AirBoundary.__str__",
                "AirBoundary.to_idf_object",
            },
            python_inputs_not_representable_by_the_typed_native_signature = new[]
            {
                "bool-name",
                "list-name",
                "dict-name-after-reassignment",
                "None-ACH",
                "bool-ACH",
                "string-ACH",
                "dict-ACH",
                "list-ACH-after-reassignment",
            },
            source_state_evidence =
                "native-scalar-property-state-captured-before-and-after-separate-rejected-construction;Python-alias-and-reassignment-snapshots-are-bounded-to-AB03",
            record_members_not_exercised = new[]
            {
                "Equals",
                "GetHashCode",
                "ToString",
                "copy-constructor",
                "clone",
                "deconstruction",
            },
            unresolved_behavior = new[]
            {
                "arbitrary-descriptors-proxies-and-conversion-hooks-not-observed",
                "subclass-metaclass-monkeypatch-and-manual-dunder-init-calls-not-observed",
                "decimal-fraction-complex-and-huge-integer-ach-values-not-observed",
                "unicode-whitespace-name-domains-beyond-the-bounded-ascii-cases-not-observed",
                "attribute-deletion-and-arbitrary-added-attributes-not-observed",
                "copy-pickle-serialization-and-reflection-bypass-not-observed",
                "concurrent-source-or-instance-mutation-not-observed",
                "representation-idf-emission-and-parent-construction-integration-not-observed",
            },
        },
        upstream = new
        {
            ast_sha256 = UpstreamAstSha256,
            commit = UpstreamCommit,
            inventory_file_bytes = InventoryBytes,
            inventory_file_sha256 = InventoryFileSha256,
            inventory_sha256 = InventoryContentSha256,
            source_bytes = UpstreamBytes,
            source_sha256 = UpstreamSourceSha256,
        },
    };

    private static void ValidateReceipt(
        JsonElement receipt,
        TargetBinding target,
        IReadOnlyList<NativeObservation> observations)
    {
        AssertUniqueKeysRecursive(receipt);
        AssertNoHostPaths(receipt);
        AssertNoUnsafeIdentity(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
        Assert.Equal(target.AssertionId, RequiredString(receipt, "assertion_id"));
        Assert.Equal(target.AdaptationId, RequiredString(receipt, "adaptation_id"));
        Assert.Equal("exception", RequiredString(receipt, "classification"));
        Assert.Equal(target.Symbol, RequiredString(receipt, "target_symbol"));
        Assert.Equal(target.NativeTarget, RequiredString(receipt, "native_target"));
        Assert.Equal(target.NativeImplementation, RequiredString(receipt, "native_implementation"));
        Assert.Equal(
            "Dragons.InvisibleDragon.Construction.AirBoundary",
            RequiredString(receipt, "native_type_fqn"));
        ValidateSourceReceipt(receipt.GetProperty("source_receipt"), target, includeIndex: true);
        AssertStringArray(receipt.GetProperty("case_coverage"), Cases.Select(item => item.CaseId).ToArray());
        JsonElement[] actual = receipt.GetProperty("observations").EnumerateArray().ToArray();
        Assert.Equal(Cases.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            Assert.Equal(Cases[index].CaseId, RequiredString(actual[index], "case_id"));
            Assert.Equal(Cases[index].FactsSha256, RequiredString(actual[index], "python_facts_sha256"));
            Assert.Equal(observations[index].Facts.Length,
                actual[index].GetProperty("native_fact_count").GetInt32());
            Assert.Equal(observations[index].FactsSha256,
                RequiredString(actual[index], "native_facts_sha256"));
            AssertStringArray(actual[index].GetProperty("native_facts"), observations[index].Facts);
        }
        JsonElement verification = receipt.GetProperty("verification");
        Assert.False(verification.GetProperty("structural_only").GetBoolean());
        Assert.False(verification.GetProperty("claims_active_load").GetBoolean());
        Assert.Equal("not_applicable", RequiredString(verification, "exercised_load"));
        Assert.Equal("cross_language", RequiredString(verification, "kind"));
        JsonElement scope = receipt.GetProperty("scope");
        Assert.False(scope.GetProperty("full_symbol_closure").GetBoolean());
        Assert.False(scope.GetProperty("full_construction_family_closure").GetBoolean());
        Assert.False(scope.GetProperty("full_idf_closure").GetBoolean());
        Assert.Equal(51, scope.GetProperty("adjacent_exclusion_count").GetInt32());
        Assert.Equal(AdjacentExclusionsSha256, RequiredString(scope, "adjacent_exclusions_sha256"));
        Assert.Equal(8, scope.GetProperty("unresolved_behavior").GetArrayLength());
    }

    private static ConstructorInfo RequiredConstructor()
    {
        ConstructorInfo[] constructors = typeof(AirBoundary)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Single(constructors);
        return constructors[0];
    }

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        Assert.IsAssignableFrom<PropertyInfo>(type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance));

    private static void ValidateProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo property = RequiredProperty(type, name);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
    }

    private static string ParameterFact(ParameterInfo parameter) => string.Join(
        ",",
        "name=" + parameter.Name,
        "type=" + TypeName(parameter.ParameterType),
        "optional=" + Lower(parameter.IsOptional),
        "has_default=" + Lower(parameter.HasDefaultValue),
        "default=" + (parameter.HasDefaultValue ? Scalar(parameter.DefaultValue) : "<none>"));

    private static string PropertyFact(PropertyInfo property) => string.Join(
        ",",
        "type=" + TypeName(property.PropertyType),
        "can_read=" + Lower(property.CanRead),
        "can_write=" + Lower(property.CanWrite),
        "setter=" + (property.SetMethod?.Name ?? "<none>"));

    private static string StateFact(AirBoundary value) =>
        "Name=" + value.Name + "|AirChangesPerHour=" + Double(value.AirChangesPerHour);

    private static string CaptureConstruction(Func<AirBoundary> action)
    {
        try
        {
            _ = action();
            return "returned";
        }
        catch (ArgumentException error)
        {
            return error.GetType().Name + "(param=" + (error.ParamName ?? "<none>") + ")";
        }
    }

    private static string TypeName(Type type) => type.FullName ?? type.Name;

    private static string Scalar(object? value) => value switch
    {
        null => "<null>",
        double number => Double(number),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>",
    };

    private static string Double(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Lower(bool value) => value ? "true" : "false";

    private static object SourceReceiptObject(TargetBinding target) => new
    {
        body_hash = target.BodyHash,
        inventory_index = target.InventoryIndex,
        kind = target.Kind,
        path = UpstreamPath,
        signature_hash = target.SignatureHash,
        symbol = target.Symbol,
        symbol_hash = target.SymbolHash,
    };

    private static void ValidateSourceReceipts(JsonElement value)
    {
        JsonElement[] actual = value.EnumerateArray().ToArray();
        Assert.Equal(Targets.Length, actual.Length);
        for (int index = 0; index < Targets.Length; index++)
        {
            ValidateSourceReceipt(actual[index], Targets[index], includeIndex: true);
        }
    }

    private static void ValidateSourceDescriptor(JsonElement value, TargetBinding target) =>
        ValidateSourceReceipt(value, target, includeIndex: false);

    private static void ValidateSourceReceipt(
        JsonElement value,
        TargetBinding target,
        bool includeIndex)
    {
        AssertKeys(value, includeIndex
            ? new[] { "body_hash", "inventory_index", "kind", "path", "signature_hash", "symbol", "symbol_hash" }
            : new[] { "body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash" });
        if (includeIndex)
        {
            Assert.Equal(target.InventoryIndex, value.GetProperty("inventory_index").GetInt32());
        }
        Assert.Equal(target.Kind, RequiredString(value, "kind"));
        Assert.Equal(UpstreamPath, RequiredString(value, "path"));
        Assert.Equal(target.Symbol, RequiredString(value, "symbol"));
        Assert.Equal(target.SymbolHash, RequiredString(value, "symbol_hash"));
        Assert.Equal(target.SignatureHash, RequiredString(value, "signature_hash"));
        Assert.Equal(target.BodyHash, RequiredString(value, "body_hash"));
    }

    private static object Artifact(string path, int bytes, string sha256) => new
    {
        bytes,
        path,
        sha256,
    };

    private static void AssertPinnedArtifact(string path, int bytes, string sha256)
    {
        byte[] content = File.ReadAllBytes(FindRepositoryFile(path));
        Assert.Equal(bytes, content.Length);
        Assert.Equal(sha256, Sha256(content));
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

    private static void AssertUniqueKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.True(names.Add(property.Name), $"Duplicate JSON key '{property.Name}'.");
                AssertUniqueKeysRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertUniqueKeysRecursive(item);
            }
        }
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        string[] actual = value.EnumerateObject().Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    private static string RequiredString(JsonElement value, string property)
    {
        string? result = value.GetProperty(property).GetString();
        Assert.False(string.IsNullOrEmpty(result));
        return result!;
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        foreach (string text in EnumerateStrings(value))
        {
            Assert.DoesNotMatch("^[A-Za-z]:[\\\\/]", text);
            Assert.False(text.StartsWith('/'), text);
            Assert.DoesNotContain("\\\\", text, StringComparison.Ordinal);
        }
    }

    private static void AssertNoUnsafeIdentity(JsonElement value)
    {
        foreach (string text in EnumerateStrings(value))
        {
            Assert.DoesNotMatch("(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])", text);
            Assert.DoesNotMatch("(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])", text);
            Assert.DoesNotMatch("(?<!\\d)\\d{4}-\\d{2}-\\d{2}[T ][0-2]\\d:[0-5]\\d:[0-5]\\d", text);
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.True(double.IsFinite(number));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoNonFiniteJsonNumbers(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoNonFiniteJsonNumbers(item);
            }
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString()!;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                foreach (string text in EnumerateStrings(property.Value))
                {
                    yield return text;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                foreach (string text in EnumerateStrings(item))
                {
                    yield return text;
                }
            }
        }
    }

    private static string Sha256(byte[] value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string CanonicalSha256(JsonElement value) =>
        Sha256(Encoding.UTF8.GetBytes(CanonicalJson(value)));

    private static string CanonicalJson(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            WriteCanonical(writer, value);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON kind {value.ValueKind}.");
        }
        writer.Flush();
    }

    private sealed record ArtifactPin(string Path, int Bytes, string Sha256);

    private sealed record CaseBinding(
        string Scenario,
        string CaseId,
        string FactsSha256,
        string CaseSha256);

    private sealed record NativeObservation(
        string Scenario,
        string[] Facts,
        string FactsSha256);

    private sealed record NativePin(int FactCount, string FactsSha256);

    private sealed record TargetBinding(
        int InventoryIndex,
        string Symbol,
        string Kind,
        string SymbolHash,
        string SignatureHash,
        string BodyHash,
        string AssertionId,
        string AdaptationId,
        string NativeTarget,
        string NativeImplementation);
}
