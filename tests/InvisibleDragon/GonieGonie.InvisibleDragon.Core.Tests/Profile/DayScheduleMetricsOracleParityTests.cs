using System.Security.Cryptography;
using System.Text.Json;
using GonieGonie.InvisibleDragon.Profile;
using GonieGonie.UpstreamTracker;

namespace GonieGonie.InvisibleDragon.Tests.Profile;

public sealed class DayScheduleMetricsOracleParityTests
{
    private const string OracleRepositoryPath =
        "fixtures/reference/python-0.7.0/day-schedule-metrics-oracle.json";
    private const string OracleSha256 =
        "sha256:45ef72b5561dd159859cf9c295ee5652450f3c37d213bfcb81f1ade79c5967b5";
    private const string EvidenceTestCase =
        "GonieGonie.InvisibleDragon.Tests.Profile.DayScheduleMetricsOracleParityTests.MatchesPinnedPythonMetrics";
    private const string UpstreamPath = "src/idragon/dragon/profile.py";
    private static readonly (string Symbol, string Hash)[] ExpectedSymbols =
    {
        ("DaySchedule.DATA_INTERVAL", "sha256:b53131ccec072b1290838381677697006a0c9cec22aff1882fab4a59bdc8c30a"),
        ("DaySchedule.average", "sha256:55bc4967765bbee28662c491439fa2c95a4e4128bc1660284502b31b05b24d52"),
        ("DaySchedule.fixed_length", "sha256:a353188fed7223a24e31fe0968cb7cdfb191fc779087fd849e018ff42c2d52ea"),
        ("DaySchedule.has_nonzero", "sha256:8e7daa8fe6a78bc181c23cc1205b8c0717384320ca337c102e5c89b2bc9d0181"),
        ("DaySchedule.has_positive", "sha256:84c867d2b8c3d24aba67c0370e844f3971a81106a53326d844531f8c93b6d603"),
        ("DaySchedule.integral", "sha256:cd5749889d0a405f8786818089df75dbae0c53b8c0b994da7cd59c318169576b"),
        ("DaySchedule.is_constant", "sha256:48c772e45f4c329dffcfccd76d09f8fb8e58b954461263a96d98904af1378f4e"),
        ("DaySchedule.max", "sha256:44f90344e50ce247c439c80440ca0797761507ae8316848df4d7bdf7b4a4b67f"),
        ("DaySchedule.min", "sha256:ed9f11bd1e07b0841a20631e20de665591c7ea818a3a70f062825983e7bf4d01"),
        ("DaySchedule.nonzero_hours", "sha256:f4c71d3aea51cdd689527156a6824982c7f39c9057525782f980033a2ded25b2"),
        ("DaySchedule.positive_average", "sha256:630219d623c8d9761eadafe6bd27ed6bee3aa7e0d96dc4d6b8acbe24d1c7d819"),
        ("DaySchedule.positive_hours", "sha256:8408d1e02b37da212885c01f74a5001985c58b4102c44d6391729ddcb148e622"),
        ("DaySchedule.step_in_hours", "sha256:8f0c0fc9d2013fb3c88672e86d6bba893a91d01467f9d481ee740379e729f0b3"),
    };

    [Fact]
    public void MatchesPinnedPythonMetrics()
    {
        string path = FindRepositoryFile(OracleRepositoryPath);
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(OracleSha256, sha256);

        using JsonDocument oracle = JsonDocument.Parse(bytes);
        JsonElement root = oracle.RootElement;
        Assert.Equal(
            "goniegonie.invisibledragon.day-schedule-metrics-oracle.v1",
            RequiredString(root, "schema"));
        JsonElement upstream = root.GetProperty("upstream");
        Assert.Equal("847b01f68f438f560a986072bcaa7768fbf67897", RequiredString(upstream, "commit"));
        Assert.Equal(
            "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0",
            RequiredString(upstream, "inventory_sha256"));
        Assert.Equal(UpstreamPath, RequiredString(upstream, "path"));
        Assert.Equal(
            "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
            RequiredString(upstream, "source_sha256"));

        JsonElement runtime = root.GetProperty("runtime");
        Assert.Equal("cpython", RequiredString(runtime, "implementation"));
        Assert.Equal("3.12.7", RequiredString(runtime, "python_version"));
        Assert.Equal(0, runtime.GetProperty("python_hash_seed").GetInt32());
        Assert.Equal("siphash13", RequiredString(runtime, "python_hash_algorithm"));
        Assert.Equal(64, runtime.GetProperty("python_hash_width_bits").GetInt32());
        AssertPinnedSymbols(root.GetProperty("symbols"));

        JsonElement classObservations = root.GetProperty("class_observations");
        int pythonDataInterval = classObservations.GetProperty("data_interval").GetInt32();
        int pythonFixedLength = classObservations.GetProperty("fixed_length").GetInt32();
        double pythonStepInHours = classObservations.GetProperty("step_in_hours").GetDouble();
        Assert.Equal(DaySchedule.IntervalsPerHour, pythonDataInterval);
        Assert.Equal(DaySchedule.FixedLength, pythonFixedLength);
        Assert.Equal(pythonStepInHours, DaySchedule.Step.TotalHours);

        JsonElement[] caseElements = root.GetProperty("cases").EnumerateArray().ToArray();
        string[] caseIds = caseElements.Select(item => RequiredString(item, "id")).ToArray();
        Assert.Equal(8, caseElements.Length);
        Assert.Equal(caseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), caseIds);
        Assert.Equal(caseIds.Length, caseIds.Distinct(StringComparer.Ordinal).Count());

        List<MetricObservation> observations = new();
        foreach (JsonElement item in caseElements)
        {
            string caseId = RequiredString(item, "id");
            double[] values = item.GetProperty("values")
                .EnumerateArray()
                .Select(value => value.GetDouble())
                .ToArray();
            Assert.Equal(DaySchedule.FixedLength, values.Length);
            DaySchedule schedule = new(caseId, values, ScheduleType.Real);
            JsonElement expected = item.GetProperty("observations");
            MetricObservation observation = new(
                caseId,
                expected.GetProperty("average").GetDouble(),
                schedule.Average,
                expected.GetProperty("has_nonzero").GetBoolean(),
                schedule.HasNonzero,
                expected.GetProperty("has_positive").GetBoolean(),
                schedule.HasPositive,
                expected.GetProperty("integral").GetDouble(),
                schedule.IntegralHours,
                expected.GetProperty("is_constant").GetBoolean(),
                schedule.IsConstant,
                expected.GetProperty("max").GetDouble(),
                schedule.Maximum,
                expected.GetProperty("min").GetDouble(),
                schedule.Minimum,
                expected.GetProperty("nonzero_hours").GetDouble(),
                schedule.NonzeroHours,
                expected.GetProperty("positive_average").GetDouble(),
                schedule.PositiveAverage,
                expected.GetProperty("positive_hours").GetDouble(),
                schedule.PositiveHours);
            AssertObservationEqual(observation);
            observations.Add(observation);
        }

        var fixture = new { path = OracleRepositoryPath, sha256 };
        RecordConstant(
            "profile-dayschedule-data-interval-b53131cc",
            "DaySchedule.DATA_INTERVAL",
            pythonDataInterval,
            DaySchedule.IntervalsPerHour,
            fixture);
        RecordMetric(
            "profile-dayschedule-average-55bc4967",
            "DaySchedule.average",
            observations,
            item => item.PythonAverage,
            item => item.DotnetAverage,
            fixture);
        RecordConstant(
            "profile-dayschedule-fixed-length-a353188f",
            "DaySchedule.fixed_length",
            pythonFixedLength,
            DaySchedule.FixedLength,
            fixture);
        RecordMetric(
            "profile-dayschedule-has-nonzero-8e7daa8f",
            "DaySchedule.has_nonzero",
            observations,
            item => item.PythonHasNonzero,
            item => item.DotnetHasNonzero,
            fixture);
        RecordMetric(
            "profile-dayschedule-has-positive-84c867d2",
            "DaySchedule.has_positive",
            observations,
            item => item.PythonHasPositive,
            item => item.DotnetHasPositive,
            fixture);
        RecordMetric(
            "profile-dayschedule-integral-cd574988",
            "DaySchedule.integral",
            observations,
            item => item.PythonIntegral,
            item => item.DotnetIntegral,
            fixture);
        RecordMetric(
            "profile-dayschedule-is-constant-48c772e4",
            "DaySchedule.is_constant",
            observations,
            item => item.PythonIsConstant,
            item => item.DotnetIsConstant,
            fixture);
        RecordMetric(
            "profile-dayschedule-max-44f90344",
            "DaySchedule.max",
            observations,
            item => item.PythonMaximum,
            item => item.DotnetMaximum,
            fixture);
        RecordMetric(
            "profile-dayschedule-min-ed9f11bd",
            "DaySchedule.min",
            observations,
            item => item.PythonMinimum,
            item => item.DotnetMinimum,
            fixture);
        RecordMetric(
            "profile-dayschedule-nonzero-hours-f4c71d3a",
            "DaySchedule.nonzero_hours",
            observations,
            item => item.PythonNonzeroHours,
            item => item.DotnetNonzeroHours,
            fixture);
        RecordMetric(
            "profile-dayschedule-positive-average-630219d6",
            "DaySchedule.positive_average",
            observations,
            item => item.PythonPositiveAverage,
            item => item.DotnetPositiveAverage,
            fixture);
        RecordMetric(
            "profile-dayschedule-positive-hours-8408d1e0",
            "DaySchedule.positive_hours",
            observations,
            item => item.PythonPositiveHours,
            item => item.DotnetPositiveHours,
            fixture);
        RecordConstant(
            "profile-dayschedule-step-in-hours-8f0c0fc9",
            "DaySchedule.step_in_hours",
            pythonStepInHours,
            DaySchedule.Step.TotalHours,
            fixture);
    }

    private static void AssertPinnedSymbols(JsonElement symbolsElement)
    {
        JsonElement[] symbols = symbolsElement.EnumerateArray().ToArray();
        Assert.Equal(ExpectedSymbols.Length, symbols.Length);
        for (int index = 0; index < ExpectedSymbols.Length; index++)
        {
            Assert.Equal(UpstreamPath, RequiredString(symbols[index], "path"));
            Assert.Equal(ExpectedSymbols[index].Symbol, RequiredString(symbols[index], "symbol"));
            Assert.Equal(ExpectedSymbols[index].Hash, RequiredString(symbols[index], "symbol_hash"));
        }
    }

    private static void AssertObservationEqual(MetricObservation item)
    {
        Assert.Equal(item.PythonAverage, item.DotnetAverage);
        Assert.Equal(item.PythonHasNonzero, item.DotnetHasNonzero);
        Assert.Equal(item.PythonHasPositive, item.DotnetHasPositive);
        Assert.Equal(item.PythonIntegral, item.DotnetIntegral);
        Assert.Equal(item.PythonIsConstant, item.DotnetIsConstant);
        Assert.Equal(item.PythonMaximum, item.DotnetMaximum);
        Assert.Equal(item.PythonMinimum, item.DotnetMinimum);
        Assert.Equal(item.PythonNonzeroHours, item.DotnetNonzeroHours);
        Assert.Equal(item.PythonPositiveAverage, item.DotnetPositiveAverage);
        Assert.Equal(item.PythonPositiveHours, item.DotnetPositiveHours);
    }

    private static void RecordConstant<TValue, TFixture>(
        string assertionId,
        string upstreamSymbol,
        TValue pythonValue,
        TValue dotnetValue,
        TFixture fixture)
    {
        TrustedEvidenceRecorder.Record(
            assertionId,
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = new
                {
                    dotnet_value = dotnetValue,
                    python_value = pythonValue,
                },
                upstream_symbol = upstreamSymbol,
            });
    }

    private static void RecordMetric<TValue, TFixture>(
        string assertionId,
        string upstreamSymbol,
        IEnumerable<MetricObservation> observations,
        Func<MetricObservation, TValue> pythonValue,
        Func<MetricObservation, TValue> dotnetValue,
        TFixture fixture)
    {
        TrustedEvidenceRecorder.Record(
            assertionId,
            EvidenceTestCase,
            "not_applicable",
            new
            {
                fixture,
                observations = observations.Select(item => new
                {
                    case_id = item.CaseId,
                    dotnet_value = dotnetValue(item),
                    python_value = pythonValue(item),
                }).ToArray(),
                upstream_symbol = upstreamSymbol,
            });
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

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed record MetricObservation(
        string CaseId,
        double PythonAverage,
        double DotnetAverage,
        bool PythonHasNonzero,
        bool DotnetHasNonzero,
        bool PythonHasPositive,
        bool DotnetHasPositive,
        double PythonIntegral,
        double DotnetIntegral,
        bool PythonIsConstant,
        bool DotnetIsConstant,
        double PythonMaximum,
        double DotnetMaximum,
        double PythonMinimum,
        double DotnetMinimum,
        double PythonNonzeroHours,
        double DotnetNonzeroHours,
        double PythonPositiveAverage,
        double DotnetPositiveAverage,
        double PythonPositiveHours,
        double DotnetPositiveHours);
}
