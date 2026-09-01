using System.Text.Json.Serialization;
using Dragons.BuildingEnergy.Contracts.Internal;

namespace Dragons.BuildingEnergy.Contracts;

/// <summary>
/// Identifies source geometry without exposing RhinoCommon types to core assemblies.
/// </summary>
public sealed record GeometryProvenance
{
    /// <summary>
    /// Creates geometry provenance.
    /// </summary>
    [JsonConstructor]
    public GeometryProvenance(
        Guid? rhinoObjectId,
        int? brepFaceIndex,
        string geometryFingerprint,
        string? grasshopperPath,
        int? grasshopperIndex)
    {
        if (rhinoObjectId == Guid.Empty)
        {
            throw new ArgumentException("A source object identifier must not be an empty GUID.", nameof(rhinoObjectId));
        }

        if (brepFaceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brepFaceIndex),
                brepFaceIndex,
                "A Brep face index must be non-negative.");
        }

        if (grasshopperIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grasshopperIndex),
                grasshopperIndex,
                "A Grasshopper item index must be non-negative.");
        }

        RhinoObjectId = rhinoObjectId;
        BrepFaceIndex = brepFaceIndex;
        GeometryFingerprint = ContractGuard.RequiredText(
            geometryFingerprint,
            nameof(geometryFingerprint));
        GrasshopperPath = ContractGuard.OptionalText(grasshopperPath, nameof(grasshopperPath));
        GrasshopperIndex = grasshopperIndex;
    }

    /// <summary>
    /// Gets the Rhino document object identifier, when the geometry came from a document.
    /// </summary>
    [JsonPropertyOrder(0)]
    public Guid? RhinoObjectId { get; }

    /// <summary>
    /// Gets the zero-based Brep face index, when applicable.
    /// </summary>
    [JsonPropertyOrder(1)]
    public int? BrepFaceIndex { get; }

    /// <summary>
    /// Gets the implementation-defined stable geometry fingerprint.
    /// </summary>
    [JsonPropertyOrder(2)]
    public string GeometryFingerprint { get; }

    /// <summary>
    /// Gets the Grasshopper data-tree path, when applicable.
    /// </summary>
    [JsonPropertyOrder(3)]
    public string? GrasshopperPath { get; }

    /// <summary>
    /// Gets the zero-based item index within the Grasshopper path, when applicable.
    /// </summary>
    [JsonPropertyOrder(4)]
    public int? GrasshopperIndex { get; }
}
