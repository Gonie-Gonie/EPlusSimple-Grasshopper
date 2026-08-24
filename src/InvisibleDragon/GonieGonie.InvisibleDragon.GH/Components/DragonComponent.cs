using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Idd;

namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

internal static class DragonPanels
{
    internal const string Category = "InvisibleDragon";
    internal const string Core = "Core";
    internal const string Construction = "Construction";
    internal const string Profile = "Profile";
    internal const string Geometry = "Geometry";
    internal const string Model = "Model";
    internal const string Results = "Results";
}

public abstract class DragonComponent : GH_Component
{
    protected DragonComponent(
        string name,
        string nickname,
        string description,
        string subcategory)
        : base(name, nickname, description, DragonPanels.Category, subcategory)
    {
    }

    protected override Bitmap? Icon => PluginIcons.Icon24;

    protected sealed override void SolveInstance(IGH_DataAccess DA)
    {
        try
        {
            Solve(DA);
        }
        catch (Exception exception)
        {
            AddRuntimeMessage(
                GH_RuntimeMessageLevel.Error,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    protected abstract void Solve(IGH_DataAccess DA);

    protected void Report(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            GH_RuntimeMessageLevel level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Info => GH_RuntimeMessageLevel.Remark,
                DiagnosticSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                _ => GH_RuntimeMessageLevel.Error,
            };
            AddRuntimeMessage(level, $"{diagnostic.Code}: {diagnostic.Message}");
        }
    }
}

internal static class StableIds
{
    internal static EntityId Resolve(string? explicitId, string prefix, params string[] parts)
    {
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return new EntityId(explicitId!.Trim());
        }

        string source = prefix + "\n" + string.Join("\n", parts);
        byte[] bytes = Encoding.UTF8.GetBytes(source);
#if NET6_0_OR_GREATER
        byte[] hash = SHA256.HashData(bytes);
#else
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(bytes);
        }
#endif
        string token = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        return new EntityId($"{prefix}-{token.Substring(0, 20)}");
    }
}

internal static class IddSchemaProvider
{
    private static readonly object SyncRoot = new();
    private static string? cachedPath;
    private static long cachedLength;
    private static DateTime cachedWriteTimeUtc;
    private static IddSchema? cachedSchema;

    internal static string? ResolvePath(string? suppliedPath)
    {
        if (!string.IsNullOrWhiteSpace(suppliedPath))
        {
            string full = Path.GetFullPath(suppliedPath!.Trim());
            return Directory.Exists(full) ? Path.Combine(full, "Energy+.idd") : full;
        }

        foreach (string variable in new[]
        {
            "GONIEGONIE_ENERGYPLUS_ROOT",
            "ENERGYPLUS_24_2_ROOT",
            "ENERGYPLUS_ROOT",
        })
        {
            string? root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root))
            {
                string candidate = Path.Combine(root, "Energy+.idd");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        string conventional = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
            "EnergyPlusV24-2-0",
            "Energy+.idd");
        return File.Exists(conventional) ? conventional : null;
    }

    internal static IddSchema? Resolve(string? suppliedPath)
    {
        string? path = ResolvePath(suppliedPath);
        if (path is null)
        {
            return null;
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The EnergyPlus IDD file was not found.", path);
        }

        lock (SyncRoot)
        {
            if (cachedSchema is not null &&
                string.Equals(cachedPath, file.FullName, StringComparison.OrdinalIgnoreCase) &&
                cachedLength == file.Length &&
                cachedWriteTimeUtc == file.LastWriteTimeUtc)
            {
                return cachedSchema;
            }

            cachedSchema = IddParser.ParseFile(file.FullName);
            cachedPath = file.FullName;
            cachedLength = file.Length;
            cachedWriteTimeUtc = file.LastWriteTimeUtc;
            return cachedSchema;
        }
    }
}
