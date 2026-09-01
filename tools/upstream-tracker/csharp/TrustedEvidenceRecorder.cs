using System.Security.Cryptography;
using System.Text.Json;

namespace Dragons.UpstreamTracker;

/// <summary>
/// Emits one deterministic-output record for the trusted compatibility collector.
/// Link this exact tracked source file into a net8.0 test project that owns a
/// receipt; the collector binds the linked file, test source, TRX case, and DLL.
/// </summary>
internal static class TrustedEvidenceRecorder
{
    private const string RecordSchema = "dragons.trusted-evidence-record.v1";

    public static void Record(
        string assertionId,
        string testCase,
        string exercisedLoad,
        object? output)
    {
        string? recordsDirectory = Environment.GetEnvironmentVariable(
            "DRAGONS_EVIDENCE_RECORDS_DIRECTORY");
        string? nonce = Environment.GetEnvironmentVariable(
            "DRAGONS_EVIDENCE_SESSION_NONCE");
        bool hasRecordsDirectory = !string.IsNullOrWhiteSpace(recordsDirectory);
        bool hasNonce = !string.IsNullOrWhiteSpace(nonce);
        if (!hasRecordsDirectory && !hasNonce)
        {
            return;
        }

        if (hasRecordsDirectory != hasNonce)
        {
            throw new InvalidOperationException(
                "Both trusted collector environment variables must be set together.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(assertionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(testCase);
        if (exercisedLoad is not ("not_applicable" or "zero" or "nonzero"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(exercisedLoad),
                exercisedLoad,
                "Expected not_applicable, zero, or nonzero.");
        }

        string trustedRecordsDirectory = recordsDirectory!;
        string trustedNonce = nonce!;
        Directory.CreateDirectory(trustedRecordsDirectory);
        string randomSuffix = RandomNumberGenerator.GetHexString(16).ToLowerInvariant();
        string path = Path.Combine(
            trustedRecordsDirectory,
            $"record-{randomSuffix}.json");

        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("assertion_id", assertionId);
            writer.WriteString("exercised_load", exercisedLoad);
            writer.WritePropertyName("output");
            JsonSerializer.Serialize(writer, output, output?.GetType() ?? typeof(object));
            writer.WriteString("schema", RecordSchema);
            writer.WriteString("session_nonce", trustedNonce);
            writer.WriteBoolean("structural_only", false);
            writer.WriteString("test_case", testCase);
            writer.WriteEndObject();
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }
}
