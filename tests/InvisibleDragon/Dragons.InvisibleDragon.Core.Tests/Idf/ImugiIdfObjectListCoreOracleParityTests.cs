#pragma warning disable CA1861

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dragons.InvisibleDragon.Idf;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Idf;

public sealed class ImugiIdfObjectListCoreOracleParityTests
{
    private const string FixturePath = "fixtures/reference/python-0.7.0/imugi-idf-object-list-core-oracle.json";
    private const int FixtureBytes = 105_110;
    private const string FixtureSha = "sha256:4c4da9b23f38805b4550aa5c75c5f2899ebec336a43c7718188e267f77373767";
    private const string GeneratorPath = "tools/python-reference/generate_imugi_idf_object_list_core_oracle.py";
    private const int GeneratorBytes = 22_811;
    private const string GeneratorSha = "sha256:8243d0a6f8289209d088a7e679bf84da53cc0cedf75dbdea140596d2e0a452ca";
    private const string ValidatorPath = "tests/PythonReference/test_imugi_idf_object_list_core_oracle.py";
    private const int ValidatorBytes = 7_509;
    private const string ValidatorSha = "sha256:e66605a5c403fc186be87427bd64a9f832c3e7085768788774d760f86b9bad81";
    private const string Schema = "dragons.python-reference.imugi-idf-object-list-core.v1";
    private const string CasesSha = "sha256:60ddb2ba91b3c3b19867063bdca5be7e0f31d628f193569ba487f79cb6816c2f";
    private const string TargetsSha = "sha256:9a292cd543bb675b93c77e7456ab43def3dc0ea004159d511cab1bef17d7feb3";
    private const string TestCase = "Dragons.InvisibleDragon.Tests.Idf.ImugiIdfObjectListCoreOracleParityTests.MatchesPinnedImugiIdfObjectListThroughPublicProductionApis";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private static bool DiscoverPins => false;

    private static readonly Pin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", 13_173, "sha256:0d16e28d37136a3aa0015759ead7ee324cfed08cff1a3269326d4af144518048"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfValidator.cs", 12_082, "sha256:12488433e2e9f349553e0716531e88db275f563b7f5b806c10a316ae3719cf7e"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfWriter.cs", 4_280, "sha256:c7b98b6eed298687fca229ae7262ffdf2494953b3cc6576835cacbcc47cf998a"),
    };
    private static readonly CasePin[] Cases =
    {
        new("A01", "imugi-idf-object-list-core.construction-and-properties", 8, "sha256:b8a2f2c9e0b1daacb576b29686834f8735cef0ae5ad241cc0295b640b75b31fe", "sha256:88728631589871091bfaed6f37a6307d999073a4b3f2f93651dc51392b18e9f7"),
        new("A02", "imugi-idf-object-list-core.append-insert-index-and-set", 4, "sha256:c4889e3169f848995ccf954220d407f602273ac5507ca231984b57195268da8d", "sha256:5687334d30870976ca0bdb56b0308fa21aec506f195797de40155bacb4ee4008"),
        new("A03", "imugi-idf-object-list-core.fields-and-names", 4, "sha256:11770de066bf66ce09a761e8662c0f668714426e0a8d197be6d2dc55114b0cd3", "sha256:36009e6e83fc1286c5ba085af6d64c9b178c961ddf38c609629c536a24f9a30f"),
        new("A04", "imugi-idf-object-list-core.text-and-validity", 2, "sha256:38f23a51a79eef9d2338de5b04c0e555288795fc72454a71a70c453953347bfd", "sha256:477cacdffa9b37d8d0af2f12d47e3ab4f13cd9a6a1dfa88160efee132fd4a6fd"),
        new("A05", "imugi-idf-object-list-core.set-window-wall-ratio-placeholder", 1, "sha256:b689f20d9880c35704a0a7bd2e3b2e3e57ec8c78eb508dd3721ec392f4d12879", "sha256:a544572b20ae8a283c23d1fe38ff917f6a1f219df8581ee2e27c833e891aab49"),
    };
    private static readonly Expected[] ExpectedTargets =
    {
        T(1190,"IdfObjectList",0), T(1194,"IdfObjectList.__getitem__",1), T(1195,"IdfObjectList.__init__",0),
        T(1197,"IdfObjectList.__setitem__",1), T(1198,"IdfObjectList.__str__",3), T(1199,"IdfObjectList.append",1),
        T(1201,"IdfObjectList.check_validity",3), T(1203,"IdfObjectList.ensure_validity",0), T(1204,"IdfObjectList.fieldnames",2),
        T(1205,"IdfObjectList.get_fields",2), T(1206,"IdfObjectList.has_name",0), T(1207,"IdfObjectList.has_parent",0),
        T(1208,"IdfObjectList.idd",0), T(1209,"IdfObjectList.insert",1), T(1210,"IdfObjectList.is_containor",0),
        T(1211,"IdfObjectList.names",2), T(1212,"IdfObjectList.parent",0), T(1214,"IdfObjectList.set_fields",2),
        T(1215,"IdfObjectList.set_wwr",4),
    };
    private static readonly HashSet<int> Equivalent = new() { 1194, 1199, 1209, 1211 };
    private static readonly NativePin[] ExpectedNativePins =
    {
        new("A01", 8, "sha256:87504005220c654cd1040e14f0e1fdade13c3e0fbe29517a8c1ac80130aa5d9a"),
        new("A02", 7, "sha256:fbf4cd1a29de5407fde1c1b1c3d8a4e7cfd41176d3e1bd8b5389d42951b4e424"),
        new("A03", 6, "sha256:dd47d8bba57298bce849839dc54062cdbafc26f5318dbd9060b97f9aa438ffdc"),
        new("A04", 5, "sha256:df4b815475f655fbee6f4020744cc91daa64d05feac02e07670a94898853c38c"),
        new("A05", 4, "sha256:306db38fa651a7025345556eb00501ce31b63811ff9c28f22bd4561c5cfb17ee"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:559e1144e8f3e21673baa603baaa5bd7e07ac2d6c90c10bcd264a409d5578b3f",
        "sha256:0718e3852553ca0862f3006e42c8727298586adb2fadb50d8fb72e8218a83961",
        "sha256:62c9093974a8d103ec4f112e50d2104240d634d5f3d8f3577b3e07b57a3cffc2",
        "sha256:0022dd84f0dfba05cef7c74d6a1ecd38d8a1575562686875b31819c88c20e4ec",
        "sha256:c965b254c6353cb4912715816b1fcf291ce9145a292e3905af2efe314d82c1b8",
        "sha256:b528b7f2bbe9ade8529552b4caba0d0cad5eb582d08851998fdfd3ae459398eb",
        "sha256:0ced4df11a96f6c2c13f4701ebd56f2cff39fbfd1586f8193d2cba27a8e823d2",
        "sha256:35602e2323291d3998d5829735b7c76c635dd54bfda5cc6a2860c85f900730d3",
        "sha256:4b983b7035469f302fe44047b67ac2f868160012dd6636349ce5415e65044a0e",
        "sha256:dd0b3769d7ef81dacdbb706325068458144b721e2e26721ae3514a8f141570cf",
        "sha256:d8af590971db7dd4a37d4a687f86ac753bf2ab82df672969746860d33799fb64",
        "sha256:713b463aa68bd2dc0033cc35786921e3104a8cd034d78fe35c800d3bc3cca28e",
        "sha256:31f7afa836f34e6ad1495e6468cca418e1651ffa7314abb8655846f90154b52a",
        "sha256:ed4c77d553ba6cf3d2a02750098d2e2e58184bc677ba121e42efea10a09d508a",
        "sha256:cfeb99b1ace9c032154a30531ab451d3e66b4f9a322ec85bb77394781e3076d1",
        "sha256:fac0a6eca0a873cfc0783afb8bf179739315d7ba35eee2accb36a8da75b927f7",
        "sha256:4a0eb58310f174e78bc3c8f88e5b0b58c53e409d8f1ee84169203146713870f2",
        "sha256:8d8be7cea3651ccae6e4b25a5b81178f717d11ec0c2cac0c61290540d742db0e",
        "sha256:c753bade010fd82910b93b19e34d1fe6f8ce935e21f74efa9437823db2f5f084",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:ab01c40ee509ac0c3586e422b246f6fc2252302f6b95b6bdb73876bfdaca2206", // imugi-idf-object-list-core-1190-eb2835ed
        "sha256:205e7a61f8297945d3aa2f1041a04aa7a3eafc8a1db53e6390613aa3a38edad8", // imugi-idf-object-list-core-1194-034c2864
        "sha256:7fb7fd78923b4ae01f7fe9b3c78dec968ac193e5a9ba383574e1ec4181826b9e", // imugi-idf-object-list-core-1195-24887408
        "sha256:706bc70350cf6c26769f83b56217fe4db8ce6a6abd6fa9ea315d09c89d98dce0", // imugi-idf-object-list-core-1197-d30f53e9
        "sha256:55f1b049429cb44f3382e7d1b0efee0db825e17c459f69567e4f1a07f37248b2", // imugi-idf-object-list-core-1198-f05c55da
        "sha256:4f44acbb2aed2de04732c6fd2270e27a3a67a6fea8897f12bbd4ab114e76a835", // imugi-idf-object-list-core-1199-06fd9750
        "sha256:9be7a1e0704178db47f3059672d2cab6f6a01dc0de60c1fa09f290ddf9c855fa", // imugi-idf-object-list-core-1201-b4caf414
        "sha256:27dd56b8f9f770693836dbbf00efdc9dcfdd3de3a70333214bc08c25a8123be5", // imugi-idf-object-list-core-1203-72a3014e
        "sha256:a20da614709554074e51fd784d4ea1ba7750c5e401682f324206eb24999bf0e1", // imugi-idf-object-list-core-1204-3d1f6b46
        "sha256:eb437b44fb6f1503529e97d5dd30f16d2459d9dcb7d5eeca13c724c75731b70b", // imugi-idf-object-list-core-1205-2c80edf4
        "sha256:c3d4af23bca98f3ce04c2df2f5f5edd1d89023aa5c3b11818407a8ccb665c196", // imugi-idf-object-list-core-1206-16829ca5
        "sha256:d3c621b803a4c0fa0a8774022d46fc6e2bbb510e045b4fe6ccdcaa9aeb8fcde1", // imugi-idf-object-list-core-1207-0087b1d2
        "sha256:39dea5e32196fa0b3889a71fbaafe83cc1d691c746614149441cf315cef6d000", // imugi-idf-object-list-core-1208-19646451
        "sha256:e45e6fa9a1d7fae8dd5f807abcda8daa707ada59b1f605d247cf714e98651932", // imugi-idf-object-list-core-1209-31a2a780
        "sha256:35f6f953fff5bdde7c6e9e0cbbd10080ffbe0a480cc8b9db415717b1674b9aca", // imugi-idf-object-list-core-1210-52ef6813
        "sha256:690ad50b0dd1f71e25d2675c13eb955ad5593015df197d501e40d38e70028250", // imugi-idf-object-list-core-1211-585d5ffa
        "sha256:72ae5034cbe13696783e69fccfd744029304f4f0f1d2f9abc629643b011af54b", // imugi-idf-object-list-core-1212-063a0bdd
        "sha256:3f876fbe5cfd856f04e536499a514bf05d07ab4e20ed5a902e65f90ae8198773", // imugi-idf-object-list-core-1214-9a9bd1c1
        "sha256:4972632ab03e09cc2be79abb9a655523a6e98d629265cc184ec0479d24c7f5c8", // imugi-idf-object-list-core-1215-45bc5d0e
    };

    [Fact]
    public void MatchesPinnedImugiIdfObjectListThroughPublicProductionApis()
    {
        ValidateApisAndPins();
        using JsonDocument fixture = ReadFixture();
        Target[] targets = ValidateFixture(fixture.RootElement);
        Observation[] observations = Cases.Select(Observe).ToArray();
        object[] receipts = targets.Select(target => Receipt(target, observations)).ToArray();
        string[] hashes = receipts.Select(value => CanonicalSha(JsonSerializer.SerializeToElement(value))).ToArray();
        string[] collectorOutputHashes = receipts
            .Select(receipt => CanonicalSha(JsonSerializer.SerializeToElement(new
            {
                cases = new[]
                {
                    new
                    {
                        output = receipt,
                        test_case = TestCase,
                    },
                },
            })))
            .ToArray();
        if (DiscoverPins)
        {
            string native = string.Join(Environment.NewLine, observations.Select(x => $"new(\"{x.Code}\", {x.Facts.Length}, \"{x.Hash}\"),"));
            string receipt = string.Join(Environment.NewLine, targets.Select((x, i) => $"\"{hashes[i]}\", // {x.Index} {x.Symbol}"));
            throw new Xunit.Sdk.XunitException("IMUGI_LIST_PINS\n" + native + "\n" + receipt);
        }
        Assert.Equal(ExpectedNativePins, observations.Select(x => new NativePin(x.Code, x.Facts.Length, x.Hash)).ToArray());
        Assert.Equal(ExpectedReceiptHashes, hashes);
        Assert.Equal(ExpectedCollectorOutputHashes, collectorOutputHashes);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach ((Target target, object receipt) in targets.Zip(receipts))
        {
            Assert.True(ids.Add(target.AssertionId));
            TrustedEvidenceRecorder.Record(target.AssertionId, TestCase, "not_applicable", receipt);
        }
        Assert.Equal(19, ids.Count);
        Assert.Equal(4, targets.Count(x => x.Classification == "equivalent"));
        Assert.Equal(15, targets.Count(x => x.Classification == "exception"));
    }

    private static Expected T(int index, string symbol, int caseIndex) => new(index, symbol, Cases[caseIndex].Id);

    private static void ValidateApisAndPins()
    {
        AssertArtifact(GeneratorPath, GeneratorBytes, GeneratorSha);
        AssertArtifact(ValidatorPath, ValidatorBytes, ValidatorSha);
        foreach (Pin pin in NativeSources) AssertArtifact(pin.Path, pin.Bytes, pin.Sha);
        Type type = typeof(IdfObjectCollection);
        Assert.True(type.IsPublic && type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), x => x.Name == nameof(IdfObjectCollection.Append));
        Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), x => x.Name == nameof(IdfObjectCollection.Insert));
        Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), x => x.Name == nameof(IdfObjectCollection.TryGetByName));
        Assert.Single(typeof(IdfValidator).GetMethods(BindingFlags.Public | BindingFlags.Static), x => x.Name == nameof(IdfValidator.Validate));
    }

    private static JsonDocument ReadFixture()
    {
        byte[] bytes = File.ReadAllBytes(Find(FixturePath));
        Assert.Equal(FixtureBytes, bytes.Length);
        Assert.Equal(FixtureSha, Sha(bytes));
        return JsonDocument.Parse(bytes);
    }

    private static Target[] ValidateFixture(JsonElement root)
    {
        Assert.Equal(Schema, S(root, "schema"));
        Assert.Equal(CasesSha, S(root, "cases_sha256"));
        Assert.Equal(CasesSha, CanonicalSha(root.GetProperty("cases")));
        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(5, cases.Length);
        for (int i = 0; i < Cases.Length; i++)
        {
            Assert.Equal(Cases[i].Code, S(cases[i], "code"));
            Assert.Equal(Cases[i].Id, S(cases[i], "id"));
            Assert.Equal(Cases[i].Count, cases[i].GetProperty("target_symbols").GetArrayLength());
            Assert.Equal(Cases[i].CaseSha, S(root.GetProperty("case_sha256"), Cases[i].Id));
            Assert.Equal(Cases[i].FactsSha, S(cases[i].GetProperty("python"), "facts_sha256"));
        }
        JsonElement contract = root.GetProperty("consumer_contract");
        Assert.Equal(4, contract.GetProperty("classification_counts").GetProperty("equivalent").GetInt32());
        Assert.Equal(15, contract.GetProperty("classification_counts").GetProperty("exception").GetInt32());
        JsonElement evidence = contract.GetProperty("evidence_contract");
        Assert.False(evidence.GetProperty("active_energyplus_process_claim").GetBoolean());
        Assert.False(evidence.GetProperty("internal_native_route_claim").GetBoolean());
        Assert.False(evidence.GetProperty("python_api_or_source_compatibility_claim").GetBoolean());
        Assert.False(evidence.GetProperty("structural_only").GetBoolean());
        Assert.Equal(19, evidence.GetProperty("expected_receipt_count").GetInt32());
        Assert.Equal(TargetsSha, S(contract.GetProperty("closure").GetProperty("partition_receipts_sha256"), "target"));
        JsonElement targetElement = root.GetProperty("target_receipts");
        Assert.Equal(TargetsSha, CanonicalSha(targetElement));
        JsonElement[] rows = targetElement.EnumerateArray().ToArray();
        Assert.Equal(19, rows.Length);
        var result = new Target[19];
        for (int i = 0; i < rows.Length; i++)
        {
            Expected expected = ExpectedTargets[i]; JsonElement row = rows[i];
            Assert.Equal(expected.Index, row.GetProperty("inventory_index").GetInt32());
            Assert.Equal(expected.Symbol, S(row, "symbol"));
            string classification = Equivalent.Contains(expected.Index) ? "equivalent" : "exception";
            Assert.Equal(classification, S(contract.GetProperty("classifications"), expected.Symbol));
            string route = Route(expected.Symbol);
            Assert.Equal(route, S(contract.GetProperty("native_routes"), expected.Symbol));
            result[i] = new(expected.Index, expected.Symbol, S(row,"kind"), S(row,"symbol_hash"), S(row,"signature_hash"), S(row,"body_hash"), classification, S(contract.GetProperty("assertion_ids"), expected.Symbol), S(contract.GetProperty("adaptations"), expected.Symbol), route, expected.CaseId);
        }
        return result;
    }

    private static string Route(string symbol) => symbol switch
    {
        "IdfObjectList.__getitem__" => "Dragons.InvisibleDragon.Idf.IdfObjectCollection.this[int|string]",
        "IdfObjectList.append" => "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Append(IdfObject)",
        "IdfObjectList.insert" => "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Insert(int, IdfObject)",
        "IdfObjectList.names" => "Dragons.InvisibleDragon.Idf.IdfObjectCollection -> IdfObject.Name",
        _ => "Dragons.InvisibleDragon.Idf.IdfObjectCollection/IdfDocument public typed adaptation (no Python API/source compatibility claim)",
    };

    private static Observation Observe(CasePin item)
    {
        string[] facts = item.Code switch { "A01" => Construction(), "A02" => Editing(), "A03" => Fields(), "A04" => TextValidity(), "A05" => WindowRatio(), _ => throw new InvalidOperationException() };
        return new(item.Code, item.Id, facts, CanonicalSha(JsonSerializer.SerializeToElement(facts)));
    }

    private static IdfDocument Document() => new(objects: new[] { new IdfObject("Oracle:Object", new[] { "Zero", "On", "1" }) });

    private static string[] Construction()
    {
        var document = Document(); IdfObjectCollection list = document["Oracle:Object"];
        return new[] { "public-type=" + list.GetType().FullName, "sealed=" + list.GetType().IsSealed, "count=" + list.Count, "document-schema-null=" + (document.Schema is null), "has-name=" + list.TryGetByName("zero", out _), "constructor-public=false", "python-parent-property=false", "python-containor-property=false" };
    }

    private static string[] Editing()
    {
        var document = Document(); IdfObjectCollection list = document["Oracle:Object"];
        list.Append(new IdfObject("Oracle:Object", new[] { "Second", "Off", "2" }));
        list.Insert(1, new IdfObject("Oracle:Object", new[] { "Middle", "On", "3" }));
        Exception wrong = Assert.Throws<ArgumentException>(() => list.Append(new IdfObject("Other:Object")));
        return new[] { "count=" + list.Count, "integer-index=" + list[0].Name, "name-index=" + list["middle"].Name, "append-route=public", "insert-route=public", "document-order=" + string.Join("|", document.Select(x => x.Name)), "wrong-type=" + wrong.GetType().Name };
    }

    private static string[] Fields()
    {
        IdfObjectCollection list = Document()["Oracle:Object"];
        string names = string.Join("|", list.Select(x => x.Name));
        string fields = string.Join("|", list.Select(x => x[1]));
        return new[] { "names=" + names, "field-values=" + fields, "definition-null=" + (list[0].Definition is null), "typed-enumeration=true", "python-set-fields-api=false", "python-fieldnames-api=false" };
    }

    private static string[] TextValidity()
    {
        var document = Document(); string first = IdfWriter.Write(document); string second = IdfWriter.Write(document);
        var validation = IdfValidator.Validate(document);
        return new[] { "writer-deterministic=" + (first == second), "writer-sha256=" + Sha(Encoding.UTF8.GetBytes(first)), "validation-public-route=true", "validation-valid=" + validation.IsValid, "active-energyplus-process-claim=false" };
    }

    private static string[] WindowRatio() => new[] { "set-wwr-public-api=false", "geometry-mutation-performed=false", "classification=exception", "python-api-source-compatibility-claim=false" };

    private static object Receipt(Target target, IReadOnlyList<Observation> observations)
    {
        Observation observation = Assert.Single(observations, x => x.CaseId == target.CaseId);
        CasePin fixtureCase = Assert.Single(Cases, x => x.Id == target.CaseId);
        return new { adaptation_id = target.Adaptation, artifacts = new { fixture = Artifact(FixturePath,FixtureBytes,FixtureSha), generator = Artifact(GeneratorPath,GeneratorBytes,GeneratorSha), native_sources = NativeSources.Select(x => Artifact(x.Path,x.Bytes,x.Sha)).ToArray(), python_validator = Artifact(ValidatorPath,ValidatorBytes,ValidatorSha) }, assertion_id = target.AssertionId, classification = target.Classification, native_route = target.Route, observations = new[] { new { case_code=observation.Code, case_id=observation.CaseId, native_fact_count=observation.Facts.Length, native_facts=observation.Facts, native_facts_sha256=observation.Hash, python_case_sha256=fixtureCase.CaseSha, python_facts_sha256=fixtureCase.FactsSha } }, scope = new { active_energyplus_process_claim=false, equivalent_target_count=4, exact_case_count=5, exact_target_count=19, exception_target_count=15, fixture_repository_commit="db1f31e", internal_native_route_claimed=false, public_production_routes_only=true, python_api_or_source_compatibility_claim=false, structural_only=false }, source_receipt = new { body_hash=target.BodyHash, inventory_index=target.Index, kind=target.Kind, path=UpstreamPath, signature_hash=target.SignatureHash, symbol=target.Symbol, symbol_hash=target.SymbolHash }, target_symbol=target.Symbol, upstream = new { commit=UpstreamCommit, source_path=UpstreamPath, target_receipts_sha256=TargetsSha } };
    }

    private static object Artifact(string path,int bytes,string sha256) => new { bytes,path,sha256 };
    private static void AssertArtifact(string path,int bytes,string sha) { byte[] value=File.ReadAllBytes(Find(path)); Assert.Equal(bytes,value.Length); Assert.Equal(sha,Sha(value)); }
    private static string Find(string relative) { DirectoryInfo? d=new(AppContext.BaseDirectory); while(d is not null) { string p=Path.Combine(d.FullName,relative.Replace('/',Path.DirectorySeparatorChar)); if(File.Exists(p)) return p; d=d.Parent; } throw new FileNotFoundException(relative); }
    private static string S(JsonElement value,string property) => Assert.IsType<string>(value.GetProperty(property).GetString());
    private static string Sha(byte[] value) => "sha256:"+Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static string CanonicalSha(JsonElement value) => Sha(Encoding.UTF8.GetBytes(Canonical(value)));
    private static string Canonical(JsonElement value) { using var stream=new MemoryStream(); using(var writer=new Utf8JsonWriter(stream,new JsonWriterOptions { Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) Write(writer,value); return Encoding.UTF8.GetString(stream.ToArray()); }
    private static void Write(Utf8JsonWriter writer,JsonElement value) { switch(value.ValueKind) { case JsonValueKind.Object: writer.WriteStartObject(); foreach(JsonProperty p in value.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(writer,p.Value);} writer.WriteEndObject(); break; case JsonValueKind.Array: writer.WriteStartArray(); foreach(JsonElement x in value.EnumerateArray()) Write(writer,x); writer.WriteEndArray(); break; case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break; case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText()); break; case JsonValueKind.True: writer.WriteBooleanValue(true); break; case JsonValueKind.False: writer.WriteBooleanValue(false); break; case JsonValueKind.Null: writer.WriteNullValue(); break; default: throw new InvalidOperationException(); } }
    private sealed record Pin(string Path,int Bytes,string Sha);
    private sealed record CasePin(string Code,string Id,int Count,string CaseSha,string FactsSha);
    private sealed record Expected(int Index,string Symbol,string CaseId);
    private sealed record Target(int Index,string Symbol,string Kind,string SymbolHash,string SignatureHash,string BodyHash,string Classification,string AssertionId,string Adaptation,string Route,string CaseId);
    private sealed record Observation(string Code,string CaseId,string[] Facts,string Hash);
    private sealed record NativePin(string Code,int Count,string Hash);
}
