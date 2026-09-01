using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.InvisibleDragon.Idf;
using Dragons.InvisibleDragon.Model;
using Dragons.InvisibleDragon.Shape;
using Dragons.UpstreamTracker;

#pragma warning disable CA1861 // Closed oracle arrays are intentionally auditable in place.

namespace Dragons.InvisibleDragon.Tests.Model;

public sealed class TerrainOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/dragon-model-terrain-oracle.json";
    private const string OracleSha256 =
        "sha256:f371e7b98aebb4f5bb338185c32e804975801349a123c58aba4d78263658183e";
    private const string CasesSha256 =
        "sha256:aea20222894cc0c5a500dfccb15f9955e56666f4de763fef62c297fe975d0a47";
    private const int OracleByteLength = 17_936;
    private const int ExpectedCaseCount = 18;
    private const string OracleSchema =
        "dragons.python-reference.dragon-model-terrain.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Model.TerrainOracleParityTests.MatchesPinnedPythonTerrain";
    private const string UpstreamPath = "src/idragon/dragon/model.py";
    private const string NativeTypeName =
        "Dragons.InvisibleDragon.Model.Terrain";

    // These exact path/symbol/hash/assertion literals are consumed by the
    // trusted compatibility evidence collector.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("src/idragon/dragon/model.py", "Terrain", "sha256:c6163ac59051a6638838c9f9b2953585bf6825942dfa79b46af3be27279e5799", "dragon-model-terrain-c6163ac5"),
        new("src/idragon/dragon/model.py", "Terrain.CITY", "sha256:86bbbeccfdcac8147f1ea09090065c8567a1a910715d4679b1059b02a27839bc", "dragon-model-terrain-city-86bbbecc"),
        new("src/idragon/dragon/model.py", "Terrain.COUNTRY", "sha256:b5cce6c9c3dbcbe551d86663ed5d7b4615451b5b9841f0fd6c8ddc6c6a5b5eae", "dragon-model-terrain-country-b5cce6c9"),
        new("src/idragon/dragon/model.py", "Terrain.OCEAN", "sha256:4fb458afdad96d03018c848e08a853065cf2ff1f71d110175a13e18481c6b20a", "dragon-model-terrain-ocean-4fb458af"),
        new("src/idragon/dragon/model.py", "Terrain.SUBURBS", "sha256:3de90284fe1a6b5e8b582cd04c07cd01da2a3fc6d097bce30b2c3d23144167e6", "dragon-model-terrain-suburbs-3de90284"),
        new("src/idragon/dragon/model.py", "Terrain.URBAN", "sha256:a4c4bc7a7a67f1165956614348dde48e687d79001443487b879f5abf1cbf5a62", "dragon-model-terrain-urban-a4c4bc7"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("Terrain", "class", "sha256:1d1e2b681f443f98c601d67c7ad6574c3ab400169fba214018821be810b35a05", "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726", "exception", "native-typed-terrain-enum-valid-idf-token", NativeTypeName),
        new("Terrain.CITY", "constant", "sha256:8111cd1050752ea024674b02b1502d1fdab240d04147d65f4c8ad71f148f0791", "sha256:1dd88966c75717b665c6649618e6003073b9f4c6c767171d6adc097e23263394", "equivalent", null, NativeTypeName + ".City"),
        new("Terrain.COUNTRY", "constant", "sha256:cd58cf34472c886ee073d9c92cccd9a21ef585675a3aebbfac665ec8701fd93c", "sha256:20ae46499cfabff7e35ca4cda49b33ccfd5258adad3ceed6ae7feb05eaae3772", "equivalent", null, NativeTypeName + ".Country"),
        new("Terrain.OCEAN", "constant", "sha256:43f22f5af8b01a0e2ac6f0d4c47016cc200961a8b80b0228c0f7768076df9086", "sha256:49dab2386f677c04c24d008110220ae1ef2e02d84ce9a54de25a4c05e6e683d8", "equivalent", null, NativeTypeName + ".Ocean"),
        new("Terrain.SUBURBS", "constant", "sha256:201c53eabe683bbe1abea3efd17c21f4b74c585b63fd2d76ca2bb44878f99587", "sha256:6bece3a025b22ae5b104d63e066146295e565cfae57cf5fcc92e827ec2644291", "equivalent", null, NativeTypeName + ".Suburbs"),
        new("Terrain.URBAN", "constant", "sha256:69ca03abbb5e119dbba6122c1e9a4c0eb82beaeeae2abca2fd9c8ea80949c011", "sha256:84445019fc9c0fbb69f98f9b193728c3227aeabb9f1b19ca165d80f1e0250b30", "equivalent", null, NativeTypeName + ".Urban"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("dragon-model-terrain.enum.construction", "terrain-class", "Terrain"),
        new("dragon-model-terrain.enum.member-topology", "terrain-class", "Terrain"),
        new("dragon-model-terrain.enum.text-projection", "terrain-class", "Terrain"),
        new("dragon-model-terrain.member.city.engineering-token", "terrain-member", "Terrain.CITY"),
        new("dragon-model-terrain.member.city.roundtrip", "terrain-member", "Terrain.CITY"),
        new("dragon-model-terrain.member.city.value", "terrain-member", "Terrain.CITY"),
        new("dragon-model-terrain.member.country.engineering-token", "terrain-member", "Terrain.COUNTRY"),
        new("dragon-model-terrain.member.country.roundtrip", "terrain-member", "Terrain.COUNTRY"),
        new("dragon-model-terrain.member.country.value", "terrain-member", "Terrain.COUNTRY"),
        new("dragon-model-terrain.member.ocean.engineering-token", "terrain-member", "Terrain.OCEAN"),
        new("dragon-model-terrain.member.ocean.roundtrip", "terrain-member", "Terrain.OCEAN"),
        new("dragon-model-terrain.member.ocean.value", "terrain-member", "Terrain.OCEAN"),
        new("dragon-model-terrain.member.suburbs.engineering-token", "terrain-member", "Terrain.SUBURBS"),
        new("dragon-model-terrain.member.suburbs.roundtrip", "terrain-member", "Terrain.SUBURBS"),
        new("dragon-model-terrain.member.suburbs.value", "terrain-member", "Terrain.SUBURBS"),
        new("dragon-model-terrain.member.urban.engineering-token", "terrain-member", "Terrain.URBAN"),
        new("dragon-model-terrain.member.urban.roundtrip", "terrain-member", "Terrain.URBAN"),
        new("dragon-model-terrain.member.urban.value", "terrain-member", "Terrain.URBAN"),
    };

    [Fact]
    public void MatchesPinnedPythonTerrain()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = Sha256(bytes);
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement[] cases = ValidateCorpus(oracle.RootElement);
        var observations = new List<NativeObservation>(ExpectedCaseCount);
        for (int index = 0; index < cases.Length; index++)
        {
            CaseBinding binding = ExpectedCases[index];
            SymbolContract symbol = Assert.Single(
                ExpectedSymbols,
                candidate => candidate.Symbol == binding.Symbol);
            JsonElement pythonFacts = cases[index]
                .GetProperty("python")
                .GetProperty("facts");
            string[] nativeFacts = ExecuteCase(binding, pythonFacts);
            Assert.Equal(3, nativeFacts.Length);
            Assert.Equal(3, nativeFacts.Distinct(StringComparer.Ordinal).Count());
            Assert.All(nativeFacts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));

            JsonElement nativeFactsJson = JsonSerializer.SerializeToElement(nativeFacts);
            AssertNoRawAddresses(nativeFactsJson.GetRawText());
            AssertNoHostPaths(nativeFactsJson);
            AssertNoNonFiniteJsonNumbers(nativeFactsJson);
            observations.Add(new NativeObservation(
                binding.CaseId,
                binding.Symbol,
                symbol.AdaptationId,
                nativeFacts));
        }

        Assert.Equal(ExpectedCaseCount, observations.Count);
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            NativeObservation[] symbolObservations = observations
                .Where(item => item.Symbol == evidence.Symbol)
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(3, symbolObservations.Length);
            var receipt = new
            {
                fixture = new
                {
                    case_count = ExpectedCaseCount,
                    cases_sha256 = CasesSha256,
                    path = OracleRepositoryPath,
                    sha256,
                },
                observations = symbolObservations.Select(item => new
                {
                    adaptation_id = item.AdaptationId,
                    case_id = item.CaseId,
                    native_facts = item.NativeFacts,
                    native_outcome = "returned",
                }).ToArray(),
                upstream_path = evidence.Path,
                upstream_symbol = evidence.Symbol,
            };
            JsonElement receiptJson = JsonSerializer.SerializeToElement(receipt);
            ValidateReceipt(receiptJson, evidence, symbolObservations);
            TrustedEvidenceRecorder.Record(
                evidence.AssertionId,
                EvidenceTestCase,
                "not_applicable",
                receipt);
        }
    }

    private static JsonElement[] ValidateCorpus(JsonElement root)
    {
        AssertUniqueObjectKeysRecursive(root);
        AssertKeys(
            root,
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream");
        Assert.Equal(OracleSchema, RequiredString(root, "schema"));
        AssertNoRawAddresses(root.GetRawText());
        AssertNoHostPaths(root);
        AssertNoNonFiniteJsonNumbers(root);

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
            RequiredString(upstream, "source_sha256"));

        ValidateRuntime(root.GetProperty("runtime"));
        ValidateEvidenceBindings();
        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));
        ValidateNativeBindings();

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        string[] identifiers = cases.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(ExpectedCases.Select(item => item.CaseId), identifiers);
        Assert.Equal(identifiers.OrderBy(item => item, StringComparer.Ordinal), identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol).OrderBy(item => item, StringComparer.Ordinal),
            cases.Select(item => RequiredString(item, "symbol"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(
            cases.GroupBy(item => RequiredString(item, "symbol")),
            group => Assert.Equal(3, group.Count()));
        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateRuntime(JsonElement runtime)
    {
        AssertKeys(
            runtime,
            "dependencies",
            "implementation",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());

        JsonElement dependencies = runtime.GetProperty("dependencies");
        AssertKeys(
            dependencies,
            "colorama",
            "et_xmlfile",
            "numpy",
            "openpyxl",
            "pandas",
            "python-dateutil",
            "pytz",
            "six",
            "tqdm",
            "tzdata");
        Assert.Equal("0.4.6", RequiredString(dependencies, "colorama"));
        Assert.Equal("2.0.0", RequiredString(dependencies, "et_xmlfile"));
        Assert.Equal("2.3.1", RequiredString(dependencies, "numpy"));
        Assert.Equal("3.1.5", RequiredString(dependencies, "openpyxl"));
        Assert.Equal("2.3.0", RequiredString(dependencies, "pandas"));
        Assert.Equal("2.9.0.post0", RequiredString(dependencies, "python-dateutil"));
        Assert.Equal("2024.2", RequiredString(dependencies, "pytz"));
        Assert.Equal("1.16.0", RequiredString(dependencies, "six"));
        Assert.Equal("4.67.1", RequiredString(dependencies, "tqdm"));
        Assert.Equal("2024.2", RequiredString(dependencies, "tzdata"));
    }

    private static void ValidateEvidenceBindings()
    {
        Assert.Equal(ExpectedSymbols.Length, ExpectedEvidence.Length);
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol),
            ExpectedEvidence.Select(item => item.Symbol));
        Assert.Equal(
            ExpectedEvidence.Length,
            ExpectedEvidence.Select(item => item.AssertionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Equal(UpstreamPath, evidence.Path);
            Assert.Matches("^sha256:[0-9a-f]{64}$", evidence.SymbolHash);
            Assert.Matches("^dragon-model-terrain(?:-[a-z]+)?-[0-9a-f]{7,8}$", evidence.AssertionId);
            Assert.Equal(
                evidence.SymbolHash,
                Assert.Single(
                    ExpectedSymbols,
                    item => item.Symbol == evidence.Symbol).SymbolHash);
        }
    }

    private static void ValidateSymbols(JsonElement value)
    {
        JsonElement[] symbols = value.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < symbols.Length; index++)
        {
            JsonElement actual = symbols[index];
            SymbolContract expected = ExpectedSymbols[index];
            AssertKeys(
                actual,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(UpstreamPath, RequiredString(actual, "path"));
            Assert.Equal(expected.Symbol, RequiredString(actual, "symbol"));
            Assert.Equal(expected.Kind, RequiredString(actual, "kind"));
            Assert.Equal(expected.SignatureHash, RequiredString(actual, "signature_hash"));
            Assert.Equal(expected.BodyHash, RequiredString(actual, "body_hash"));
            Assert.Equal(expected.SymbolHash, RequiredString(actual, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement contract)
    {
        AssertKeys(
            contract,
            "adaptations",
            "assertion_ids",
            "case_count",
            "case_ids",
            "classifications",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, contract.GetProperty("case_count").GetInt32());
        AssertStringArray(
            contract.GetProperty("case_ids"),
            ExpectedCases.Select(item => item.CaseId).ToArray());
        AssertStringArray(
            contract.GetProperty("target_symbols"),
            ExpectedSymbols.Select(item => item.Symbol).ToArray());
        Assert.Equal(
            "pinned-python-only-no-native-type-name-claims",
            RequiredString(contract, "runtime_names"));

        JsonElement classifications = contract.GetProperty("classifications");
        AssertKeys(
            classifications,
            ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(
                symbol.Classification,
                RequiredString(classifications, symbol.Symbol));
        }
        Assert.Equal(
            1,
            ExpectedSymbols.Count(item => item.Classification == "exception"));
        Assert.Equal(
            5,
            ExpectedSymbols.Count(item => item.Classification == "equivalent"));

        JsonElement adaptations = contract.GetProperty("adaptations");
        AssertKeys(adaptations, "Terrain");
        Assert.Equal(
            "native-typed-terrain-enum-valid-idf-token",
            RequiredString(adaptations, "Terrain"));
        Assert.Single(ExpectedSymbols, item => item.AdaptationId is not null);

        JsonElement assertionIds = contract.GetProperty("assertion_ids");
        AssertKeys(
            assertionIds,
            ExpectedEvidence.Select(item => item.Symbol).ToArray());
        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            Assert.Equal(
                evidence.AssertionId,
                RequiredString(assertionIds, evidence.Symbol));
        }
    }

    private static void ValidateNativeBindings()
    {
        Type type = typeof(Terrain);
        Assert.Equal(NativeTypeName, type.FullName);
        Assert.True(type.IsPublic);
        Assert.True(type.IsEnum);
        Assert.True(type.IsSealed);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(type));

        MemberBinding[] members = NativeMembers();
        Assert.Equal(
            members.Select(item => item.NativeName),
            Enum.GetNames<Terrain>());
        Assert.Equal(
            members.Select(item => item.Value),
            Enum.GetValues<Terrain>());
        Assert.Equal(
            members.Select(item => item.Ordinal),
            Enum.GetValues<Terrain>().Select(item => (int)item));

        foreach (MemberBinding member in members)
        {
            SymbolContract contract = Assert.Single(
                ExpectedSymbols,
                item => item.Symbol == member.Symbol);
            Assert.Equal(NativeTypeName + "." + member.NativeName, contract.NativeTarget);
            FieldInfo field = type.GetField(
                member.NativeName,
                BindingFlags.Public | BindingFlags.Static)!;
            Assert.NotNull(field);
            Assert.Equal(type, field.FieldType);
            Assert.True(field.IsLiteral);
            Assert.False(field.IsInitOnly);
            Assert.Equal(member.Ordinal, (int)field.GetRawConstantValue()!);
        }

        ConstructorInfo constructor = Assert.Single(
            typeof(EnergyModel).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        ParameterInfo terrainParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == "terrain");
        Assert.Equal(typeof(Terrain), terrainParameter.ParameterType);
        Assert.True(terrainParameter.HasDefaultValue);
        Assert.Equal(Terrain.Suburbs, terrainParameter.DefaultValue);

        PropertyInfo property = typeof(EnergyModel).GetProperty(nameof(EnergyModel.Terrain))!;
        Assert.NotNull(property);
        Assert.Equal(typeof(Terrain), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.Null(property.SetMethod);
        Assert.NotNull(typeof(EnergyModel).GetMethod(
            nameof(EnergyModel.ToIdfDocument),
            BindingFlags.Public | BindingFlags.Instance));
    }

    private static void ValidateCase(JsonElement value, CaseBinding expected)
    {
        bool hasAdaptation = expected.Symbol == "Terrain";
        AssertKeys(
            value,
            hasAdaptation
                ? new[] { "executor", "expected_dotnet", "id", "python", "symbol" }
                : new[] { "executor", "id", "python", "symbol" });
        Assert.Equal(expected.CaseId, RequiredString(value, "id"));
        Assert.Equal(expected.Executor, RequiredString(value, "executor"));
        Assert.Equal(expected.Symbol, RequiredString(value, "symbol"));

        JsonElement python = value.GetProperty("python");
        AssertKeys(python, "facts", "outcome");
        Assert.Equal("returned", RequiredString(python, "outcome"));

        if (hasAdaptation)
        {
            JsonElement expectedDotnet = value.GetProperty("expected_dotnet");
            AssertKeys(expectedDotnet, "adaptation", "outcome");
            Assert.Equal(
                "native-typed-terrain-enum-valid-idf-token",
                RequiredString(expectedDotnet, "adaptation"));
            Assert.Equal("returned", RequiredString(expectedDotnet, "outcome"));
        }
        else
        {
            Assert.False(value.TryGetProperty("expected_dotnet", out _));
        }
    }

    private static string[] ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        ValidatePythonFacts(binding, pythonFacts);
        if (binding.Symbol == "Terrain")
        {
            return binding.CaseId switch
            {
                "dragon-model-terrain.enum.construction" => ExecuteNativeConstruction(),
                "dragon-model-terrain.enum.member-topology" => ExecuteNativeTopology(),
                "dragon-model-terrain.enum.text-projection" => ExecuteNativeTextProjection(),
                _ => throw new Xunit.Sdk.XunitException(
                    "Unknown Terrain class case '" + binding.CaseId + "'."),
            };
        }

        MemberBinding member = Assert.Single(
            NativeMembers(),
            item => item.Symbol == binding.Symbol);
        if (binding.CaseId.EndsWith(".engineering-token", StringComparison.Ordinal))
        {
            return ExecuteNativeEngineeringToken(member);
        }

        if (binding.CaseId.EndsWith(".roundtrip", StringComparison.Ordinal))
        {
            return ExecuteNativeRoundtrip(member);
        }

        if (binding.CaseId.EndsWith(".value", StringComparison.Ordinal))
        {
            return ExecuteNativeValue(member);
        }

        throw new Xunit.Sdk.XunitException(
            "Unknown Terrain member case '" + binding.CaseId + "'.");
    }

    private static void ValidatePythonFacts(CaseBinding binding, JsonElement facts)
    {
        if (binding.CaseId == "dragon-model-terrain.enum.construction")
        {
            ValidatePythonConstruction(facts);
            return;
        }

        if (binding.CaseId == "dragon-model-terrain.enum.member-topology")
        {
            ValidatePythonTopology(facts);
            return;
        }

        if (binding.CaseId == "dragon-model-terrain.enum.text-projection")
        {
            ValidatePythonTextProjection(facts);
            return;
        }

        MemberBinding member = Assert.Single(
            NativeMembers(),
            item => item.Symbol == binding.Symbol);
        if (binding.CaseId.EndsWith(".engineering-token", StringComparison.Ordinal))
        {
            AssertKeys(
                facts,
                "building_field_equals_value",
                "building_field_is_member",
                "building_field_value",
                "energyplus_choice_token",
                "model_retains_member");
            Assert.True(facts.GetProperty("building_field_equals_value").GetBoolean());
            Assert.True(facts.GetProperty("building_field_is_member").GetBoolean());
            Assert.Equal(member.Token, RequiredString(facts, "building_field_value"));
            Assert.Equal(member.Token, RequiredString(facts, "energyplus_choice_token"));
            Assert.True(facts.GetProperty("model_retains_member").GetBoolean());
            return;
        }

        if (binding.CaseId.EndsWith(".roundtrip", StringComparison.Ordinal))
        {
            AssertKeys(
                facts,
                "construct_from_member_is_member",
                "construct_from_value_is_member",
                "hash_equals_value_hash",
                "json_value",
                "lookup_by_name_is_member");
            Assert.True(facts.GetProperty("construct_from_member_is_member").GetBoolean());
            Assert.True(facts.GetProperty("construct_from_value_is_member").GetBoolean());
            Assert.True(facts.GetProperty("hash_equals_value_hash").GetBoolean());
            Assert.Equal(member.Token, RequiredString(facts, "json_value"));
            Assert.True(facts.GetProperty("lookup_by_name_is_member").GetBoolean());
            return;
        }

        if (binding.CaseId.EndsWith(".value", StringComparison.Ordinal))
        {
            AssertKeys(
                facts,
                "canonical_name",
                "declared_name",
                "equals_value",
                "is_str_instance",
                "value",
                "value_type");
            Assert.Equal(member.UpstreamName, RequiredString(facts, "canonical_name"));
            Assert.Equal(member.UpstreamName, RequiredString(facts, "declared_name"));
            Assert.True(facts.GetProperty("equals_value").GetBoolean());
            Assert.True(facts.GetProperty("is_str_instance").GetBoolean());
            Assert.Equal(member.Token, RequiredString(facts, "value"));
            Assert.Equal("str", RequiredString(facts, "value_type"));
            return;
        }

        throw new Xunit.Sdk.XunitException(
            "Unknown Python Terrain facts for '" + binding.CaseId + "'.");
    }

    private static void ValidatePythonConstruction(JsonElement facts)
    {
        AssertKeys(
            facts,
            "invalid_observations",
            "member_passthrough_identity",
            "valid_observations");
        MemberBinding[] members = NativeMembers();

        JsonElement passthrough = facts.GetProperty("member_passthrough_identity");
        AssertKeys(passthrough, members.Select(item => item.UpstreamName).ToArray());
        foreach (MemberBinding member in members)
        {
            Assert.True(passthrough.GetProperty(member.UpstreamName).GetBoolean());
        }

        JsonElement[] valid = facts.GetProperty("valid_observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(members.Length, valid.Length);
        for (int index = 0; index < valid.Length; index++)
        {
            JsonElement observation = valid[index];
            MemberBinding member = members[index];
            AssertKeys(observation, "input", "outcome", "result", "same_member");
            JsonElement input = observation.GetProperty("input");
            AssertKeys(input, "kind", "value");
            Assert.Equal("string", RequiredString(input, "kind"));
            Assert.Equal(member.Token, RequiredString(input, "value"));
            Assert.Equal("returned", RequiredString(observation, "outcome"));
            JsonElement result = observation.GetProperty("result");
            AssertKeys(result, "name", "value");
            Assert.Equal(member.UpstreamName, RequiredString(result, "name"));
            Assert.Equal(member.Token, RequiredString(result, "value"));
            Assert.True(observation.GetProperty("same_member").GetBoolean());
        }

        JsonElement[] invalid = facts.GetProperty("invalid_observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(5, invalid.Length);
        string?[] stringValues = { "country", "Rural", "" };
        for (int index = 0; index < invalid.Length; index++)
        {
            JsonElement observation = invalid[index];
            AssertKeys(
                observation,
                "error_category",
                "exception_type",
                "input",
                "outcome");
            Assert.Equal("domain", RequiredString(observation, "error_category"));
            Assert.Equal("ValueError", RequiredString(observation, "exception_type"));
            Assert.Equal("raised", RequiredString(observation, "outcome"));
            JsonElement input = observation.GetProperty("input");
            if (index < stringValues.Length)
            {
                AssertKeys(input, "kind", "value");
                Assert.Equal("string", RequiredString(input, "kind"));
                Assert.Equal(stringValues[index], RequiredString(input, "value"));
            }
            else if (index == 3)
            {
                AssertKeys(input, "decimal", "kind");
                Assert.Equal("0", RequiredString(input, "decimal"));
                Assert.Equal("int", RequiredString(input, "kind"));
            }
            else
            {
                AssertKeys(input, "kind");
                Assert.Equal("none", RequiredString(input, "kind"));
            }
        }
    }

    private static void ValidatePythonTopology(JsonElement facts)
    {
        AssertKeys(
            facts,
            "declared_member_names",
            "declared_member_values",
            "has_aliases",
            "iterated_member_names",
            "iterated_member_values",
            "member_count",
            "unique_member_count");
        MemberBinding[] members = NativeMembers();
        string[] names = members.Select(item => item.UpstreamName).ToArray();
        string[] values = members.Select(item => item.Token).ToArray();
        AssertStringArray(facts.GetProperty("declared_member_names"), names);
        AssertStringArray(facts.GetProperty("declared_member_values"), values);
        AssertStringArray(facts.GetProperty("iterated_member_names"), names);
        AssertStringArray(facts.GetProperty("iterated_member_values"), values);
        Assert.False(facts.GetProperty("has_aliases").GetBoolean());
        Assert.Equal(5, facts.GetProperty("member_count").GetInt32());
        Assert.Equal(5, facts.GetProperty("unique_member_count").GetInt32());
    }

    private static void ValidatePythonTextProjection(JsonElement facts)
    {
        AssertKeys(
            facts,
            "base_names",
            "class_name",
            "is_enum_subclass",
            "is_str_subclass",
            "json_tokens",
            "module",
            "rendered_building_tokens",
            "signature",
            "str_tokens");
        AssertStringArray(facts.GetProperty("base_names"), "str", "Enum");
        Assert.Equal("Terrain", RequiredString(facts, "class_name"));
        Assert.True(facts.GetProperty("is_enum_subclass").GetBoolean());
        Assert.True(facts.GetProperty("is_str_subclass").GetBoolean());
        Assert.Equal("idragon.dragon.model", RequiredString(facts, "module"));
        Assert.Equal("(*values)", RequiredString(facts, "signature"));

        MemberBinding[] members = NativeMembers();
        JsonElement jsonTokens = facts.GetProperty("json_tokens");
        JsonElement renderedTokens = facts.GetProperty("rendered_building_tokens");
        JsonElement stringTokens = facts.GetProperty("str_tokens");
        string[] names = members.Select(item => item.UpstreamName).ToArray();
        AssertKeys(jsonTokens, names);
        AssertKeys(renderedTokens, names);
        AssertKeys(stringTokens, names);
        foreach (MemberBinding member in members)
        {
            Assert.Equal(member.Token, RequiredString(jsonTokens, member.UpstreamName));
            string qualifiedToken = "Terrain." + member.UpstreamName;
            Assert.Equal(
                qualifiedToken,
                RequiredString(renderedTokens, member.UpstreamName));
            Assert.Equal(
                qualifiedToken,
                RequiredString(stringTokens, member.UpstreamName));
        }
    }

    private static string[] ExecuteNativeConstruction()
    {
        MemberBinding[] members = NativeMembers();
        foreach (MemberBinding member in members)
        {
            Assert.True(Enum.TryParse(member.Token, ignoreCase: false, out Terrain parsed));
            Assert.Equal(member.Value, parsed);
        }

        Assert.False(Enum.TryParse("country", ignoreCase: false, out Terrain _));
        Terrain undefined = (Terrain)int.MaxValue;
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EnergyModel(
                "Terrain oracle invalid value",
                Array.Empty<Zone>(),
                terrain: undefined));
        Assert.Equal("terrain", error.ParamName);
        return new[]
        {
            "native_representation=public-enum-not-string-subclass",
            "valid_title_case_parse_count=5;lowercase_parse=false",
            "undefined_energy_model_value=ArgumentOutOfRangeException:terrain",
        };
    }

    private static string[] ExecuteNativeTopology()
    {
        string[] names = Enum.GetNames<Terrain>();
        Terrain[] values = Enum.GetValues<Terrain>();
        int[] ordinals = values.Select(item => (int)item).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(values.Length, values.Distinct().Count());
        return new[]
        {
            "native_member_names=" + string.Join("|", names),
            "native_member_tokens=" + string.Join("|", values.Select(item => item.ToString())),
            "native_member_ordinals=" + string.Join("|", ordinals),
        };
    }

    private static string[] ExecuteNativeTextProjection()
    {
        Type type = typeof(Terrain);
        MemberBinding[] members = NativeMembers();
        string[] idfTokens = members
            .Select(member => BuildingTerrainToken(CreateModel(member.Value)))
            .ToArray();
        Assert.Equal(members.Select(item => item.Token), idfTokens);
        return new[]
        {
            "native_type=" + type.FullName,
            "native_base=" + type.BaseType!.FullName + ";is_string_assignable=" + typeof(string).IsAssignableFrom(type).ToString().ToLowerInvariant(),
            "actual_building_tokens=" + string.Join("|", idfTokens),
        };
    }

    private static string[] ExecuteNativeEngineeringToken(MemberBinding member)
    {
        EnergyModel model = CreateModel(member.Value);
        string buildingToken = BuildingTerrainToken(model);
        Assert.Equal(member.Value, model.Terrain);
        Assert.Equal(member.Token, buildingToken);
        Assert.Equal(member.Token, member.Value.ToString());
        return new[]
        {
            "model_terrain=" + model.Terrain,
            "building_terrain_field=" + buildingToken,
            "energyplus_choice_token=" + member.Token,
        };
    }

    private static string[] ExecuteNativeRoundtrip(MemberBinding member)
    {
        Terrain byName = Enum.Parse<Terrain>(member.NativeName, ignoreCase: false);
        Terrain byToken = Enum.Parse<Terrain>(member.Token, ignoreCase: false);
        string? name = Enum.GetName(member.Value);
        Assert.Equal(member.Value, byName);
        Assert.Equal(member.Value, byToken);
        Assert.Equal(member.NativeName, name);
        return new[]
        {
            "parse_native_name=" + byName,
            "parse_engineering_token=" + byToken,
            "enum_get_name=" + name,
        };
    }

    private static string[] ExecuteNativeValue(MemberBinding member)
    {
        FieldInfo field = typeof(Terrain).GetField(
            member.NativeName,
            BindingFlags.Public | BindingFlags.Static)!;
        Assert.NotNull(field);
        Assert.Equal(member.Ordinal, (int)field.GetRawConstantValue()!);
        return new[]
        {
            "native_name=" + member.NativeName,
            "native_ordinal=" + member.Ordinal,
            "native_value_type=" + typeof(Terrain).FullName + ";token=" + member.Token,
        };
    }

    private static EnergyModel CreateModel(Terrain terrain) =>
        new("Terrain oracle", Array.Empty<Zone>(), terrain: terrain);

    private static string BuildingTerrainToken(EnergyModel model)
    {
        IdfObject building = Assert.Single(model.ToIdfDocument()["Building"]);
        Assert.Equal("Building", building.ObjectType);
        return building[2];
    }

    private static MemberBinding[] NativeMembers() =>
        new MemberBinding[]
    {
        new("Terrain.COUNTRY", "COUNTRY", "Country", Terrain.Country, 0),
        new("Terrain.SUBURBS", "SUBURBS", "Suburbs", Terrain.Suburbs, 1),
        new("Terrain.CITY", "CITY", "City", Terrain.City, 2),
        new("Terrain.OCEAN", "OCEAN", "Ocean", Terrain.Ocean, 3),
        new("Terrain.URBAN", "URBAN", "Urban", Terrain.Urban, 4),
    };

    private static void ValidateReceipt(
        JsonElement receipt,
        EvidenceBinding evidence,
        IReadOnlyList<NativeObservation> expectedObservations)
    {
        AssertKeys(
            receipt,
            "fixture",
            "observations",
            "upstream_path",
            "upstream_symbol");
        Assert.Equal(evidence.Path, RequiredString(receipt, "upstream_path"));
        Assert.Equal(evidence.Symbol, RequiredString(receipt, "upstream_symbol"));

        JsonElement fixture = receipt.GetProperty("fixture");
        AssertKeys(fixture, "case_count", "cases_sha256", "path", "sha256");
        Assert.Equal(ExpectedCaseCount, fixture.GetProperty("case_count").GetInt32());
        Assert.Equal(CasesSha256, RequiredString(fixture, "cases_sha256"));
        Assert.Equal(OracleRepositoryPath, RequiredString(fixture, "path"));
        Assert.Equal(OracleSha256, RequiredString(fixture, "sha256"));

        JsonElement[] observations = receipt.GetProperty("observations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(3, observations.Length);
        Assert.Equal(
            observations.Select(item => RequiredString(item, "case_id"))
                .OrderBy(item => item, StringComparer.Ordinal),
            observations.Select(item => RequiredString(item, "case_id")));
        for (int index = 0; index < observations.Length; index++)
        {
            JsonElement observation = observations[index];
            NativeObservation expected = expectedObservations[index];
            AssertKeys(
                observation,
                "adaptation_id",
                "case_id",
                "native_facts",
                "native_outcome");
            Assert.Equal(expected.CaseId, RequiredString(observation, "case_id"));
            Assert.Equal("returned", RequiredString(observation, "native_outcome"));
            if (expected.AdaptationId is null)
            {
                Assert.Equal(
                    JsonValueKind.Null,
                    observation.GetProperty("adaptation_id").ValueKind);
            }
            else
            {
                Assert.Equal(
                    expected.AdaptationId,
                    RequiredString(observation, "adaptation_id"));
            }

            Assert.Equal(
                expected.NativeFacts,
                observation.GetProperty("native_facts")
                    .EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToArray());
        }

        AssertReceiptPayloadSafe(receipt);
        AssertNoRawAddresses(receipt.GetRawText());
        AssertNoHostPaths(receipt);
        AssertNoNonFiniteJsonNumbers(receipt);
    }

    private static void AssertStringArray(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString()!));
    }

    private static string CanonicalSha256(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false,
            }))
        {
            WriteCanonicalJson(writer, value);
        }

        return Sha256(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
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
                throw new Xunit.Sdk.XunitException(
                    "Unsupported canonical JSON kind '" + value.ValueKind + "'.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            string[] names = value.EnumerateObject()
                .Select(property => property.Name)
                .ToArray();
            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertUniqueObjectKeysRecursive(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertUniqueObjectKeysRecursive(item);
            }
        }
    }

    private static void AssertReceiptPayloadSafe(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.False(property.Name is
                    "classification" or
                    "expected_dotnet" or
                    "policy" or
                    "python" or
                    "python_facts" or
                    "python_outcome");
                AssertReceiptPayloadSafe(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertReceiptPayloadSafe(item);
            }
        }
    }

    private static void AssertNoRawAddresses(string value)
    {
        Assert.False(Regex.IsMatch(
            value,
            @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant));
    }

    private static void AssertNoHostPaths(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            Assert.False(Regex.IsMatch(
                text,
                @"^(?:[A-Za-z]:[\\/]|[\\/]{2}|/)",
                RegexOptions.CultureInvariant));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                AssertNoHostPaths(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoHostPaths(item);
            }
        }
    }

    private static void AssertNoNonFiniteJsonNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            Assert.True(value.TryGetDouble(out double number));
            Assert.False(double.IsNaN(number));
            Assert.False(double.IsInfinity(number));
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

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject()
            .Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal), actual);
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = parent.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
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

        throw new FileNotFoundException(
            "Could not locate repository file '" + relativePath + "'.");
    }

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record EvidenceBinding(
        string Path,
        string Symbol,
        string SymbolHash,
        string AssertionId);

    private sealed record SymbolContract(
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId,
        string NativeTarget)
    {
        public string SymbolHash => Assert.Single(
            ExpectedEvidence,
            evidence => evidence.Symbol == Symbol).SymbolHash;
    }

    private sealed record CaseBinding(string CaseId, string Executor, string Symbol);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string? AdaptationId,
        IReadOnlyList<string> NativeFacts);

    private sealed record MemberBinding(
        string Symbol,
        string UpstreamName,
        string Token,
        Terrain Value,
        int Ordinal)
    {
        public string NativeName => Token;
    }
}
