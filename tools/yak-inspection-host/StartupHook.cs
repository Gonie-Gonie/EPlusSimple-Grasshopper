using System.Reflection;

public static class StartupHook
{
    private const string ProbePathsVariable = "DRAGONS_YAK_INSPECTION_PATHS";
    private static readonly string[] ManagedExtensions = { ".dll", ".gha" };

    public static void Initialize()
    {
        string? value = Environment.GetEnvironmentVariable(ProbePathsVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string[] directories = value
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => Resolve(args.Name, directories);

        // Yak loads a GHA with Assembly.LoadFile. Preloading its product dependencies into
        // the default context lets GetTypes inspect the real assembly without shipping the
        // SDK references or changing the archive payload.
        foreach (string directory in directories)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "Dragons.*.dll"))
            {
                TryLoad(path);
            }
        }
    }

    private static Assembly? Resolve(string fullName, IEnumerable<string> directories)
    {
        string? simpleName;
        try
        {
            simpleName = new AssemblyName(fullName).Name;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(simpleName))
        {
            return null;
        }

        foreach (string directory in directories)
        {
            foreach (string extension in ManagedExtensions)
            {
                string candidate = Path.Combine(directory, simpleName + extension);
                Assembly? assembly = TryLoad(candidate);
                if (assembly is not null)
                {
                    return assembly;
                }
            }
        }

        return null;
    }

    private static Assembly? TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
