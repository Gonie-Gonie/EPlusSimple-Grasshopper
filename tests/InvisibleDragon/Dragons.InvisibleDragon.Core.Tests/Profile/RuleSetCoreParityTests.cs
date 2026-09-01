using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dragons.InvisibleDragon.Profile;
using Dragons.UpstreamTracker;

namespace Dragons.InvisibleDragon.Tests.Profile;

public sealed class RuleSetCoreParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/rule-set-core-oracle.json";
    private const string OracleSha256 =
        "sha256:091fa6bffcab120a51c2b46f5533909bb4b909a929754029d99c866fe1e6e9e4";
    private const string CasesSha256 =
        "sha256:9509c5d9ed393dfdbedcd786b62ddca041fbacb1d13aa91391d441ec3c67bb63";
    private const int OracleByteLength = 170_467;
    private const int ExpectedCaseCount = 72;
    private const string OracleSchema =
        "dragons.invisibledragon.rule-set-core-oracle.v1";
    private const string EvidenceTestCase =
        "Dragons.InvisibleDragon.Tests.Profile.RuleSetCoreParityTests.MatchesPinnedPythonRuleSetCore";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";

    private static readonly string[] SlotKeys =
    {
        "weekdays",
        "weekends",
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday",
        "holiday",
    };

    // Exact three-literal bindings are consumed by the compatibility manifest
    // collector without interpreting test behavior.
    private static readonly EvidenceBinding[] ExpectedEvidence =
    {
        new("RuleSet", "sha256:3e0aaca76114e9e5a84d2b6ceb9a650913ad03b5bb6d35c99d3f0a5f97b36994", "profile-ruleset-core-value-object-3e0aaca7"),
        new("RuleSet.__deepcopy__", "sha256:058f6012eabebca75ffb65c55f0fc1fccc51995d38521394faaceb32dfbb9748", "profile-ruleset-core-deepcopy-058f6012"),
        new("RuleSet.__init__", "sha256:f1c4b446cbbc826152dae8f4c4677d323271ad408d12fda0b0b527ae9ecaec51", "profile-ruleset-core-init-f1c4b446"),
        new("RuleSet.astype", "sha256:0c0d27de9ef57d948f60d77e2ad8ff58f6898f25965135e6584bb0ad65dff226", "profile-ruleset-core-astype-0c0d27de"),
        new("RuleSet.clip", "sha256:c3bd923567b392c6753dd317132395cea97872100ce08853d218144d579f1ede", "profile-ruleset-core-clip-c3bd9235"),
        new("RuleSet.friday", "sha256:72220457054927f7999dd905ba93aabf314844b6571266eea6aefbc92823880b", "profile-ruleset-core-friday-72220457"),
        new("RuleSet.from_constant", "sha256:1093e8f49640c59a592997f0bf053e4a153733c7fab6d2ac36dc913c742e635c", "profile-ruleset-core-from-constant-1093e8f4"),
        new("RuleSet.from_days", "sha256:d1d5dd6fce56c158588b0e2ce11671d0063e1e16aee2161b15af5a1c7f5213e9", "profile-ruleset-core-from-days-d1d5dd6f"),
        new("RuleSet.get_dayschedule", "sha256:51486c906fb24fd537abf5d0f07c77d5ec77c150a9f3874e3f1991d59b1de645", "profile-ruleset-core-get-dayschedule-51486c90"),
        new("RuleSet.holiday", "sha256:9bbd78bae0f36cfa3af556f39f48a53eb852e8a65e338b0a7ea8235a4861087a", "profile-ruleset-core-holiday-9bbd78ba"),
        new("RuleSet.max", "sha256:c62c3676c65897d28e02ae555dd0343582ed8be67411b36fedbef32cca4d3d38", "profile-ruleset-core-max-c62c3676"),
        new("RuleSet.min", "sha256:bf1962353ed21c07ad290a4ff9a5ccd94e7db31b8f7a6313ef3104e280a30807", "profile-ruleset-core-min-bf196235"),
        new("RuleSet.monday", "sha256:4cca788f61eeb17cb784485a8b94b01ff7679deadfdf1d4d9fe8160abfe54c95", "profile-ruleset-core-monday-4cca788f"),
        new("RuleSet.saturday", "sha256:693a3041f2dcd664bea7b574ecea60f7f4b66dce26af8cefbdf49ae356fb71a2", "profile-ruleset-core-saturday-693a3041"),
        new("RuleSet.summary", "sha256:f669cea057b58f712dc37b4439991f0fc91aa923ecd1d7b0ab80f8e7cd8cc9fc", "profile-ruleset-core-summary-f669cea0"),
        new("RuleSet.sunday", "sha256:cfcbc078846cee7f94d9c09b0f32190b495489b8b8f1f21b860a7cbaa16324fc", "profile-ruleset-core-sunday-cfcbc078"),
        new("RuleSet.thursday", "sha256:2d3bbbc02f1cd354f02d0a564e8c8979970ed867e9f5e61fbd76d5465293f602", "profile-ruleset-core-thursday-2d3bbbc0"),
        new("RuleSet.to_dict", "sha256:e2a85d522fcc2dbacec768944cf872ba9b7ffd5dd42e03eb4f7cac035da2efff", "profile-ruleset-core-to-dict-e2a85d52"),
        new("RuleSet.to_idf_compactexpr", "sha256:015a80b07ad77b088b27d89e7c2f2224553870ad20bffa55a4d43dd1573fb6de", "profile-ruleset-core-idf-compactexpr-015a80b0"),
        new("RuleSet.tuesday", "sha256:30f9dc0b522275442a6bff0cc22640539789e05274ad72c7a77b487caebe3e68", "profile-ruleset-core-tuesday-30f9dc0b"),
        new("RuleSet.type", "sha256:63a5d7d94275c2184f4c9eae268b1c33ee3351525711e7c232fae68f80c84d6a", "profile-ruleset-core-type-63a5d7d9"),
        new("RuleSet.wednesday", "sha256:a896496ee156854d2a7693128f6a66bac9628457468ef2a7b9a00732abe86a22", "profile-ruleset-core-wednesday-a896496e"),
        new("RuleSet.weekdays", "sha256:f89fcc578196070c1535586d7c2b7142654dcf2d4b0179c21ee92643e0098294", "profile-ruleset-core-weekdays-f89fcc57"),
        new("RuleSet.weekends", "sha256:c3fdd0ae9d51b8f43fa821ee2dd774593c90db66b6c19c4a51e0db0ccfd927b5", "profile-ruleset-core-weekends-c3fdd0ae"),
    };

    private static readonly SymbolContract[] ExpectedSymbols =
    {
        new("RuleSet", "class", "sha256:ef0a54678cdf78602bc289989268ec4be5889a84c3c5540e29a46e191d125158", "sha256:a11810c9b7a2d650b7c76769e34925cdc1c160f0d92a783d616c06a008bbe832", "exception", "immutable-ruleset-value-object"),
        new("RuleSet.__deepcopy__", "function", "sha256:520ee536d924ac7323d561d9d85957e67316c14aea8bd80a5664a796409a796f", "sha256:ad49ff4fff462758e5d5d8a1cf7eff1eedc4ff042da1d3315aa46a4e48a5d144", "exception", "native-ruleset-deepcopy-memo"),
        new("RuleSet.__init__", "function", "sha256:608b060baba1615ff775a1a258ea80f31531ab2ead04af7cffe6f9807fa70da4", "sha256:ad14a63315409c3ec6728c9e82f0b40de2300b6cd2f6c3959efa400a9cd7d945", "exception", "immutable-deterministic-ruleset-construction"),
        new("RuleSet.astype", "function", "sha256:cf39c8fabdbe0ecbd6ba6adc59426feaf3c128631fbeee6c69d60e5e73aa7855", "sha256:9cf91845f70b2249a6c498393e623d3af79ee8b1d086aa3b2b6c62d90d1c178c", "exception", "immutable-ruleset-astype"),
        new("RuleSet.clip", "function", "sha256:855c6f59d635ebd7895537821026cb2c9c8c4c6c4bfc9d5e1640d6559f48a981", "sha256:9fe56b3a1c9f21d7a0721710ba240a16554f15890d6ddd6643fdfcc5e5943da9", "exception", "immutable-ruleset-clip"),
        new("RuleSet.friday", "function", "sha256:d8f80b44e53531d060f0315e07cf5630b29410ac1af17bb8124150b1b93fa1fa", "sha256:d9fd4ee092614b6a06ba2cdfe3769cb105c911629c127301df27b05daf7aef28", "exception", "immutable-ruleset-friday-update"),
        new("RuleSet.from_constant", "function", "sha256:78f492e0a2dd8ba19b55d416aa9c12edb3b68423e6cf2c5502a5928ac3d0abc6", "sha256:0b19637e1c09fe802513e548d388a8fd7dc47804fd0eb187da6c1e4b64507212", "exception", "deterministic-finite-ruleset-from-constant"),
        new("RuleSet.from_days", "function", "sha256:733b678bcfef161c72c4fb040e97e0788ae11f76e7dec1ddde67eaa2606d10bf", "sha256:507371e6221a26c23208480654600694beebfb4af3a634586fca5733b2790b52", "exception", "validated-deterministic-ruleset-from-days"),
        new("RuleSet.get_dayschedule", "function", "sha256:1a8742611ecfc2141d656bc92ba8e467f9d868558ad5291c525ee8c2e09e420a", "sha256:28f0f003c41d9065e1f6831570bc9bc1e3da85838689c18379f6d5ddba8d97fb", "equivalent", null),
        new("RuleSet.holiday", "function", "sha256:3af0ebe3e9623dfc7fed3f710017eb215d6a1cc57fe9b714489e97aac7bfaa87", "sha256:be07a7c048c4dab4a5a53d5c20bc17f0eaa6a369b442ea404cf0b006c0852bbe", "exception", "immutable-ruleset-holiday-update"),
        new("RuleSet.max", "function", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:47c7d237620a0132c7199b5eb33f1a1305f80ba00118929b0422f9d4f5b099ae", "equivalent", null),
        new("RuleSet.min", "function", "sha256:f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "sha256:1c7b752a31153019c85a5c71e06f54ac38e202d62817d270c4bd253dddc4731e", "equivalent", null),
        new("RuleSet.monday", "function", "sha256:f10341133c365d37adad333fcef83b309cc925d031a315bc6ac2bbf8377d8dbd", "sha256:c2da11bad3eb7d6a6fc0f1cff443179ac04d8770ea24af283a12a754382cd3de", "exception", "immutable-ruleset-monday-update"),
        new("RuleSet.saturday", "function", "sha256:2380dd190eec51d7aa37263facd1a9e12989f01d756e19b1a4f63f5e383b4968", "sha256:cfccab53a6c430e822787ac78cc0ff2a5be1e1ee6a15b6e3b1c29d0a959404f8", "exception", "immutable-ruleset-saturday-update"),
        new("RuleSet.summary", "function", "sha256:1b593b0eff21728ab25d12af1562984d3da660986aafcaf89a44de6ef14d62ce", "sha256:910befff9ae9f300a776f7281dfcdd7d5887ca33710cf7fc464c03ce3cb397c8", "equivalent", null),
        new("RuleSet.sunday", "function", "sha256:b2b8b108f6b302456dd1d0ec62fc74adec221dccd2adc42ace3f4b48d92860ed", "sha256:95442f9f0651fb3ddc87a1c329eda501393062819231d68fae2a637d6148955a", "exception", "immutable-ruleset-sunday-update"),
        new("RuleSet.thursday", "function", "sha256:5be74f7ac04eb4575303a9f8649d0ffd3a06b9d9e22a2c9e30b2efefbf00ae59", "sha256:5889f9223e779a76a35591e31aa979288d0b8dfa45ddeb8c2124aa8d7a2f33bc", "exception", "immutable-ruleset-thursday-update"),
        new("RuleSet.to_dict", "function", "sha256:a99804c1ea11fb4281230b344829794ccaecb9e02c8e74a18a290bf00441116e", "sha256:57763202f4ee3c29d58de3342489991202a44c8fe08d5d4ec7594cbd5059337c", "equivalent", null),
        new("RuleSet.to_idf_compactexpr", "function", "sha256:636cf3ce72c5b8fab425c494f6300a642ea6dcc1bbec8f0653a7746285088cb8", "sha256:186931bf4cdf99bd478390cf576b2ddcd58b71ef219832d80d1985fa514dec54", "equivalent", null),
        new("RuleSet.tuesday", "function", "sha256:c590b6062061da5e42864e2fd88b4293bf880eee53b1b837ba823a588b628bf2", "sha256:1e5ef088530721dbaa7db5d5cafefec5c86643beb860e6c5d65a1aaf9011302a", "exception", "immutable-ruleset-tuesday-update"),
        new("RuleSet.type", "function", "sha256:5dede16ed32055ee5ef3307fdd9e5d66bb15147e3dfbfe282745b9285cfa267a", "sha256:c127d9c3b77bd9baf591dbecb6bac00e4249af93aa7b3ef5a503333c07abc581", "equivalent", null),
        new("RuleSet.wednesday", "function", "sha256:38547e842b968d7e914894ef043cb565fea149051755d1dc91b1d7337e404741", "sha256:2c763ac25ce46c7b660a88d96d5a6564f65ab6cfc232db3a3e42e55408df53e8", "exception", "immutable-ruleset-wednesday-update"),
        new("RuleSet.weekdays", "function", "sha256:c49d1194cd324beae53bca8145ab606d1a26c9f21d86fbb04f4b93217831a1cb", "sha256:105a78e7e0595fef7130a929dc130834e8a832a77923a4583f9d1c2c9ec5dbf8", "exception", "immutable-ruleset-weekdays-update"),
        new("RuleSet.weekends", "function", "sha256:e362ecffa7bd1eda05a79c1cd15c72e8b974c083acbe741197f66b08ce2ee420", "sha256:df662aecc01710f785b1221e793e9c3987eeaa3e2a5d17ad043e592a9ad1ca6b", "exception", "immutable-ruleset-weekends-update"),
    };

    private static readonly CaseBinding[] ExpectedCases =
    {
        new("astype.inplace-stale-type", "astype", "RuleSet.astype", "returned", null),
        new("astype.outplace-string", "astype", "RuleSet.astype", "returned", null),
        new("astype.partial-failure", "astype", "RuleSet.astype", "raised", "domain"),
        new("class.alias-topology", "class", "RuleSet", "returned", null),
        new("class.mutable-slot", "class", "RuleSet", "returned", null),
        new("class.slot-inventory", "class", "RuleSet", "returned", null),
        new("clip.bounds-empty-name", "clip", "RuleSet.clip", "returned", null),
        new("clip.inplace", "clip", "RuleSet.clip", "returned", null),
        new("clip.reversed", "clip", "RuleSet.clip", "raised", "domain"),
        new("deepcopy.alias-topology", "deepcopy", "RuleSet.__deepcopy__", "returned", null),
        new("deepcopy.memo-hit", "deepcopy", "RuleSet.__deepcopy__", "returned", null),
        new("deepcopy.repeated", "deepcopy", "RuleSet.__deepcopy__", "returned", null),
        new("friday.clear", "slot", "RuleSet.friday", "returned", null),
        new("friday.explicit", "slot", "RuleSet.friday", "returned", null),
        new("friday.mixed-type", "slot", "RuleSet.friday", "raised", "domain"),
        new("from-constant.day-alias", "from-constant", "RuleSet.from_constant", "returned", null),
        new("from-constant.nonfinite", "from-constant", "RuleSet.from_constant", "raised", "domain"),
        new("from-constant.scalar-distinct", "from-constant", "RuleSet.from_constant", "returned", null),
        new("from-days.day-ignores-type", "from-days", "RuleSet.from_days", "returned", null),
        new("from-days.mixed-types", "from-days", "RuleSet.from_days", "raised", "domain"),
        new("from-days.scalar-overrides", "from-days", "RuleSet.from_days", "returned", null),
        new("get-dayschedule.integer-indices", "get-dayschedule", "RuleSet.get_dayschedule", "returned", null),
        new("get-dayschedule.invalid-index", "get-dayschedule", "RuleSet.get_dayschedule", "raised", "range"),
        new("get-dayschedule.string-fallback", "get-dayschedule", "RuleSet.get_dayschedule", "returned", null),
        new("holiday.clear", "slot", "RuleSet.holiday", "returned", null),
        new("holiday.explicit", "slot", "RuleSet.holiday", "returned", null),
        new("holiday.mixed-type", "slot", "RuleSet.holiday", "raised", "domain"),
        new("init.default-anonymous", "init", "RuleSet.__init__", "returned", null),
        new("init.explicit-padded", "init", "RuleSet.__init__", "returned", null),
        new("init.mixed-types", "init", "RuleSet.__init__", "raised", "domain"),
        new("max.defaults", "max", "RuleSet.max", "returned", null),
        new("max.override", "max", "RuleSet.max", "returned", null),
        new("max.signed-zero", "max", "RuleSet.max", "returned", null),
        new("min.defaults", "min", "RuleSet.min", "returned", null),
        new("min.override", "min", "RuleSet.min", "returned", null),
        new("min.signed-zero", "min", "RuleSet.min", "returned", null),
        new("monday.clear", "slot", "RuleSet.monday", "returned", null),
        new("monday.explicit", "slot", "RuleSet.monday", "returned", null),
        new("monday.mixed-type", "slot", "RuleSet.monday", "raised", "domain"),
        new("saturday.clear", "slot", "RuleSet.saturday", "returned", null),
        new("saturday.explicit", "slot", "RuleSet.saturday", "returned", null),
        new("saturday.mixed-type", "slot", "RuleSet.saturday", "raised", "domain"),
        new("summary.default-normalized", "summary", "RuleSet.summary", "returned", null),
        new("summary.exclude-days", "summary", "RuleSet.summary", "returned", null),
        new("summary.override-rich", "summary", "RuleSet.summary", "returned", null),
        new("sunday.clear", "slot", "RuleSet.sunday", "returned", null),
        new("sunday.explicit", "slot", "RuleSet.sunday", "returned", null),
        new("sunday.mixed-type", "slot", "RuleSet.sunday", "raised", "domain"),
        new("thursday.clear", "slot", "RuleSet.thursday", "returned", null),
        new("thursday.explicit", "slot", "RuleSet.thursday", "returned", null),
        new("thursday.mixed-type", "slot", "RuleSet.thursday", "raised", "domain"),
        new("to-dict.aliases", "to-dict", "RuleSet.to_dict", "returned", null),
        new("to-dict.nulls", "to-dict", "RuleSet.to_dict", "returned", null),
        new("to-dict.order", "to-dict", "RuleSet.to_dict", "returned", null),
        new("to-idf.defaults", "to-idf", "RuleSet.to_idf_compactexpr", "returned", null),
        new("to-idf.weekday-expansion", "to-idf", "RuleSet.to_idf_compactexpr", "returned", null),
        new("to-idf.weekend-holiday", "to-idf", "RuleSet.to_idf_compactexpr", "returned", null),
        new("tuesday.clear", "slot", "RuleSet.tuesday", "returned", null),
        new("tuesday.explicit", "slot", "RuleSet.tuesday", "returned", null),
        new("tuesday.mixed-type", "slot", "RuleSet.tuesday", "raised", "domain"),
        new("type.default-real", "type", "RuleSet.type", "returned", null),
        new("type.explicit-token", "type", "RuleSet.type", "returned", null),
        new("type.inferred-day", "type", "RuleSet.type", "returned", null),
        new("wednesday.clear", "slot", "RuleSet.wednesday", "returned", null),
        new("wednesday.explicit", "slot", "RuleSet.wednesday", "returned", null),
        new("wednesday.mixed-type", "slot", "RuleSet.wednesday", "raised", "domain"),
        new("weekdays.explicit", "slot", "RuleSet.weekdays", "returned", null),
        new("weekdays.mixed-type", "slot", "RuleSet.weekdays", "raised", "domain"),
        new("weekdays.replace", "slot", "RuleSet.weekdays", "returned", null),
        new("weekends.explicit", "slot", "RuleSet.weekends", "returned", null),
        new("weekends.mixed-type", "slot", "RuleSet.weekends", "raised", "domain"),
        new("weekends.replace", "slot", "RuleSet.weekends", "returned", null),
    };

    [Fact]
    public void ConstructorIsDeterministicNullSafeAndTypeAtomic()
    {
        RuleSet defaults = new(null);

        Assert.Equal("anonymous", defaults.Name);
        Assert.Equal(ScheduleType.Real, defaults.Type);
        Assert.Equal("anonymous:weekdays", defaults.Weekdays.Name);
        Assert.Equal("anonymous:weekends", defaults.Weekends.Name);
        Assert.NotSame(defaults.Weekdays, defaults.Weekends);
        Assert.All(defaults.Weekdays, value => Assert.Equal(0, value));
        Assert.All(defaults.Weekends, value => Assert.Equal(0, value));
        Assert.All(
            new[]
            {
                nameof(RuleSet.Weekdays),
                nameof(RuleSet.Weekends),
                nameof(RuleSet.Monday),
                nameof(RuleSet.Tuesday),
                nameof(RuleSet.Wednesday),
                nameof(RuleSet.Thursday),
                nameof(RuleSet.Friday),
                nameof(RuleSet.Saturday),
                nameof(RuleSet.Sunday),
                nameof(RuleSet.Holiday),
            },
            property => Assert.False(typeof(RuleSet).GetProperty(property)!.CanWrite));

        DaySchedule on = DaySchedule.FromConstant("on", 1, ScheduleType.OnOff);
        RuleSet padded = new("  padded  ", monday: on);
        Assert.Equal("padded", padded.Name);
        Assert.Equal(ScheduleType.OnOff, padded.Type);
        Assert.Throws<ArgumentException>(() => new RuleSet(" "));
        Assert.Throws<ArgumentException>(() => new RuleSet(
            "mixed",
            on,
            DaySchedule.FromConstant("real", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuleSet("invalid", type: (ScheduleType)12345));
    }

    [Fact]
    public void DeepCopySplitsAliasesAndPreservesNullTopology()
    {
        DaySchedule shared = DaySchedule.FromConstant("shared", 0.25, ScheduleType.Fraction);
        RuleSet source = new(
            "source",
            shared,
            shared,
            monday: shared,
            holiday: shared,
            type: ScheduleType.Fraction);

        RuleSet first = source.DeepCopy();
        RuleSet second = source.DeepCopy();

        Assert.Equal("source:COPY", first.Name);
        Assert.Equal(source.Type, first.Type);
        Assert.Equal(source.Weekdays.Values, first.Weekdays.Values);
        Assert.Equal("shared:COPY", first.Weekdays.Name);
        Assert.NotSame(source.Weekdays, first.Weekdays);
        Assert.NotSame(first.Weekdays, first.Weekends);
        Assert.NotSame(first.Weekdays, first.Monday);
        Assert.NotSame(first.Monday, first.Holiday);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Weekdays, second.Weekdays);
        Assert.Null(first.Tuesday);
        Assert.Null(first.Wednesday);
        Assert.Null(first.Thursday);
        Assert.Null(first.Friday);
        Assert.Null(first.Saturday);
        Assert.Null(first.Sunday);
    }

    [Fact]
    public void AsTypeIsImmutableSplitsAliasesAndFailsAtomically()
    {
        DaySchedule shared = DaySchedule.FromConstant("shared", 0.5);
        RuleSet source = new("source", shared, shared, monday: shared);

        RuleSet converted = source.AsType(ScheduleType.Fraction);

        Assert.Equal(ScheduleType.Fraction, converted.Type);
        Assert.Equal(ScheduleType.Real, source.Type);
        Assert.Same(shared, source.Weekdays);
        Assert.NotSame(converted.Weekdays, converted.Weekends);
        Assert.NotSame(converted.Weekdays, converted.Monday);
        Assert.All(converted.ToDictionary().Values.Where(day => day is not null),
            day => Assert.Equal(ScheduleType.Fraction, day!.Type));

        RuleSet partiallyConvertible = new(
            "partial",
            DaySchedule.FromConstant("valid", 0.5),
            DaySchedule.FromConstant("invalid", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            partiallyConvertible.AsType(ScheduleType.Fraction));
        Assert.Equal(ScheduleType.Real, partiallyConvertible.Type);
        Assert.Equal(0.5, partiallyConvertible.Weekdays[0]);
        Assert.Equal(2, partiallyConvertible.Weekends[0]);
    }

    [Fact]
    public void ClipUsesEmptyNameFallbackAndPreservesSignedZeroAndNulls()
    {
        DaySchedule signedPositiveZero = DaySchedule.FromConstant("positive", 0d);
        RuleSet source = new(
            "source",
            signedPositiveZero,
            DaySchedule.FromConstant("upper", 2d),
            monday: DaySchedule.FromConstant("lower", -2d));

        RuleSet clipped = source.Clip(-0d, 1d, string.Empty);

        Assert.Equal("source:CLIP", clipped.Name);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(clipped.Weekdays[0]));
        Assert.Equal(1, clipped.Weekends[0]);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(clipped.Monday![0]));
        Assert.Null(clipped.Tuesday);
        Assert.Same(signedPositiveZero, source.Weekdays);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(source.Weekdays[0]));

        Assert.Throws<ArgumentException>(() => source.Clip(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Clip(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.Clip(maximum: double.PositiveInfinity));
        Assert.Equal(2, source.Weekends[0]);
    }

    [Fact]
    public void FunctionalUpdatesCoverEverySlotAndRejectMixedTypes()
    {
        RuleSet source = RuleSet.FromConstant("source", 0, ScheduleType.OnOff);
        DaySchedule replacement = DaySchedule.FromConstant("replacement", 1, ScheduleType.OnOff);
        string[] keys =
        {
            "weekdays",
            "weekends",
            "monday",
            "tuesday",
            "wednesday",
            "thursday",
            "friday",
            "saturday",
            "sunday",
            "holiday",
        };

        foreach (string key in keys)
        {
            RuleSet updated = source.WithDaySchedule(key, replacement);

            Assert.Same(replacement, updated.ToDictionary()[key]);
            Assert.Equal("source", updated.Name);
            Assert.Equal(ScheduleType.OnOff, updated.Type);
            Assert.NotSame(source, updated);
            if (key == "weekdays" || key == "weekends")
            {
                Assert.NotSame(replacement, source.ToDictionary()[key]);
            }
            else
            {
                Assert.Null(source.ToDictionary()[key]);
                RuleSet cleared = updated.WithDaySchedule(key, null);
                Assert.Null(cleared.ToDictionary()[key]);
            }
        }

        Assert.Throws<ArgumentNullException>(() =>
            source.WithDaySchedule("weekdays", null));
        Assert.Throws<ArgumentNullException>(() =>
            source.WithDaySchedule("weekends", null));
        Assert.Throws<ArgumentException>(() => source.WithDaySchedule(
            "monday",
            DaySchedule.FromConstant("real", 1)));
        Assert.Throws<ArgumentException>(() =>
            source.WithDaySchedule("Monday", replacement));
    }

    [Fact]
    public void FromConstantPinsScalarAndDayAliasTopologies()
    {
        RuleSet scalar = RuleSet.FromConstant("scalar", true, ScheduleType.OnOff);

        Assert.Equal(ScheduleType.OnOff, scalar.Type);
        Assert.NotSame(scalar.Weekdays, scalar.Weekends);
        Assert.NotSame(scalar.Weekdays.Values, scalar.Weekends.Values);
        Assert.All(scalar.Weekdays, value => Assert.Equal(1, value));
        Assert.All(scalar.Weekends, value => Assert.Equal(1, value));

        DaySchedule temperature = DaySchedule.FromConstant(
            "temperature",
            20,
            ScheduleType.Temperature);
        RuleSet day = RuleSet.FromConstant("day", temperature, ScheduleType.Real);
        Assert.Equal(ScheduleType.Temperature, day.Type);
        Assert.Same(temperature, day.Weekdays);
        Assert.Same(temperature, day.Weekends);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleSet.FromConstant("nonfinite", double.NaN));
    }

    [Fact]
    public void FromDaysPinsDefaultAliasScalarOverrideAndTypedDaySemantics()
    {
        RuleSet scalar = RuleSet.FromDays(
            "scalar",
            0,
            monday: 1,
            tuesday: 1,
            holiday: true,
            type: ScheduleType.OnOff);

        Assert.Same(scalar.Weekdays, scalar.Weekends);
        Assert.NotSame(scalar.Monday, scalar.Tuesday);
        Assert.Equal(1, scalar.Monday![0]);
        Assert.Equal(1, scalar.Holiday![0]);
        Assert.Null(scalar.Wednesday);

        DaySchedule temperature = DaySchedule.FromConstant(
            "temperature",
            20,
            ScheduleType.Temperature);
        RuleSet typedDay = RuleSet.FromDays(
            "typed-day",
            temperature,
            type: ScheduleType.Real);
        Assert.Equal(ScheduleType.Temperature, typedDay.Type);
        Assert.Same(temperature, typedDay.Weekdays);
        Assert.Same(temperature, typedDay.Weekends);

        Assert.Throws<ArgumentException>(() => RuleSet.FromDays(
            "mixed",
            temperature,
            monday: DaySchedule.FromConstant("real", 1)));
    }

    [Fact]
    public void LookupSupportsStringsPythonIndicesFallbackAndRawNulls()
    {
        DaySchedule weekdays = DaySchedule.FromConstant("weekdays", 1);
        DaySchedule weekends = DaySchedule.FromConstant("weekends", 2);
        DaySchedule monday = DaySchedule.FromConstant("monday", 3);
        DaySchedule holiday = DaySchedule.FromConstant("holiday", 4);
        RuleSet rules = new(
            "lookup",
            weekdays,
            weekends,
            monday: monday,
            holiday: holiday);

        Assert.Same(monday, rules.GetDaySchedule("monday"));
        Assert.Same(weekdays, rules.GetDaySchedule("weekdays"));
        Assert.Same(weekdays, rules.GetDaySchedule("weekdays", fallback: false));
        Assert.Same(weekends, rules.GetDaySchedule("weekends"));
        Assert.Same(weekends, rules.GetDaySchedule("weekends", fallback: false));
        Assert.Same(weekdays, rules.GetDaySchedule("tuesday"));
        Assert.Null(rules.GetDaySchedule("tuesday", fallback: false));
        Assert.Same(weekends, rules.GetDaySchedule("sunday"));
        Assert.Same(holiday, rules.GetDaySchedule("holiday"));
        Assert.Same(monday, rules.GetDaySchedule(0));
        Assert.Same(monday, rules.GetDaySchedule(-8));
        Assert.Same(holiday, rules.GetDaySchedule(-1));
        Assert.Null(rules.GetDaySchedule(1, fallback: false));
        Assert.Same(monday, rules.GetDaySchedule(DayOfWeek.Monday));
        Assert.Same(holiday, rules.GetDaySchedule(DayOfWeek.Tuesday, isHoliday: true));

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetDaySchedule(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetDaySchedule(-9));
        Assert.Throws<ArgumentException>(() => rules.GetDaySchedule("workday"));
        Assert.Throws<ArgumentException>(() => rules.GetDaySchedule("Monday"));
    }

    [Fact]
    public void MinimumMaximumAndTypePreservePinnedSlotOrderAndSignedZero()
    {
        DaySchedule positiveZero = DaySchedule.FromConstant("positive", 0d);
        DaySchedule negativeZero = DaySchedule.FromConstant("negative", -0d);
        RuleSet firstPositive = new("positive-first", positiveZero, negativeZero);
        RuleSet firstNegative = new("negative-first", negativeZero, positiveZero);

        Assert.Equal(ScheduleType.Real, firstPositive.Type);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(firstPositive.Minimum));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(firstPositive.Maximum));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(firstNegative.Minimum));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(firstNegative.Maximum));

        RuleSet range = new(
            "range",
            DaySchedule.FromConstant("low", -5),
            DaySchedule.FromConstant("high", 7),
            monday: DaySchedule.FromConstant("override", -9));
        Assert.Equal(-9, range.Minimum);
        Assert.Equal(7, range.Maximum);
    }

    [Fact]
    public void ToDictionaryIsOrderedReadOnlyAndPreservesAliasesAndNulls()
    {
        DaySchedule shared = DaySchedule.FromConstant("shared", 1);
        RuleSet rules = new("dictionary", shared, shared, friday: shared);

        IReadOnlyDictionary<string, DaySchedule?> dictionary = rules.ToDictionary();

        Assert.Equal(
            new[]
            {
                "weekdays",
                "weekends",
                "monday",
                "tuesday",
                "wednesday",
                "thursday",
                "friday",
                "saturday",
                "sunday",
                "holiday",
            },
            dictionary.Keys);
        Assert.Same(dictionary["weekdays"], dictionary["weekends"]);
        Assert.Same(dictionary["weekdays"], dictionary["friday"]);
        Assert.Null(dictionary["monday"]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, DaySchedule?>)dictionary).Add("extra", shared));
    }

    [Fact]
    public void SummaryAndToStringMatchPinnedPythonTextExactly()
    {
        RuleSet rules = new(
            "rule's",
            DaySchedule.FromConstant("weekday", 1),
            DaySchedule.FromConstant("weekend", 2),
            monday: DaySchedule.FromConstant("monday", 3),
            holiday: DaySchedule.FromConstant("holiday", 4));
        const string header =
            "RuleSet \"rule's\" [type=real]\n"
            + "  range: min=1, max=4\n"
            + "  defaults: weekdays='weekday', weekends='weekend'\n"
            + "  overrides: monday, holiday";
        const string expected =
            header + "\n"
            + "  monday   : 'monday' (override, min=3, max=3)\n"
            + "  tuesday  : 'weekday' (fallback, min=1, max=1)\n"
            + "  wednesday: 'weekday' (fallback, min=1, max=1)\n"
            + "  thursday : 'weekday' (fallback, min=1, max=1)\n"
            + "  friday   : 'weekday' (fallback, min=1, max=1)\n"
            + "  saturday : 'weekend' (fallback, min=2, max=2)\n"
            + "  sunday   : 'weekend' (fallback, min=2, max=2)\n"
            + "  holiday  : 'holiday' (override, min=4, max=4)";

        Assert.Equal(header, rules.Summary(includeDays: false));
        Assert.Equal(expected, rules.Summary());
        Assert.Equal(expected, rules.ToString());
    }

    [Fact]
    public void ToIdfCompactExpressionMatchesSelectionExpansionAndIsReadOnly()
    {
        RuleSet defaults = new(
            "defaults",
            DaySchedule.FromConstant("weekday", 1),
            DaySchedule.FromConstant("weekend", 2));
        Assert.Equal(
            new[]
            {
                "For: Weekdays", "Until: 24:00", "1.0",
                "For: Weekends", "Until: 24:00", "2.0",
                "For: AllOtherDays", "Until: 24:00", "2.0",
            },
            defaults.ToIdfCompactExpression());

        RuleSet expanded = new(
            "expanded",
            DaySchedule.FromConstant("weekday", 1),
            DaySchedule.FromConstant("weekend", 2),
            monday: DaySchedule.FromConstant("monday", 3),
            saturday: DaySchedule.FromConstant("saturday", 4),
            holiday: DaySchedule.FromConstant("holiday", 5));
        IReadOnlyList<string> fields = expanded.ToIdfCompactExpression();

        Assert.Equal(
            new[]
            {
                "For: Monday", "Until: 24:00", "3.0",
                "For: Tuesday", "Until: 24:00", "1.0",
                "For: Wednesday", "Until: 24:00", "1.0",
                "For: Thursday", "Until: 24:00", "1.0",
                "For: Friday", "Until: 24:00", "1.0",
                "For: Saturday", "Until: 24:00", "4.0",
                "For: Sunday", "Until: 24:00", "2.0",
                "For: Holiday", "Until: 24:00", "5.0",
                "For: AllOtherDays", "Until: 24:00", "2.0",
            },
            fields);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)fields).Add("mutable"));
    }

    [Fact]
    public void MatchesPinnedPythonRuleSetCore()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(OracleSha256, sha256);
        Assert.Equal(OracleByteLength, bytes.Length);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo pinnedCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = pinnedCulture;
            CultureInfo.CurrentUICulture = pinnedCulture;

            using JsonDocument oracle = JsonDocument.Parse(bytes);
            JsonElement[] cases = ValidateCorpus(oracle.RootElement);
            var observations = new List<NativeObservation>(ExpectedCaseCount);
            for (int index = 0; index < cases.Length; index++)
            {
                CaseBinding binding = ExpectedCases[index];
                JsonElement pythonFacts = cases[index]
                    .GetProperty("python")
                    .GetProperty("facts");
                NativeCall call = ExecuteCase(binding, pythonFacts);
                SymbolContract symbol = Assert.Single(
                    ExpectedSymbols,
                    candidate => candidate.Symbol == binding.Symbol);
                observations.Add(new NativeObservation(
                    binding.CaseId,
                    binding.Symbol,
                    call.Outcome,
                    call.ErrorCategory,
                    symbol.AdaptationId,
                    call.Facts));
            }

            Assert.Equal(ExpectedCaseCount, observations.Count);
            foreach (EvidenceBinding evidence in ExpectedEvidence)
            {
                NativeObservation[] symbolObservations = observations
                    .Where(item => item.Symbol == evidence.Symbol)
                    .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(3, symbolObservations.Length);
                TrustedEvidenceRecorder.Record(
                    evidence.AssertionId,
                    EvidenceTestCase,
                    "not_applicable",
                    new
                    {
                        fixture = new
                        {
                            case_count = ExpectedCaseCount,
                            path = OracleRepositoryPath,
                            sha256,
                        },
                        observations = symbolObservations.Select(item => new
                        {
                            adaptation_id = item.Adaptation,
                            case_id = item.CaseId,
                            native_error_category = item.NativeErrorCategory,
                            native_facts = item.NativeFacts,
                            native_outcome = item.NativeOutcome,
                        }).ToArray(),
                        upstream_symbol = evidence.Symbol,
                    });
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
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
        Assert.False(
            Regex.IsMatch(
                root.GetRawText(),
                @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]+(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));

        JsonElement upstream = root.GetProperty("upstream");
        AssertKeys(upstream, "commit", "inventory_sha256", "path", "source_sha256");
        Assert.Equal(
            "847b01f68f438f560a986072bcaa7768fbf67897",
            RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
            RequiredString(upstream, "source_sha256"));

        JsonElement runtime = root.GetProperty("runtime");
        AssertKeys(
            runtime,
            "implementation",
            "python_hash_algorithm",
            "python_hash_seed",
            "python_hash_width_bits",
            "python_version");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));

        ValidateSymbols(root.GetProperty("symbols"));
        ValidateConsumerContract(root.GetProperty("consumer_contract"));

        JsonElement[] cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedCaseCount, cases.Length);
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).ToArray(),
            cases.Select(item => RequiredString(item, "id")).ToArray());
        Assert.Equal(
            ExpectedCaseCount,
            cases.Select(item => RequiredString(item, "id"))
                .Distinct(StringComparer.Ordinal)
                .Count());
        for (int index = 0; index < cases.Length; index++)
        {
            ValidateCase(cases[index], ExpectedCases[index]);
        }

        foreach (EvidenceBinding evidence in ExpectedEvidence)
        {
            string[] caseIds = ExpectedCases
                .Where(item => item.Symbol == evidence.Symbol)
                .Select(item => item.CaseId)
                .ToArray();
            Assert.Equal(3, caseIds.Length);
            Assert.Equal(
                caseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                caseIds);
        }

        Assert.Equal(CasesSha256, RequiredString(root, "cases_sha256"));
        Assert.Equal(CasesSha256, CanonicalSha256(root.GetProperty("cases")));
        return cases;
    }

    private static void ValidateSymbols(JsonElement symbolsElement)
    {
        JsonElement[] actual = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, actual.Length);
        Assert.Equal(ExpectedEvidence.Length, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            JsonElement item = actual[index];
            SymbolContract symbol = ExpectedSymbols[index];
            EvidenceBinding evidence = ExpectedEvidence[index];
            Assert.Equal(symbol.Symbol, evidence.Symbol);
            AssertKeys(
                item,
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash");
            Assert.Equal(symbol.BodyHash, RequiredString(item, "body_hash"));
            Assert.Equal(symbol.Kind, RequiredString(item, "kind"));
            Assert.Equal(UpstreamPath, RequiredString(item, "path"));
            Assert.Equal(symbol.SignatureHash, RequiredString(item, "signature_hash"));
            Assert.Equal(symbol.Symbol, RequiredString(item, "symbol"));
            Assert.Equal(evidence.SymbolHash, RequiredString(item, "symbol_hash"));
        }
    }

    private static void ValidateConsumerContract(JsonElement consumer)
    {
        AssertKeys(
            consumer,
            "adaptations",
            "case_count",
            "case_ids",
            "classifications",
            "float_encoding",
            "runtime_names",
            "target_symbols");
        Assert.Equal(ExpectedCaseCount, consumer.GetProperty("case_count").GetInt32());
        Assert.Equal(
            ExpectedCases.Select(item => item.CaseId).ToArray(),
            consumer.GetProperty("case_ids").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            ExpectedSymbols.Select(item => item.Symbol).ToArray(),
            consumer.GetProperty("target_symbols").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            "python-binary64-hex-without-0x-prefix",
            RequiredString(consumer, "float_encoding"));
        Assert.Equal(
            "policy-token-no-raw-address",
            RequiredString(consumer, "runtime_names"));

        JsonElement classifications = consumer.GetProperty("classifications");
        AssertKeys(classifications, ExpectedSymbols.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in ExpectedSymbols)
        {
            Assert.Equal(symbol.Classification, RequiredString(classifications, symbol.Symbol));
        }

        SymbolContract[] adapted = ExpectedSymbols
            .Where(item => item.AdaptationId is not null)
            .ToArray();
        JsonElement adaptations = consumer.GetProperty("adaptations");
        AssertKeys(adaptations, adapted.Select(item => item.Symbol).ToArray());
        foreach (SymbolContract symbol in adapted)
        {
            Assert.Equal(symbol.AdaptationId, RequiredString(adaptations, symbol.Symbol));
        }

        Assert.Equal(7, ExpectedSymbols.Count(item => item.Classification == "equivalent"));
        Assert.Equal(17, adapted.Length);
        Assert.Equal(17, adapted.Select(item => item.AdaptationId).Distinct().Count());
    }

    private static void ValidateCase(JsonElement item, CaseBinding binding)
    {
        SymbolContract symbol = Assert.Single(
            ExpectedSymbols,
            candidate => candidate.Symbol == binding.Symbol);
        bool adapted = symbol.AdaptationId is not null;
        AssertKeys(
            item,
            adapted
                ? new[] { "executor", "expected_dotnet", "id", "python", "symbol" }
                : new[] { "executor", "id", "python", "symbol" });
        Assert.Equal(binding.CaseId, RequiredString(item, "id"));
        Assert.Equal(binding.Executor, RequiredString(item, "executor"));
        Assert.Equal(binding.Symbol, RequiredString(item, "symbol"));

        JsonElement python = item.GetProperty("python");
        string pythonOutcome = RequiredString(python, "outcome");
        string? pythonErrorCategory = null;
        if (pythonOutcome == "returned")
        {
            AssertKeys(python, "facts", "outcome");
        }
        else
        {
            Assert.Equal("raised", pythonOutcome);
            AssertKeys(
                python,
                "error_category",
                "exception_type",
                "facts",
                "message",
                "outcome");
            pythonErrorCategory = RequiredString(python, "error_category");
            Assert.Contains(pythonErrorCategory, new[] { "domain", "range", "type" });
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(python, "exception_type")));
            _ = RequiredString(python, "message");
        }

        if (adapted)
        {
            JsonElement expected = item.GetProperty("expected_dotnet");
            AssertKeys(
                expected,
                binding.NativeOutcome == "raised"
                    ? new[] { "adaptation", "error_category", "outcome" }
                    : new[] { "adaptation", "outcome" });
            Assert.Equal(symbol.AdaptationId, RequiredString(expected, "adaptation"));
            Assert.Equal(binding.NativeOutcome, RequiredString(expected, "outcome"));
            if (binding.NativeOutcome == "raised")
            {
                Assert.Equal(
                    binding.NativeErrorCategory,
                    RequiredString(expected, "error_category"));
            }
            else
            {
                Assert.Null(binding.NativeErrorCategory);
            }
        }
        else
        {
            Assert.Equal("equivalent", symbol.Classification);
            Assert.Equal(pythonOutcome, binding.NativeOutcome);
            Assert.Equal(pythonErrorCategory, binding.NativeErrorCategory);
        }

        JsonElement facts = python.GetProperty("facts");
        Assert.Equal(JsonValueKind.Object, facts.ValueKind);
        ValidateFactNode(facts);
    }

    private static void ValidateFactNode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateFactNode(item);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            Assert.Contains(
                value.ValueKind,
                new[]
                {
                    JsonValueKind.False,
                    JsonValueKind.Null,
                    JsonValueKind.Number,
                    JsonValueKind.String,
                    JsonValueKind.True,
                });
            if (value.ValueKind == JsonValueKind.Number)
            {
                _ = value.GetInt64();
            }

            return;
        }

        AssertUniqueObjectKeys(value);
        if (value.TryGetProperty("kind", out JsonElement kindElement))
        {
            string kind = kindElement.GetString()!;
            if (kind == "binary64")
            {
                AssertKeys(value, "hex_without_prefix", "kind");
                Assert.Matches(
                    @"^-?(?:nan|inf|0\.0p\+0|0\.[0-9a-f]{13}p-1022|1\.[0-9a-f]{13}p[+-][0-9]+)$",
                    RequiredString(value, "hex_without_prefix"));
            }
            else if (kind == "schedule")
            {
                AssertKeys(value, "kind", "name", "schedule_type", "unit", "values");
                Assert.Contains(
                    RequiredString(value, "schedule_type"),
                    new[] { "fraction", "onoff", "real", "temperature" });
                Assert.Contains(
                    value.GetProperty("unit").ValueKind,
                    new[] { JsonValueKind.Null, JsonValueKind.String });
                ValidateNameDescriptor(value.GetProperty("name"));
                ValidateValuesDescriptor(value.GetProperty("values"));
            }
            else if (kind == "ruleset")
            {
                AssertKeys(value, "days", "kind", "name", "ruleset_type", "slots");
                Assert.Contains(
                    RequiredString(value, "ruleset_type"),
                    new[] { "fraction", "onoff", "real", "temperature" });
                ValidateNameDescriptor(value.GetProperty("name"));
                ValidateTopology(value.GetProperty("days"), value.GetProperty("slots"));
            }
            else
            {
                throw new Xunit.Sdk.XunitException(
                    $"Unknown RuleSet core fact kind '{kind}'.");
            }
        }
        else if (value.TryGetProperty("days", out JsonElement mappingDays)
            && value.TryGetProperty("keys", out JsonElement mappingKeys)
            && value.TryGetProperty("slots", out JsonElement mappingSlots))
        {
            AssertKeys(value, "days", "keys", "slots");
            Assert.Equal(SlotKeys, mappingKeys.EnumerateArray().Select(item => item.GetString()).ToArray());
            ValidateTopology(mappingDays, mappingSlots);
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            ValidateFactNode(property.Value);
        }
    }

    private static void ValidateTopology(JsonElement days, JsonElement slots)
    {
        Assert.Equal(JsonValueKind.Array, days.ValueKind);
        AssertKeys(slots, SlotKeys);
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in days.EnumerateArray())
        {
            AssertKeys(item, "reference", "schedule");
            string reference = RequiredString(item, "reference");
            Assert.Matches(@"^day-[0-9]{2}$", reference);
            Assert.True(references.Add(reference));
            JsonElement schedule = item.GetProperty("schedule");
            Assert.Equal("schedule", RequiredString(schedule, "kind"));
        }

        foreach (JsonProperty slot in slots.EnumerateObject())
        {
            if (slot.Value.ValueKind == JsonValueKind.Null)
            {
                Assert.DoesNotContain(slot.Name, new[] { "weekdays", "weekends" });
            }
            else
            {
                Assert.Contains(slot.Value.GetString()!, references);
            }
        }
    }

    private static void ValidateNameDescriptor(JsonElement value)
    {
        string namePolicy = RequiredString(value, "policy");
        if (namePolicy == "runtime-identity-hex")
        {
            AssertKeys(value, "policy");
            return;
        }

        Assert.Equal("literal", namePolicy);
        AssertKeys(value, "policy", "value");
        Assert.False(
            Regex.IsMatch(
                RequiredString(value, "value"),
                @"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]+(?![0-9A-Za-z])",
                RegexOptions.CultureInvariant));
    }

    private static void ValidateValuesDescriptor(JsonElement value)
    {
        string encoding = RequiredString(value, "encoding");
        Assert.Equal(DaySchedule.FixedLength, value.GetProperty("length").GetInt32());
        if (encoding == "repeat")
        {
            AssertKeys(value, "encoding", "length", "pattern");
            int count = value.GetProperty("pattern").GetArrayLength();
            Assert.InRange(count, 1, DaySchedule.FixedLength);
            Assert.Equal(0, DaySchedule.FixedLength % count);
            return;
        }

        Assert.Equal("full", encoding);
        AssertKeys(value, "encoding", "items", "length");
        Assert.Equal(DaySchedule.FixedLength, value.GetProperty("items").GetArrayLength());
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

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
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
                    $"Unsupported canonical JSON kind '{value.ValueKind}'.");
        }
    }

    private static void AssertUniqueObjectKeysRecursive(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            AssertUniqueObjectKeys(value);
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

    private static void AssertUniqueObjectKeys(JsonElement value)
    {
        string[] names = value.EnumerateObject().Select(item => item.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertKeys(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject()
            .Select(item => item.Name)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(item => item, StringComparer.Ordinal).ToArray(), actual);
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
            $"Could not locate repository file '{relativePath}'.");
    }

    private static NativeCall ExecuteCase(CaseBinding binding, JsonElement pythonFacts)
    {
        NativeCall call = binding.Executor switch
        {
            "astype" => ExecuteOracleAsType(binding.CaseId),
            "class" => ExecuteOracleClass(binding.CaseId),
            "clip" => ExecuteOracleClip(binding.CaseId),
            "deepcopy" => ExecuteOracleDeepCopy(binding.CaseId),
            "from-constant" => ExecuteOracleFromConstant(binding.CaseId),
            "from-days" => ExecuteOracleFromDays(binding.CaseId),
            "get-dayschedule" => ExecuteOracleGetDaySchedule(binding.CaseId, pythonFacts),
            "init" => ExecuteOracleInit(binding.CaseId),
            "max" => ExecuteOracleMetric(binding.CaseId, pythonFacts),
            "min" => ExecuteOracleMetric(binding.CaseId, pythonFacts),
            "slot" => ExecuteOracleSlot(binding.CaseId),
            "summary" => ExecuteOracleSummary(binding.CaseId, pythonFacts),
            "to-dict" => ExecuteOracleToDictionary(binding.CaseId, pythonFacts),
            "to-idf" => ExecuteOracleToIdf(binding.CaseId, pythonFacts),
            "type" => ExecuteOracleType(binding.CaseId, pythonFacts),
            _ => throw new Xunit.Sdk.XunitException(
                $"No native RuleSet core executor exists for '{binding.CaseId}'."),
        };

        Assert.Equal(binding.NativeOutcome, call.Outcome);
        Assert.Equal(binding.NativeErrorCategory, call.ErrorCategory);
        Assert.NotEmpty(call.Facts);
        Assert.All(call.Facts, fact => Assert.False(string.IsNullOrWhiteSpace(fact)));
        Assert.Equal(call.Facts.Length, call.Facts.Distinct(StringComparer.Ordinal).Count());
        return call;
    }

    private static NativeCall ExecuteOracleClass(string caseId)
    {
        if (caseId == "class.alias-topology")
        {
            DaySchedule shared = DaySchedule.FromConstant("shared", 1d);
            RuleSet result = new("alias", shared, shared, monday: shared);
            Assert.Same(shared, result.Weekdays);
            Assert.Same(shared, result.Weekends);
            Assert.Same(shared, result.Monday);
            return Returned("native RuleSet retained the three supplied shared slot references");
        }

        if (caseId == "class.mutable-slot")
        {
            RuleSet source = BaseOracleRuleSet();
            DaySchedule replacement = DaySchedule.FromConstant("monday", 4d);
            RuleSet result = source.WithDaySchedule("monday", replacement);
            Assert.Null(source.Monday);
            Assert.Same(replacement, result.Monday);
            Assert.NotSame(source, result);
            return Returned(
                "native functional slot update returned a fresh RuleSet",
                "native functional slot update retained the source null Monday");
        }

        Assert.Equal("class.slot-inventory", caseId);
        string[] propertyNames = SlotKeys
            .Select(key => char.ToUpperInvariant(key[0]) + key.Substring(1))
            .ToArray();
        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = typeof(RuleSet).GetProperty(propertyName)!;
            Assert.NotNull(property);
            Assert.False(property.CanWrite);
            Assert.True(typeof(DaySchedule).IsAssignableFrom(property.PropertyType)
                || Nullable.GetUnderlyingType(property.PropertyType) == typeof(DaySchedule));
        }

        MethodInfo update = Assert.Single(
            typeof(RuleSet).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name == nameof(RuleSet.WithDaySchedule));
        Assert.Equal(2, update.GetParameters().Length);
        return Returned(
            "native RuleSet exposes ten read-only day slot properties",
            "native RuleSet exposes one two-parameter functional slot updater");
    }

    private static NativeCall ExecuteOracleDeepCopy(string caseId)
    {
        if (caseId == "deepcopy.alias-topology")
        {
            DaySchedule shared = new(
                "shared",
                Enumerable.Repeat(1d, DaySchedule.FixedLength),
                ScheduleType.Real,
                "ratio");
            RuleSet source = new("source", shared, shared, monday: shared);
            RuleSet result = source.DeepCopy();
            Assert.NotSame(source, result);
            Assert.NotSame(source.Weekdays, result.Weekdays);
            Assert.NotSame(result.Weekdays, result.Weekends);
            Assert.NotSame(result.Weekdays, result.Monday);
            DaySchedule[] copiedDays =
            {
                result.Weekdays,
                result.Weekends,
                result.Monday!,
            };
            Assert.All(copiedDays, day =>
            {
                Assert.Equal("shared:COPY", day.Name);
                Assert.Equal(ScheduleType.Real, day.Type);
                Assert.Equal("ratio", day.Unit);
                Assert.Equal(shared.Values, day.Values);
                Assert.All(day, value => Assert.Equal(1d, value));
            });
            Assert.Equal("source:COPY", result.Name);
            return Returned(
                "native DeepCopy returned a fresh RuleSet and fresh day values",
                "native DeepCopy split source slot aliases per pinned topology");
        }

        if (caseId == "deepcopy.memo-hit")
        {
            MethodInfo method = Assert.Single(
                typeof(RuleSet).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                candidate => candidate.Name == nameof(RuleSet.DeepCopy));
            Assert.Empty(method.GetParameters());
            RuleSet source = BaseOracleRuleSet();
            RuleSet result = source.DeepCopy();
            Assert.NotSame(source, result);
            return Returned(
                "native DeepCopy intentionally has no caller memo parameter",
                "native parameterless DeepCopy returned a fresh immutable value");
        }

        Assert.Equal("deepcopy.repeated", caseId);
        RuleSet repeatedSource = BaseOracleRuleSet();
        RuleSet left = repeatedSource.DeepCopy();
        RuleSet right = repeatedSource.DeepCopy();
        Assert.NotSame(left, right);
        Assert.NotSame(left, repeatedSource);
        Assert.NotSame(right, repeatedSource);
        Assert.Equal(left.Weekdays.Values, right.Weekdays.Values);
        return Returned(
            "repeated native DeepCopy calls returned distinct RuleSets",
            "repeated native DeepCopy calls retained equal weekday values");
    }

    private static NativeCall ExecuteOracleInit(string caseId)
    {
        if (caseId == "init.default-anonymous")
        {
            RuleSet result = new(null);
            Assert.Equal("anonymous", result.Name);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.NotSame(result.Weekdays, result.Weekends);
            Assert.All(result.Weekdays, value => Assert.Equal(0d, value));
            Assert.All(result.Weekends, value => Assert.Equal(0d, value));
            Assert.All(SlotKeys.Skip(2), key => Assert.Null(GetOracleSlot(result, key)));
            return Returned(
                "native null-name construction used deterministic anonymous",
                "native default construction created distinct Real zero defaults");
        }

        if (caseId == "init.explicit-padded")
        {
            DaySchedule weekdays = new(
                "  weekday  ",
                Enumerable.Repeat(0.25d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "  ratio  ");
            DaySchedule weekends = new(
                "  weekend  ",
                Enumerable.Repeat(0.75d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "  ratio  ");
            RuleSet result = new("  rules  ", weekdays, weekends, type: ScheduleType.Fraction);
            Assert.Equal("rules", result.Name);
            Assert.Equal("weekday", result.Weekdays.Name);
            Assert.Equal("weekend", result.Weekends.Name);
            Assert.Equal("ratio", result.Weekdays.Unit);
            Assert.Equal(ScheduleType.Fraction, result.Type);
            return Returned(
                "native construction normalized padded RuleSet and day names",
                "native construction retained Fraction values and normalized ratio unit");
        }

        Assert.Equal("init.mixed-types", caseId);
        DaySchedule fraction = DaySchedule.FromConstant("weekday", 0.5d, ScheduleType.Fraction);
        DaySchedule temperature = DaySchedule.FromConstant("weekend", 20d, ScheduleType.Temperature);
        Assert.Throws<ArgumentException>(() => new RuleSet("mixed", fraction, temperature));
        Assert.Equal(ScheduleType.Fraction, fraction.Type);
        Assert.Equal(ScheduleType.Temperature, temperature.Type);
        return RaisedDomain(
            "native constructor rejected mixed day schedule types",
            "failed mixed construction left both supplied days unchanged");
    }

    private static NativeCall ExecuteOracleAsType(string caseId)
    {
        if (caseId == "astype.inplace-stale-type")
        {
            RuleSet source = RuleSet.FromConstant("typed", 0.5d, ScheduleType.Fraction);
            RuleSet result = source.AsType(ScheduleType.Real);
            Assert.NotSame(source, result);
            Assert.Equal(ScheduleType.Fraction, source.Type);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.All(source.Weekdays, value => Assert.Equal(0.5d, value));
            Assert.All(result.Weekdays, value => Assert.Equal(0.5d, value));
            return Returned(
                "native immutable AsType returned a fresh Real RuleSet",
                "native immutable AsType retained the Fraction source contract");
        }

        if (caseId == "astype.outplace-string")
        {
            DaySchedule shared = new(
                "shared",
                Enumerable.Repeat(0.5d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "ratio");
            RuleSet source = new("typed", shared, shared, monday: shared);
            RuleSet result = source.AsType(ScheduleType.Real);
            Assert.Equal(ScheduleType.Real, result.Type);
            Assert.Equal(ScheduleType.Fraction, source.Type);
            Assert.NotSame(result.Weekdays, result.Weekends);
            Assert.NotSame(result.Weekdays, result.Monday);
            Assert.All(
                new[] { result.Weekdays, result.Weekends, result.Monday! },
                day =>
                {
                    Assert.Equal("shared", day.Name);
                    Assert.Equal(ScheduleType.Real, day.Type);
                    Assert.Equal("ratio", day.Unit);
                    Assert.All(day, value => Assert.Equal(0.5d, value));
                });
            Assert.DoesNotContain(
                typeof(RuleSet).GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method => method.Name == nameof(RuleSet.AsType)
                    && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));
            return Returned(
                "native enum AsType converted every populated slot and split aliases",
                "native surface excludes unvalidated Python string type tokens");
        }

        Assert.Equal("astype.partial-failure", caseId);
        RuleSet partial = new(
            "partial",
            DaySchedule.FromConstant("weekday", 0.5d),
            DaySchedule.FromConstant("weekend", 2d));
        Assert.Throws<ArgumentOutOfRangeException>(() => partial.AsType(ScheduleType.Fraction));
        Assert.Equal(ScheduleType.Real, partial.Type);
        Assert.All(partial.Weekdays, value => Assert.Equal(0.5d, value));
        Assert.All(partial.Weekends, value => Assert.Equal(2d, value));
        return RaisedDomain(
            "native AsType rejected the non-Fraction weekend value",
            "failed native AsType retained both source defaults atomically");
    }

    private static NativeCall ExecuteOracleClip(string caseId)
    {
        RuleSet source = new(
            "source",
            new DaySchedule("weekday", RepeatOraclePattern(-2d, 2d), unit: "kW"),
            new DaySchedule("weekend", RepeatOraclePattern(3d, -3d), unit: "kW"));
        if (caseId == "clip.bounds-empty-name")
        {
            RuleSet result = source.Clip(-1d, 1d, string.Empty);
            Assert.Equal("source:CLIP", result.Name);
            Assert.Equal("weekday:CLIP", result.Weekdays.Name);
            Assert.Equal("weekend:CLIP", result.Weekends.Name);
            Assert.Equal("kW", result.Weekdays.Unit);
            Assert.Equal(
                RepeatOraclePattern(-1d, 1d),
                result.Weekdays.Values);
            Assert.Equal(
                RepeatOraclePattern(1d, -1d),
                result.Weekends.Values);
            Assert.Equal(RepeatOraclePattern(-2d, 2d), source.Weekdays.Values);
            return Returned(
                "native empty-name Clip used source:CLIP and clipped all 288 default values",
                "native Clip retained the complete source vectors and units");
        }

        if (caseId == "clip.inplace")
        {
            RuleSet result = source.Clip(-1d, 1d);
            Assert.NotSame(source, result);
            Assert.Equal(RepeatOraclePattern(-1d, 1d), result.Weekdays.Values);
            Assert.Equal(RepeatOraclePattern(1d, -1d), result.Weekends.Values);
            Assert.Equal(RepeatOraclePattern(-2d, 2d), source.Weekdays.Values);
            Assert.Equal(RepeatOraclePattern(3d, -3d), source.Weekends.Values);
            return Returned(
                "native immutable Clip returned a fresh clipped RuleSet",
                "native immutable Clip retained all source values");
        }

        Assert.Equal("clip.reversed", caseId);
        RuleSet reversed = BaseOracleRuleSet();
        Assert.Throws<ArgumentException>(() => reversed.Clip(3d, 1d));
        Assert.All(reversed.Weekdays, value => Assert.Equal(1d, value));
        Assert.All(reversed.Weekends, value => Assert.Equal(2d, value));
        return RaisedDomain(
            "native Clip rejected minimum 3 above maximum 1",
            "failed native Clip retained both source defaults");
    }

    private static NativeCall ExecuteOracleSlot(string caseId)
    {
        string[] parts = caseId.Split('.');
        Assert.Equal(2, parts.Length);
        string slot = parts[0];
        string mode = parts[1];
        Assert.Contains(slot, SlotKeys);
        RuleSet source = BaseOracleRuleSet();

        if (mode == "explicit")
        {
            DaySchedule explicitDay = DaySchedule.FromConstant($"{slot}-explicit", 3d);
            RuleSet result = source.WithDaySchedule(slot, explicitDay);
            Assert.Same(explicitDay, GetOracleSlot(result, slot));
            Assert.NotSame(source, result);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.Type, result.Type);
            return Returned(
                $"native {slot} functional update retained the supplied day reference",
                $"native {slot} functional update returned a fresh RuleSet");
        }

        if (mode == "clear")
        {
            Assert.DoesNotContain(slot, new[] { "weekdays", "weekends" });
            DaySchedule explicitDay = DaySchedule.FromConstant($"{slot}-explicit", 3d);
            RuleSet populated = source.WithDaySchedule(slot, explicitDay);
            RuleSet result = populated.WithDaySchedule(slot, null);
            Assert.Same(explicitDay, GetOracleSlot(populated, slot));
            Assert.Null(GetOracleSlot(result, slot));
            Assert.NotSame(populated, result);
            return Returned(
                $"native {slot} functional clear returned a fresh RuleSet",
                $"native {slot} functional clear retained the populated source");
        }

        if (mode == "replace")
        {
            Assert.Contains(slot, new[] { "weekdays", "weekends" });
            DaySchedule replacement = DaySchedule.FromConstant($"{slot}-replacement", 4d);
            RuleSet result = source.WithDaySchedule(slot, replacement);
            Assert.Same(replacement, GetOracleSlot(result, slot));
            Assert.NotSame(replacement, GetOracleSlot(source, slot));
            Assert.All(GetOracleSlot(result, slot)!, value => Assert.Equal(4d, value));
            return Returned(
                $"native required {slot} replacement retained the supplied day reference",
                $"native required {slot} replacement retained the source default");
        }

        Assert.Equal("mixed-type", mode);
        DaySchedule mixed = DaySchedule.FromConstant(
            $"{slot}-temperature",
            20d,
            ScheduleType.Temperature);
        Assert.Throws<ArgumentException>(() => source.WithDaySchedule(slot, mixed));
        Assert.Equal(ScheduleType.Real, source.Type);
        if (slot == "weekdays" || slot == "weekends")
        {
            Assert.Equal(ScheduleType.Real, GetOracleSlot(source, slot)!.Type);
        }
        else
        {
            Assert.Null(GetOracleSlot(source, slot));
        }

        return RaisedDomain(
            $"native {slot} functional update rejected a Temperature day on a Real RuleSet",
            $"failed native {slot} update retained the source slot topology");
    }

    private static NativeCall ExecuteOracleFromConstant(string caseId)
    {
        if (caseId == "from-constant.day-alias")
        {
            DaySchedule day = new(
                "shared",
                Enumerable.Repeat(0.75d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "ratio");
            RuleSet result = RuleSet.FromConstant(
                "day-alias",
                day,
                ScheduleType.Temperature);
            Assert.Same(day, result.Weekdays);
            Assert.Same(day, result.Weekends);
            Assert.Equal(ScheduleType.Fraction, result.Type);
            Assert.Equal("ratio", result.Weekdays.Unit);
            return Returned(
                "native typed-day FromConstant aliased both defaults to the input",
                "native typed-day FromConstant treated the day type as authoritative");
        }

        if (caseId == "from-constant.nonfinite")
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RuleSet.FromConstant("nonfinite", double.NaN, ScheduleType.Real));
            return RaisedDomain("native FromConstant rejected a non-finite Real scalar");
        }

        Assert.Equal("from-constant.scalar-distinct", caseId);
        RuleSet scalar = RuleSet.FromConstant(null, 2.5d, ScheduleType.Real);
        Assert.Equal("anonymous", scalar.Name);
        Assert.Equal("anonymous:weekdays", scalar.Weekdays.Name);
        Assert.Equal("anonymous:weekends", scalar.Weekends.Name);
        Assert.NotSame(scalar.Weekdays, scalar.Weekends);
        Assert.NotSame(scalar.Weekdays.Values, scalar.Weekends.Values);
        Assert.All(scalar.Weekdays, value => Assert.Equal(2.5d, value));
        Assert.All(scalar.Weekends, value => Assert.Equal(2.5d, value));
        return Returned(
            "native scalar FromConstant used deterministic anonymous naming",
            "native scalar FromConstant created distinct equal defaults");
    }

    private static NativeCall ExecuteOracleFromDays(string caseId)
    {
        if (caseId == "from-days.day-ignores-type")
        {
            DaySchedule defaultDay = new(
                "default",
                Enumerable.Repeat(0.25d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "ratio");
            DaySchedule friday = new(
                "friday",
                Enumerable.Repeat(0.75d, DaySchedule.FixedLength),
                ScheduleType.Fraction,
                "ratio");
            RuleSet result = RuleSet.FromDays(
                "typed-day",
                defaultDay,
                friday: friday,
                type: ScheduleType.Temperature);
            Assert.Same(defaultDay, result.Weekdays);
            Assert.Same(defaultDay, result.Weekends);
            Assert.Same(friday, result.Friday);
            Assert.Equal(ScheduleType.Fraction, result.Type);
            return Returned(
                "native typed-day FromDays aliased both defaults to the input",
                "native typed-day FromDays retained the typed Friday and ignored explicit type");
        }

        if (caseId == "from-days.mixed-types")
        {
            DaySchedule defaultDay = DaySchedule.FromConstant(
                "default",
                0.25d,
                ScheduleType.Fraction);
            DaySchedule monday = DaySchedule.FromConstant(
                "monday",
                20d,
                ScheduleType.Temperature);
            Assert.Throws<ArgumentException>(() => RuleSet.FromDays(
                "mixed",
                defaultDay,
                monday: monday));
            Assert.Equal(ScheduleType.Fraction, defaultDay.Type);
            Assert.Equal(ScheduleType.Temperature, monday.Type);
            return RaisedDomain(
                "native FromDays rejected a mixed-type override",
                "failed native FromDays retained both supplied day schedules");
        }

        Assert.Equal("from-days.scalar-overrides", caseId);
        RuleSet scalar = RuleSet.FromDays(
            "days",
            0.25d,
            monday: 0.75d,
            saturday: 1d,
            holiday: 0.5d,
            type: ScheduleType.Fraction);
        Assert.Same(scalar.Weekdays, scalar.Weekends);
        Assert.Equal("days:default", scalar.Weekdays.Name);
        Assert.Equal("days:monday", scalar.Monday!.Name);
        Assert.Equal("days:saturday", scalar.Saturday!.Name);
        Assert.Equal("days:holiday", scalar.Holiday!.Name);
        Assert.NotSame(scalar.Weekdays, scalar.Monday);
        Assert.NotSame(scalar.Monday, scalar.Saturday);
        Assert.NotSame(scalar.Saturday, scalar.Holiday);
        Assert.All(scalar.Weekdays, value => Assert.Equal(0.25d, value));
        Assert.All(scalar.Monday!, value => Assert.Equal(0.75d, value));
        Assert.All(scalar.Saturday!, value => Assert.Equal(1d, value));
        Assert.All(scalar.Holiday!, value => Assert.Equal(0.5d, value));
        return Returned(
            "native scalar FromDays shared one default across weekday and weekend",
            "native scalar FromDays created distinct validated override schedules");
    }

    private static NativeCall ExecuteOracleGetDaySchedule(
        string caseId,
        JsonElement pythonFacts)
    {
        if (caseId == "get-dayschedule.integer-indices")
        {
            RuleSet source = BaseOracleRuleSet();
            DaySchedule monday = DaySchedule.FromConstant("monday", 3d);
            DaySchedule holiday = DaySchedule.FromConstant("holiday", 4d);
            source = source
                .WithDaySchedule("monday", monday)
                .WithDaySchedule("holiday", holiday);
            var facts = new
            {
                index_0_is_monday = ReferenceEquals(source.GetDaySchedule(0), monday),
                index_7_is_holiday = ReferenceEquals(source.GetDaySchedule(7), holiday),
                negative_1_is_holiday = ReferenceEquals(source.GetDaySchedule(-1), holiday),
                negative_8_is_monday = ReferenceEquals(source.GetDaySchedule(-8), monday),
            };
            AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(facts));
            return Returned("native integer lookup matched indices -8 -1 0 and 7 exactly");
        }

        if (caseId == "get-dayschedule.invalid-index")
        {
            RuleSet source = BaseOracleRuleSet();
            Assert.Throws<ArgumentOutOfRangeException>(() => source.GetDaySchedule(8));
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new
                {
                    source = EncodeRuleSet(source),
                }));
            return RaisedRange(
                "native integer lookup rejected index 8",
                "failed native integer lookup retained the exact source topology");
        }

        Assert.Equal("get-dayschedule.string-fallback", caseId);
        RuleSet rules = BaseOracleRuleSet();
        DaySchedule tuesday = DaySchedule.FromConstant("tuesday", 3d);
        rules = rules.WithDaySchedule("tuesday", tuesday);
        var fallbackFacts = new
        {
            holiday_fallback_is_weekends = ReferenceEquals(
                rules.GetDaySchedule("holiday"),
                rules.Weekends),
            monday_fallback_is_weekdays = ReferenceEquals(
                rules.GetDaySchedule("monday"),
                rules.Weekdays),
            monday_raw_is_none = rules.GetDaySchedule("monday", fallback: false) is null,
            tuesday_explicit_is_input = ReferenceEquals(
                rules.GetDaySchedule("tuesday"),
                tuesday),
            weekdays_string_is_default = ReferenceEquals(
                rules.GetDaySchedule("weekdays"),
                rules.Weekdays),
        };
        AssertJsonEquivalent(pythonFacts, JsonSerializer.SerializeToElement(fallbackFacts));
        return Returned(
            "native string lookup matched explicit default and fallback identities",
            "native fallback=false lookup returned the raw null Monday override");
    }

    private static NativeCall ExecuteOracleMetric(string caseId, JsonElement pythonFacts)
    {
        bool maximum = caseId.StartsWith("max.", StringComparison.Ordinal);
        RuleSet source;
        if (caseId.EndsWith("defaults", StringComparison.Ordinal))
        {
            source = new(
                "range",
                DaySchedule.FromConstant("weekday", 1d),
                DaySchedule.FromConstant("weekend", 2d));
        }
        else if (caseId.EndsWith("override", StringComparison.Ordinal))
        {
            source = new(
                "range",
                DaySchedule.FromConstant("weekday", 1d),
                DaySchedule.FromConstant("weekend", 2d),
                monday: DaySchedule.FromConstant("monday", -5d),
                holiday: DaySchedule.FromConstant("holiday", 9d));
        }
        else
        {
            Assert.EndsWith("signed-zero", caseId, StringComparison.Ordinal);
            source = maximum
                ? new RuleSet(
                    "zero",
                    DaySchedule.FromConstant("weekday", -0d),
                    DaySchedule.FromConstant("weekend", 0d))
                : new RuleSet(
                    "zero",
                    DaySchedule.FromConstant("weekday", 0d),
                    DaySchedule.FromConstant("weekend", -0d));
        }

        double value = maximum ? source.Maximum : source.Minimum;
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new { value = EncodeBinary64(value) }));
        if (caseId == "max.signed-zero")
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(-0d),
                BitConverter.DoubleToInt64Bits(value));
        }
        else if (caseId == "min.signed-zero")
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(0d),
                BitConverter.DoubleToInt64Bits(value));
        }

        return Returned($"native {caseId} matched the pinned Python binary64 value exactly");
    }

    private static NativeCall ExecuteOracleSummary(string caseId, JsonElement pythonFacts)
    {
        string summary;
        if (caseId == "summary.default-normalized")
        {
            RuleSet defaults = new(null);
            summary = defaults.Summary()
                .Replace("'anonymous:weekdays'", "'<runtime-identity>'", StringComparison.Ordinal)
                .Replace("'anonymous:weekends'", "'<runtime-identity>'", StringComparison.Ordinal)
                .Replace("'anonymous'", "'<runtime-identity>'", StringComparison.Ordinal);
            Assert.DoesNotContain("anonymous", summary, StringComparison.Ordinal);
            Assert.Contains("<runtime-identity>", summary, StringComparison.Ordinal);
        }
        else if (caseId == "summary.exclude-days")
        {
            summary = BaseOracleRuleSet().Summary(includeDays: false);
        }
        else
        {
            Assert.Equal("summary.override-rich", caseId);
            RuleSet rich = new(
                "a'b",
                DaySchedule.FromConstant("weekday", 1.23456d, ScheduleType.Real),
                DaySchedule.FromConstant("weekend", -0.000012345d, ScheduleType.Real),
                monday: DaySchedule.FromConstant("monday", 10_000d, ScheduleType.Real),
                holiday: DaySchedule.FromConstant("holiday", -2d, ScheduleType.Real));
            summary = rich.Summary(includeDays: true);
            Assert.Contains("min=1.235", summary, StringComparison.Ordinal);
            Assert.Contains("min=-1.234e-05", summary, StringComparison.Ordinal);
            Assert.Contains("max=1e+04", summary, StringComparison.Ordinal);
        }

        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new { summary }));
        return Returned($"native {caseId} text matched the normalized pinned summary exactly");
    }

    private static NativeCall ExecuteOracleToDictionary(
        string caseId,
        JsonElement pythonFacts)
    {
        if (caseId == "to-dict.order")
        {
            IReadOnlyDictionary<string, DaySchedule?> mapping = BaseOracleRuleSet()
                .ToDictionary();
            Assert.Equal(SlotKeys, mapping.Keys);
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new { keys = mapping.Keys.ToArray() }));
            return Returned("native ToDictionary matched the exact ten-key Python order");
        }

        IReadOnlyDictionary<string, DaySchedule?> dictionary;
        if (caseId == "to-dict.nulls")
        {
            dictionary = BaseOracleRuleSet().ToDictionary();
            Assert.All(SlotKeys.Skip(2), key => Assert.Null(dictionary[key]));
        }
        else
        {
            Assert.Equal("to-dict.aliases", caseId);
            DaySchedule shared = DaySchedule.FromConstant("shared", 1d);
            dictionary = new RuleSet(
                "alias",
                shared,
                shared,
                monday: shared,
                holiday: shared).ToDictionary();
            Assert.Same(dictionary["weekdays"], dictionary["weekends"]);
            Assert.Same(dictionary["weekdays"], dictionary["monday"]);
            Assert.Same(dictionary["weekdays"], dictionary["holiday"]);
        }

        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new
            {
                mapping = EncodeMapping(dictionary),
            }));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, DaySchedule?>)dictionary).Add(
                "extra",
                DaySchedule.FromConstant("extra", 0d)));
        return Returned($"native {caseId} matched pinned keys nulls values and alias topology");
    }

    private static NativeCall ExecuteOracleToIdf(string caseId, JsonElement pythonFacts)
    {
        RuleSet source;
        if (caseId == "to-idf.defaults")
        {
            source = BaseOracleRuleSet();
        }
        else if (caseId == "to-idf.weekday-expansion")
        {
            source = BaseOracleRuleSet().WithDaySchedule(
                "wednesday",
                DaySchedule.FromConstant("wednesday", 3d));
        }
        else
        {
            Assert.Equal("to-idf.weekend-holiday", caseId);
            source = new RuleSet(
                "idf",
                DaySchedule.FromConstant("weekday", 1d),
                DaySchedule.FromConstant("weekend", -0d),
                saturday: DaySchedule.FromConstant("saturday", 2d),
                holiday: DaySchedule.FromConstant("holiday", 3d));
        }

        IReadOnlyList<string> fields = source.ToIdfCompactExpression();
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new { fields = fields.ToArray() }));
        Assert.Equal("For: AllOtherDays", fields[fields.Count - 3]);
        if (caseId == "to-idf.weekend-holiday")
        {
            Assert.Contains("-0.0", fields);
            Assert.Equal(
                new[]
                {
                    "For: Weekdays",
                    "For: Saturday",
                    "For: Sunday",
                    "For: Holiday",
                    "For: AllOtherDays",
                },
                fields.Where(item => item.StartsWith("For: ", StringComparison.Ordinal)));
        }

        return Returned($"native {caseId} fields matched the pinned compact IDF order and values");
    }

    private static NativeCall ExecuteOracleType(string caseId, JsonElement pythonFacts)
    {
        if (caseId == "type.default-real")
        {
            RuleSet result = new("default");
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new { type = CanonicalType(result.Type) }));
            Assert.False(typeof(RuleSet).GetProperty(nameof(RuleSet.Type))!.CanWrite);
            return Returned("native default RuleSet exposed read-only Real type");
        }

        if (caseId == "type.explicit-token")
        {
            RuleSet result = new("typed", type: ScheduleType.Temperature);
            AssertJsonEquivalent(
                pythonFacts,
                JsonSerializer.SerializeToElement(new { type = CanonicalType(result.Type) }));
            Assert.Equal(ScheduleType.Temperature, result.Weekdays.Type);
            Assert.Equal(ScheduleType.Temperature, result.Weekends.Type);
            return Returned("native explicit Temperature enum propagated to both generated defaults");
        }

        Assert.Equal("type.inferred-day", caseId);
        DaySchedule weekend = DaySchedule.FromConstant(
            "weekend",
            20d,
            ScheduleType.Temperature);
        RuleSet inferred = new("inferred", weekends: weekend);
        Assert.Equal(ScheduleType.Temperature, inferred.Type);
        Assert.Equal(ScheduleType.Temperature, inferred.Weekdays.Type);
        Assert.Same(weekend, inferred.Weekends);
        AssertJsonEquivalent(
            pythonFacts,
            JsonSerializer.SerializeToElement(new
            {
                result = EncodeRuleSet(
                    inferred,
                    runtimeNameDays: new[] { inferred.Weekdays }),
                type = CanonicalType(inferred.Type),
            }));
        return Returned(
            "native RuleSet inferred Temperature from the supplied weekend",
            "native inferred type propagated to a deterministic generated weekday default");
    }

    private static RuleSet BaseOracleRuleSet()
    {
        return new RuleSet(
            "rules",
            DaySchedule.FromConstant("weekday", 1d),
            DaySchedule.FromConstant("weekend", 2d));
    }

    private static DaySchedule? GetOracleSlot(RuleSet value, string key)
    {
        return key switch
        {
            "weekdays" => value.Weekdays,
            "weekends" => value.Weekends,
            "monday" => value.Monday,
            "tuesday" => value.Tuesday,
            "wednesday" => value.Wednesday,
            "thursday" => value.Thursday,
            "friday" => value.Friday,
            "saturday" => value.Saturday,
            "sunday" => value.Sunday,
            "holiday" => value.Holiday,
            _ => throw new Xunit.Sdk.XunitException($"Unknown native slot '{key}'."),
        };
    }

    private static double[] RepeatOraclePattern(params double[] pattern)
    {
        Assert.NotEmpty(pattern);
        return Enumerable.Range(0, DaySchedule.FixedLength)
            .Select(index => pattern[index % pattern.Length])
            .ToArray();
    }

    private static object EncodeRuleSet(
        RuleSet value,
        bool runtimeName = false,
        IReadOnlyCollection<DaySchedule>? runtimeNameDays = null)
    {
        IReadOnlyDictionary<string, DaySchedule?> mapping = value.ToDictionary();
        var references = new List<DayReference>();
        var slots = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (string key in SlotKeys)
        {
            DaySchedule? day = mapping[key];
            if (day is null)
            {
                slots[key] = null;
                continue;
            }

            DayReference? existing = references.FirstOrDefault(
                item => ReferenceEquals(item.Day, day));
            if (existing is null)
            {
                existing = new DayReference($"day-{references.Count + 1:D2}", day);
                references.Add(existing);
            }

            slots[key] = existing.Reference;
        }

        return new
        {
            days = references.Select(item => new
            {
                reference = item.Reference,
                schedule = EncodeDaySchedule(
                    item.Day,
                    runtimeNameDays?.Any(day => ReferenceEquals(day, item.Day)) == true),
            }).ToArray(),
            kind = "ruleset",
            name = EncodeName(value.Name, runtimeName),
            ruleset_type = CanonicalType(value.Type),
            slots,
        };
    }

    private static object EncodeMapping(IReadOnlyDictionary<string, DaySchedule?> mapping)
    {
        var references = new List<DayReference>();
        var slots = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, DaySchedule?> pair in mapping)
        {
            if (pair.Value is null)
            {
                slots[pair.Key] = null;
                continue;
            }

            DayReference? existing = references.FirstOrDefault(
                item => ReferenceEquals(item.Day, pair.Value));
            if (existing is null)
            {
                existing = new DayReference(
                    $"day-{references.Count + 1:D2}",
                    pair.Value);
                references.Add(existing);
            }

            slots[pair.Key] = existing.Reference;
        }

        return new
        {
            days = references.Select(item => new
            {
                reference = item.Reference,
                schedule = EncodeDaySchedule(item.Day),
            }).ToArray(),
            keys = mapping.Keys.ToArray(),
            slots,
        };
    }

    private static object EncodeDaySchedule(DaySchedule value, bool runtimeName = false)
    {
        return new
        {
            kind = "schedule",
            name = EncodeName(value.Name, runtimeName),
            schedule_type = CanonicalType(value.Type),
            unit = value.Unit,
            values = EncodeValues(value.Values),
        };
    }

    private static object EncodeName(string value, bool runtimeName)
    {
        return runtimeName
            ? (object)new { policy = "runtime-identity-hex" }
            : new { policy = "literal", value };
    }

    private static object EncodeValues(IReadOnlyList<double> values)
    {
        Assert.Equal(DaySchedule.FixedLength, values.Count);
        int patternLength = DaySchedule.FixedLength;
        foreach (int candidate in Enumerable.Range(1, DaySchedule.FixedLength))
        {
            if (DaySchedule.FixedLength % candidate != 0)
            {
                continue;
            }

            bool repeated = Enumerable.Range(0, DaySchedule.FixedLength)
                .All(index => BitConverter.DoubleToInt64Bits(values[index])
                    == BitConverter.DoubleToInt64Bits(values[index % candidate]));
            if (repeated)
            {
                patternLength = candidate;
                break;
            }
        }

        return new
        {
            encoding = "repeat",
            length = DaySchedule.FixedLength,
            pattern = values.Take(patternLength).Select(EncodeBinary64).ToArray(),
        };
    }

    private static object EncodeBinary64(double value) => new
    {
        hex_without_prefix = ToPythonHexWithoutPrefix(value),
        kind = "binary64",
    };

    private static string ToPythonHexWithoutPrefix(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        long signedBits = BitConverter.DoubleToInt64Bits(value);
        bool negative = signedBits < 0;
        ulong magnitude = unchecked((ulong)signedBits) & 0x7fff_ffff_ffff_ffffUL;
        string sign = negative ? "-" : string.Empty;
        if (magnitude == 0)
        {
            return $"{sign}0.0p+0";
        }

        int exponentBits = (int)((magnitude >> 52) & 0x7ffUL);
        ulong fraction = magnitude & 0x000f_ffff_ffff_ffffUL;
        if (exponentBits == 0)
        {
            return $"{sign}0.{fraction:x13}p-1022";
        }

        int exponent = exponentBits - 1023;
        return $"{sign}1.{fraction:x13}p{(exponent >= 0 ? "+" : string.Empty)}{exponent}";
    }

    private static string CanonicalType(ScheduleType type)
    {
        return type switch
        {
            ScheduleType.Fraction => "fraction",
            ScheduleType.OnOff => "onoff",
            ScheduleType.Real => "real",
            ScheduleType.Temperature => "temperature",
            _ => throw new Xunit.Sdk.XunitException($"Unknown native schedule type '{type}'."),
        };
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] expectedProperties = expected.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal)
                    .ToArray();
                JsonProperty[] actualProperties = actual.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(
                    expectedProperties.Select(item => item.Name),
                    actualProperties.Select(item => item.Name));
                for (int index = 0; index < expectedProperties.Length; index++)
                {
                    AssertJsonEquivalent(
                        expectedProperties[index].Value,
                        actualProperties[index].Value);
                }

                break;
            case JsonValueKind.Array:
                JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                Assert.Equal(expectedItems.Length, actualItems.Length);
                for (int index = 0; index < expectedItems.Length; index++)
                {
                    AssertJsonEquivalent(expectedItems[index], actualItems[index]);
                }

                break;
            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;
            case JsonValueKind.Number:
                Assert.Equal(expected.GetRawText(), actual.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;
            case JsonValueKind.Null:
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"Unsupported JSON fact kind '{expected.ValueKind}'.");
        }
    }

    private static NativeCall Returned(params string[] facts) =>
        new("returned", null, facts);

    private static NativeCall RaisedDomain(params string[] facts) =>
        new("raised", "domain", facts);

    private static NativeCall RaisedRange(params string[] facts) =>
        new("raised", "range", facts);

    private sealed record EvidenceBinding(
        string Symbol,
        string SymbolHash,
        string AssertionId);

    private sealed record SymbolContract(
        string Symbol,
        string Kind,
        string SignatureHash,
        string BodyHash,
        string Classification,
        string? AdaptationId);

    private sealed record CaseBinding(
        string CaseId,
        string Executor,
        string Symbol,
        string NativeOutcome,
        string? NativeErrorCategory);

    private sealed record NativeCall(
        string Outcome,
        string? ErrorCategory,
        string[] Facts);

    private sealed record NativeObservation(
        string CaseId,
        string Symbol,
        string NativeOutcome,
        string? NativeErrorCategory,
        string? Adaptation,
        string[] NativeFacts);

    private sealed record DayReference(string Reference, DaySchedule Day);
}
