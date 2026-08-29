using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Rhino;
using GonieGonie.InvisibleDragon.Shape;
using GonieGonie.SimpleDragon.Rhino;
using Rhino.Geometry;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Expands the public one-Zone opening ownership convention into the symmetric
/// face topology required by the collective Rhino zone extractor.
/// </summary>
internal static class InteriorOpeningOwnershipResolver
{
    internal static IReadOnlyList<Diagnostic> Reconcile(
        IReadOnlyList<Brep> zoneGeometry,
        IReadOnlyList<List<RhinoFenestrationSource>> openingsByZone,
        RhinoGeometryContext context)
    {
        if (zoneGeometry.Count != openingsByZone.Count)
        {
            throw new ArgumentException("Zone geometry and opening collections must have equal counts.");
        }

        var diagnostics = new List<Diagnostic>();
        FaceDescriptor[] faces = ExtractFaces(zoneGeometry, context);
        FacePair[] candidates = FindCandidatePairs(faces, context);
        var candidateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FacePair pair in candidates)
        {
            Increment(candidateCounts, pair.First.Key);
            Increment(candidateCounts, pair.Second.Key);
        }

        foreach (FacePair pair in candidates.Where(item =>
                     candidateCounts[item.First.Key] == 1
                     && candidateCounts[item.Second.Key] == 1))
        {
            ReconcilePair(pair, openingsByZone, context, diagnostics);
        }

        return diagnostics;
    }

    private static FaceDescriptor[] ExtractFaces(
        IReadOnlyList<Brep> zones,
        RhinoGeometryContext context)
    {
        var faces = new List<FaceDescriptor>();
        for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
        {
            Brep zone = zones[zoneIndex]
                ?? throw new ArgumentException("Zone geometry contains a null Brep.", nameof(zones));
            for (int faceIndex = 0; faceIndex < zone.Faces.Count; faceIndex++)
            {
                BrepFace face = zone.Faces[faceIndex];
                try
                {
                    RhinoPolygonExtraction extraction = RhinoPolygonConverter.FromBrepFace(face, context);
                    faces.Add(new FaceDescriptor(zoneIndex, faceIndex, extraction.OuterLoop));
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException
                    or NotSupportedException)
                {
                    // RhinoZoneExtractor reports unsupported-face diagnostics later.
                }
            }
        }

        return faces.ToArray();
    }

    private static FacePair[] FindCandidatePairs(
        IReadOnlyList<FaceDescriptor> faces,
        RhinoGeometryContext context)
    {
        var pairs = new List<FacePair>();
        double angleTolerance = Math.Min(context.AngleToleranceRadians, Math.PI / 2d);
        for (int firstIndex = 0; firstIndex < faces.Count; firstIndex++)
        {
            FaceDescriptor first = faces[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < faces.Count; secondIndex++)
            {
                FaceDescriptor second = faces[secondIndex];
                if (first.ZoneIndex == second.ZoneIndex
                    || !first.Polygon.IsGeometricallyEquivalentTo(
                        second.Polygon,
                        allowReversedWinding: true,
                        tolerance: context.ModelToleranceMetres)
                    || first.Polygon.Normal.Dot(second.Polygon.Normal) > -Math.Cos(angleTolerance))
                {
                    continue;
                }

                pairs.Add(new FacePair(first, second));
            }
        }

        return pairs.ToArray();
    }

    private static void ReconcilePair(
        FacePair pair,
        IReadOnlyList<List<RhinoFenestrationSource>> openingsByZone,
        RhinoGeometryContext context,
        List<Diagnostic> diagnostics)
    {
        RhinoFenestrationSource[] first = openingsByZone[pair.First.ZoneIndex]
            .Where(item => item.HostFaceIndex == pair.First.FaceIndex)
            .ToArray();
        RhinoFenestrationSource[] second = openingsByZone[pair.Second.ZoneIndex]
            .Where(item => item.HostFaceIndex == pair.Second.FaceIndex)
            .ToArray();
        OpeningDescriptor[] firstDescriptors = Describe(first, context);
        OpeningDescriptor[] secondDescriptors = Describe(second, context);
        ReconciliationPlan plan = BuildPlan(
            firstDescriptors,
            secondDescriptors,
            (candidate, other) => candidate.Polygon.IsGeometricallyEquivalentTo(
                other.Polygon,
                allowReversedWinding: true,
                tolerance: context.ModelToleranceMetres),
            (candidate, other) => MetadataEquivalent(candidate.Source, other.Source));
        bool bothExplicit = firstDescriptors.Length > 0 && secondDescriptors.Length > 0;
        if (bothExplicit && (plan.AddToFirst.Count > 0 || plan.AddToSecond.Count > 0))
        {
            diagnostics.Add(TopologyConflict(first.FirstOrDefault(), second.FirstOrDefault()));
            return;
        }

        var addToSecond = new List<RhinoFenestrationSource>();
        var addToFirst = new List<RhinoFenestrationSource>();
        foreach (int index in plan.AddToSecond)
        {
            addToSecond.Add(CloneForFace(
                firstDescriptors[index].Source,
                pair.Second,
                context.ModelToleranceMetres));
        }

        foreach (int index in plan.AddToFirst)
        {
            addToFirst.Add(CloneForFace(
                secondDescriptors[index].Source,
                pair.First,
                context.ModelToleranceMetres));
        }

        foreach (IndexPair conflict in plan.Conflicts)
        {
            diagnostics.Add(Conflict(
                firstDescriptors[conflict.First].Source,
                secondDescriptors[conflict.Second].Source));
        }

        if (plan.Conflicts.Count == 0 && bothExplicit)
        {
            foreach (IndexPair match in plan.Matches)
            {
                RhinoFenestrationSource firstSource = firstDescriptors[match.First].Source;
                RhinoFenestrationSource secondSource = secondDescriptors[match.Second].Source;
                if (firstSource.Id is not null && firstSource.Id.Equals(secondSource.Id))
                {
                    int listIndex = openingsByZone[pair.Second.ZoneIndex].IndexOf(secondSource);
                    openingsByZone[pair.Second.ZoneIndex][listIndex] = CloneForFace(
                        secondSource,
                        pair.Second,
                        context.ModelToleranceMetres);
                }
            }
        }

        if (diagnostics.Any(item => item.IsFailure))
        {
            return;
        }

        openingsByZone[pair.First.ZoneIndex].AddRange(addToFirst);
        openingsByZone[pair.Second.ZoneIndex].AddRange(addToSecond);
    }

    private static OpeningDescriptor[] Describe(
        IReadOnlyList<RhinoFenestrationSource> openings,
        RhinoGeometryContext context)
    {
        return openings.Select(item => new OpeningDescriptor(
            item,
            RhinoPolygonConverter.FromClosedCurve(item.Boundary, context))).ToArray();
    }

    private static ReconciliationPlan BuildPlan<T>(
        IReadOnlyList<T> first,
        IReadOnlyList<T> second,
        Func<T, T, bool> geometryEquivalent,
        Func<T, T, bool> metadataEquivalent)
    {
        var matchedSecond = new bool[second.Count];
        var addToSecond = new List<int>();
        var conflicts = new List<IndexPair>();
        var matches = new List<IndexPair>();
        for (int firstIndex = 0; firstIndex < first.Count; firstIndex++)
        {
            int match = -1;
            for (int secondIndex = 0; secondIndex < second.Count; secondIndex++)
            {
                if (!matchedSecond[secondIndex]
                    && geometryEquivalent(first[firstIndex], second[secondIndex]))
                {
                    match = secondIndex;
                    break;
                }
            }

            if (match < 0)
            {
                addToSecond.Add(firstIndex);
                continue;
            }

            matchedSecond[match] = true;
            matches.Add(new IndexPair(firstIndex, match));
            if (!metadataEquivalent(first[firstIndex], second[match]))
            {
                conflicts.Add(new IndexPair(firstIndex, match));
            }
        }

        int[] addToFirst = Enumerable.Range(0, second.Count)
            .Where(index => !matchedSecond[index])
            .ToArray();
        return new ReconciliationPlan(addToFirst, addToSecond, conflicts, matches);
    }

    // Pure entry point used by contract tests on machines without Rhino's native geometry runtime.
    internal static IReadOnlyList<string> BuildPlanForTesting(
        IReadOnlyList<string> firstGeometryKeys,
        IReadOnlyList<string> firstMetadataKeys,
        IReadOnlyList<string> secondGeometryKeys,
        IReadOnlyList<string> secondMetadataKeys)
    {
        if (firstGeometryKeys.Count != firstMetadataKeys.Count
            || secondGeometryKeys.Count != secondMetadataKeys.Count)
        {
            throw new ArgumentException("Geometry and metadata key counts must match.");
        }

        KeyedOpening[] first = firstGeometryKeys
            .Select((geometry, index) => new KeyedOpening(geometry, firstMetadataKeys[index]))
            .ToArray();
        KeyedOpening[] second = secondGeometryKeys
            .Select((geometry, index) => new KeyedOpening(geometry, secondMetadataKeys[index]))
            .ToArray();
        ReconciliationPlan plan = BuildPlan(
            first,
            second,
            (candidate, other) => string.Equals(candidate.Geometry, other.Geometry, StringComparison.Ordinal),
            (candidate, other) => string.Equals(candidate.Metadata, other.Metadata, StringComparison.Ordinal));
        if (first.Length > 0 && second.Length > 0
            && (plan.AddToFirst.Count > 0 || plan.AddToSecond.Count > 0))
        {
            return new[] { "conflict-topology" };
        }

        return plan.AddToFirst.Select(index => "add-first:" + index)
            .Concat(plan.AddToSecond.Select(index => "add-second:" + index))
            .Concat(plan.Conflicts.Select(pair => "conflict:" + pair.First + ":" + pair.Second))
            .ToArray();
    }

    private static bool MetadataEquivalent(
        RhinoFenestrationSource first,
        RhinoFenestrationSource second)
    {
        return string.Equals(first.Name, second.Name, StringComparison.Ordinal)
            && first.Type == second.Type
            && string.Equals(first.ConstructionId, second.ConstructionId, StringComparison.Ordinal)
            && first.Blind == second.Blind;
    }

    private static RhinoFenestrationSource CloneForFace(
        RhinoFenestrationSource source,
        FaceDescriptor target,
        double toleranceMetres)
    {
        return new RhinoFenestrationSource(
            source.Boundary,
            target.FaceIndex,
            source.Name,
            source.Type,
            source.ConstructionId,
            source.Construction,
            source.Blind,
            PairedId(source.Id, target.Polygon, toleranceMetres),
            source.RhinoObjectId,
            source.GrasshopperPath,
            source.GrasshopperIndex);
    }

    private static EntityId? PairedId(
        EntityId? sourceId,
        PlanarPolygon targetFace,
        double toleranceMetres)
    {
        return sourceId is null
            ? null
            : new EntityId(PairedIdValue(
                sourceId.Value,
                RhinoGeometryFingerprint.ForPolygon(targetFace, toleranceMetres)));
    }

    private static string PairedIdValue(string sourceId, string targetFaceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetFaceFingerprint))
        {
            throw new ArgumentException("A source ID and target-face fingerprint are required.");
        }

        string fingerprint = targetFaceFingerprint.Trim();
        string suffix = fingerprint.Substring(0, Math.Min(16, fingerprint.Length));
        return sourceId.Trim() + "-PAIR-" + suffix;
    }

    internal static string PairedIdForTesting(string sourceId, string targetFaceFingerprint) =>
        PairedIdValue(sourceId, targetFaceFingerprint);

    private static Diagnostic Conflict(
        RhinoFenestrationSource first,
        RhinoFenestrationSource second)
    {
        return new Diagnostic(
            "SD.GH.INTERIOR_OPENING_CONFLICT",
            DiagnosticSeverity.Error,
            "Matching openings were explicitly connected to both sides of an interior face, but their name, type, construction, or blind differs.",
            first.Id ?? second.Id,
            suggestedAction: "Connect the opening to one Zone only, or make the two explicit definitions equivalent with distinct stable IDs.");
    }

    private static Diagnostic TopologyConflict(
        RhinoFenestrationSource? first,
        RhinoFenestrationSource? second)
    {
        return new Diagnostic(
            "SD.GH.INTERIOR_OPENING_CONFLICT",
            DiagnosticSeverity.Error,
            "Openings were explicitly connected to both sides of an interior face, but their opening topology is not identical.",
            first?.Id ?? second?.Id,
            suggestedAction: "Connect all openings to one Zone only, or provide identical opening geometry and definitions on both sides.");
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private sealed class FaceDescriptor
    {
        internal FaceDescriptor(int zoneIndex, int faceIndex, PlanarPolygon polygon)
        {
            ZoneIndex = zoneIndex;
            FaceIndex = faceIndex;
            Polygon = polygon;
        }

        internal int ZoneIndex { get; }

        internal int FaceIndex { get; }

        internal PlanarPolygon Polygon { get; }

        internal string Key => ZoneIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + FaceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FacePair
    {
        internal FacePair(FaceDescriptor first, FaceDescriptor second)
        {
            First = first;
            Second = second;
        }

        internal FaceDescriptor First { get; }

        internal FaceDescriptor Second { get; }
    }

    private sealed class OpeningDescriptor
    {
        internal OpeningDescriptor(RhinoFenestrationSource source, PlanarPolygon polygon)
        {
            Source = source;
            Polygon = polygon;
        }

        internal RhinoFenestrationSource Source { get; }

        internal PlanarPolygon Polygon { get; }
    }

    private sealed class KeyedOpening
    {
        internal KeyedOpening(string geometry, string metadata)
        {
            Geometry = geometry;
            Metadata = metadata;
        }

        internal string Geometry { get; }

        internal string Metadata { get; }
    }

    private sealed class IndexPair
    {
        internal IndexPair(int first, int second)
        {
            First = first;
            Second = second;
        }

        internal int First { get; }

        internal int Second { get; }
    }

    private sealed class ReconciliationPlan
    {
        internal ReconciliationPlan(
            IReadOnlyList<int> addToFirst,
            IReadOnlyList<int> addToSecond,
            IReadOnlyList<IndexPair> conflicts,
            IReadOnlyList<IndexPair> matches)
        {
            AddToFirst = addToFirst;
            AddToSecond = addToSecond;
            Conflicts = conflicts;
            Matches = matches;
        }

        internal IReadOnlyList<int> AddToFirst { get; }

        internal IReadOnlyList<int> AddToSecond { get; }

        internal IReadOnlyList<IndexPair> Conflicts { get; }

        internal IReadOnlyList<IndexPair> Matches { get; }
    }
}
