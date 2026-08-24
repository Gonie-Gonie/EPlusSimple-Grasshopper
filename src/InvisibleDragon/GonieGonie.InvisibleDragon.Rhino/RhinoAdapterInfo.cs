namespace GonieGonie.InvisibleDragon.Rhino;

/// <summary>
/// Marks the Rhino adapter assembly and its minimum supported Rhino version.
/// </summary>
public static class RhinoAdapterInfo
{
#if NET48
    public static Version MinimumRhinoVersion { get; } = new(7, 0);
#else
    public static Version MinimumRhinoVersion { get; } = new(8, 0);
#endif
}
