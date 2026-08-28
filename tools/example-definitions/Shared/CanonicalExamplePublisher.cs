namespace GonieGonie.Dragons.ExampleDefinitions;

/// <summary>
/// Publishes generated binary examples without replacing semantically identical
/// tracked containers whose ZIP/3DM bookkeeping bytes are host-dependent.
/// </summary>
internal static class CanonicalExamplePublisher
{
    internal static bool Publish(
        string candidatePath,
        string canonicalPath,
        string outputDirectory,
        Action<string> validateSemantic)
    {
        if (!File.Exists(candidatePath))
        {
            throw new FileNotFoundException("Generated example candidate is absent.", candidatePath);
        }

        validateSemantic(candidatePath);
        if (File.Exists(canonicalPath))
        {
            try
            {
                validateSemantic(canonicalPath);
                return false;
            }
            catch (InvalidOperationException)
            {
                // The tracked binary no longer satisfies the current semantic
                // contract. The already validated candidate may replace it.
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(canonicalPath)
            ?? throw new InvalidOperationException("Canonical example has no parent directory."));
        string rollbackDirectory = Path.Combine(outputDirectory, "publish-rollback");
        Directory.CreateDirectory(rollbackDirectory);
        string rollbackPath = Path.Combine(rollbackDirectory, Path.GetFileName(canonicalPath) + ".previous");
        if (File.Exists(rollbackPath)) File.Delete(rollbackPath);

        bool replaced = File.Exists(canonicalPath);
        try
        {
            if (replaced) File.Replace(candidatePath, canonicalPath, rollbackPath, ignoreMetadataErrors: true);
            else File.Move(candidatePath, canonicalPath);
            validateSemantic(canonicalPath);
            if (File.Exists(rollbackPath)) File.Delete(rollbackPath);
            return true;
        }
        catch
        {
            if (replaced && File.Exists(rollbackPath))
            {
                File.Replace(rollbackPath, canonicalPath, null, ignoreMetadataErrors: true);
            }
            else if (!replaced && File.Exists(canonicalPath))
            {
                File.Delete(canonicalPath);
            }
            throw;
        }
    }
}
