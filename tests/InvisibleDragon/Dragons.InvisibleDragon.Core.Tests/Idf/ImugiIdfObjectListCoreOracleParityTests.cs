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
    private const int FixtureBytes = 105_236;
    private const string FixtureSha = "sha256:6047f16dc92ae8b8e3e93daf43149ec0d8041ac15f748619e143d6efc0f7aaba";
    private const string GeneratorPath = "tools/python-reference/generate_imugi_idf_object_list_core_oracle.py";
    private const int GeneratorBytes = 22_838;
    private const string GeneratorSha = "sha256:cc504d32c9b6926093185f0bb7e4c988c4bfe9b27d035330768f5f8b980fa8c4";
    private const string ValidatorPath = "tests/PythonReference/test_imugi_idf_object_list_core_oracle.py";
    private const int ValidatorBytes = 7_509;
    private const string ValidatorSha = "sha256:56c31b542ec2bdefb75d7402f2dbbb32217e2634be826dae3566069b475e56ef";
    private const string Schema = "dragons.python-reference.imugi-idf-object-list-core.v1";
    private const string CasesSha = "sha256:60ddb2ba91b3c3b19867063bdca5be7e0f31d628f193569ba487f79cb6816c2f";
    private const string TargetsSha = "sha256:9a292cd543bb675b93c77e7456ab43def3dc0ea004159d511cab1bef17d7feb3";
    private const string TestCase = "Dragons.InvisibleDragon.Tests.Idf.ImugiIdfObjectListCoreOracleParityTests.MatchesPinnedImugiIdfObjectListThroughPublicProductionApis";
    private const string UpstreamCommit = "847b01f68f438f560a986072bcaa7768fbf67897";
    private const string UpstreamPath = "src/idragon/imugi.py";
    private static bool DiscoverPins => false;

    private static readonly Pin[] NativeSources =
    {
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", 13_182, "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfValidator.cs", 12_094, "sha256:3f1c8c191cf7054ebdbf674895a2efcabe0b4d265c0de093d900efbb369ed3dd"),
        new("src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfWriter.cs", 4_289, "sha256:cc7cc49afcd98a4d4067371686feb49d120a4dd5f7bf30611599a6512c062892"),
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
        new("A01", 8, "sha256:70f44ad4a194c5e6b7214f95a098c1a24cddaf1b2c010571aa69b6d5c0b0287f"),
        new("A02", 7, "sha256:fbf4cd1a29de5407fde1c1b1c3d8a4e7cfd41176d3e1bd8b5389d42951b4e424"),
        new("A03", 6, "sha256:dd47d8bba57298bce849839dc54062cdbafc26f5318dbd9060b97f9aa438ffdc"),
        new("A04", 5, "sha256:df4b815475f655fbee6f4020744cc91daa64d05feac02e07670a94898853c38c"),
        new("A05", 4, "sha256:306db38fa651a7025345556eb00501ce31b63811ff9c28f22bd4561c5cfb17ee"),
    };
    private static readonly string[] ExpectedReceiptHashes =
    {
        "sha256:3f2b4b1d0c62bd92b686229ab6ea48e9cc1aad9ea8af10db84c68ead088185a9",
        "sha256:c5deb0c9099e1437f492a89b65dff9701163a26ca1e1f9e483d1e538e8cc7d07",
        "sha256:0e27e06199f4905773d51875116dfb501431e2fcdb609408160a9f3ba3b52c17",
        "sha256:6002f6ea5c4a4ca30d6da6aec51f7d9f13ceed4287db368846329fd337986654",
        "sha256:32d5dfc5c677fe27f0f56709eb4368968bde3002523ce07a5c7d5d4629fefb3c",
        "sha256:bbb65da967688a2979eba54e830a62e6f88615e6d408bbba439015bf100daade",
        "sha256:892d36e2af1cf82a153e15eb5260c4867ce7f834337ab3ea6bb06ac6525a1ebf",
        "sha256:12216a28d4a840deec78c9183ba2a6b1f640bd66387acab4496cf6f402e14fdf",
        "sha256:61ae8807ed25cde238c51043177865412bb28b11506736025034d1b47d0eac92",
        "sha256:f039ee95081f54cd5220fcfac652c2c70d6afb7ad3daf24f37b5e85af38e6f1d",
        "sha256:1bf4e64095ca4515da48eb37236bb8d7ff3da7c889a04aaef2d3db8d7c06326c",
        "sha256:0bdc86658236f5d43067473a0a8c793c942a736d16a1e4d96e468bc4fbca4fc8",
        "sha256:eb97e34c666177bde8749a09b1ca24900b2db5314cee33dc77dc16fe8035c843",
        "sha256:63cd50d9fccdc1e88e18972a29c8cd937d6e47ab0ebd384479f8fa21f0efc509",
        "sha256:0d3699e544335733dfb42064e6aea27c30f458badb59a5af8658f6abe2d6f201",
        "sha256:3ae815e7977d66cc8b1c59f8c1ef2cd0e158c84842553334e8587750040ab2f0",
        "sha256:45f7e5beea6f0bfaeb2d356cdd95bc7d530a31a2ad13c4cc6901c5c1d9c41e9e",
        "sha256:05864e4e2d0d60fe66b8bac3666d5c652dff7c418066d9723e6e37f17ed9dd29",
        "sha256:4318fa206e74e772f41b79b0615d449728db041116f31159cb3be3a11a33caed",
    };


    private static readonly string[] ExpectedCollectorOutputHashes =
    {
        "sha256:f590aec6b5355bac7e1423ab409654e785b67a11cf1314053d60ec25e622c9c4", // imugi-idf-object-list-core-1190-eb2835ed
        "sha256:1c4c6458d97c48a3168d20ffa3c0e6afde7a9a31bd8ae9e4913e096e7bc86b82", // imugi-idf-object-list-core-1194-034c2864
        "sha256:fe8ab778170d8d90f07742b15353f9051dd6c2a34d10baa15f51b54964d2f659", // imugi-idf-object-list-core-1195-24887408
        "sha256:98c612dff733226ae26295f4ce3d63a787e67f4307eb7edb74a7d3aa7b0ae7c3", // imugi-idf-object-list-core-1197-d30f53e9
        "sha256:eee149798af3a5c498eaca1aad4360abbb8d36c106355ad39be60f38dc5871a9", // imugi-idf-object-list-core-1198-f05c55da
        "sha256:9f7642cc00e07e3b21a3ea075ed6b8b6c58b1c8ee4013cb264a38c554bebdcad", // imugi-idf-object-list-core-1199-06fd9750
        "sha256:abacf0cf4c49a7d7b6e3a2c1f9eaa18efb98549e934f2175bc3d28498681546c", // imugi-idf-object-list-core-1201-b4caf414
        "sha256:00bc5766ce41c656281d6126f282e8ed65f2d6026b60b3a2e2d664f891cd51ce", // imugi-idf-object-list-core-1203-72a3014e
        "sha256:48bc53427fec6caa7ac75fc9c244b1133f10fbf5ccb791ccd672c7403bb3dc39", // imugi-idf-object-list-core-1204-3d1f6b46
        "sha256:487f450912629b27492c2c2a9807d52c300a70a204d30cc76ffbd9688b96ee8a", // imugi-idf-object-list-core-1205-2c80edf4
        "sha256:b92712324b5cbc7b37eafc1a6ec89906d6243614b607cbebc296d6e90e5a17b7", // imugi-idf-object-list-core-1206-16829ca5
        "sha256:b1b50578dc808cf282242fa26e51558c68a34d46abbc9ee49b90f80de113b1b0", // imugi-idf-object-list-core-1207-0087b1d2
        "sha256:9c1d0533258a65641ca406e2186efa90c8cd9f64dc69c0d3e54b8312bfb40472", // imugi-idf-object-list-core-1208-19646451
        "sha256:520a407a8e246b5f833a28892bf90414cee1781f3edee7c8b6e1fb26737575ff", // imugi-idf-object-list-core-1209-31a2a780
        "sha256:e3b2dee6cbf5790e970589b37c113ba96a6e7d32f6546ce15d041bcbbc364464", // imugi-idf-object-list-core-1210-52ef6813
        "sha256:7dff741da121e9a526b9b3385a6e9fba00378c5614c65f168ef2e5e660df21aa", // imugi-idf-object-list-core-1211-585d5ffa
        "sha256:060c6fab0afcad8f39b134717d61b5315687e9d82a6f48922f3f7f57ddf8e68a", // imugi-idf-object-list-core-1212-063a0bdd
        "sha256:d36555b839510c1d2904d11fe9df02b693776432143da72134aa7aa1aaa3bf23", // imugi-idf-object-list-core-1214-9a9bd1c1
        "sha256:44a88d6a5639b7b168f10e5907396ba7444f5acf6236dd26f58147ca5151417c", // imugi-idf-object-list-core-1215-45bc5d0e
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
