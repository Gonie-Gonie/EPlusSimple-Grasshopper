using GonieGonie.BuildingEnergy.Contracts;
using GonieGonie.InvisibleDragon.Construction;
using GonieGonie.InvisibleDragon.Model;
using GonieGonie.InvisibleDragon.Shape;
using OpaqueConstruction = GonieGonie.InvisibleDragon.Construction.Construction;
using ZoneProfile = GonieGonie.InvisibleDragon.Profile.Profile;

namespace GonieGonie.InvisibleDragon.Tests.Model;

public sealed class EnergyModelProjectionTests
{
    [Fact]
    public void SurfacesFlattenZonesInInputOrderAndReturnFreshReadOnlyViews()
    {
        ZoneProfile profile = Profile("PROFILE-SURFACES", "Surface profile");
        OpaqueConstruction construction = Construction("Surface construction", "Surface layer");
        Surface first = Surface("SURFACE-FIRST", "First surface", construction);
        Surface second = Surface("SURFACE-SECOND", "Second surface", construction);
        Surface third = Surface("SURFACE-THIRD", "Third surface", construction);
        var model = new EnergyModel(
            "Surface projection",
            new[]
            {
                Zone("ZONE-FIRST", "First zone", profile, first, second),
                Zone("ZONE-SECOND", "Second zone", profile, third),
            });

        IReadOnlyList<Surface> surfaces = model.Surfaces;

        Assert.Collection(
            surfaces,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item),
            item => Assert.Same(third, item));
        AssertFreshReadOnly(() => model.Surfaces, first);
    }

    [Fact]
    public void UsedConstructionsKeepFirstEquivalentOpaqueHostsAndExcludeOtherFamilies()
    {
        var firstMaterial = new Material(
            "Core material",
            1.2,
            1800,
            900,
            thermalAbsorptance: 0.2,
            solarAbsorptance: 0.3,
            visibleAbsorptance: 0.4,
            roughness: MaterialRoughness.VeryRough);
        var secondaryVariant = new Material(
            "Core material",
            1.2,
            1800,
            900,
            thermalAbsorptance: 0.9,
            solarAbsorptance: 0.8,
            visibleAbsorptance: 0.7,
            roughness: MaterialRoughness.Smooth);
        var changedCore = new Material("Core material", 1.3, 1800, 900);
        var first = new OpaqueConstruction(
            "Shared construction",
            new[] { new Layer("First layer", firstMaterial, 0.1) });
        var equivalent = new OpaqueConstruction(
            "Shared construction",
            new[] { new Layer("Renamed equivalent layer", secondaryVariant, 0.1) });
        var unequalSameName = new OpaqueConstruction(
            "Shared construction",
            new[] { new Layer("Changed layer", changedCore, 0.1) });
        OpaqueConstruction caseVariant = Construction(
            "shared construction",
            "Case-variant layer");
        OpaqueConstruction doorOnly = Construction("Door-only construction", "Door layer");
        var window = new Window(
            new EntityId("OPENING-WINDOW"),
            "Excluded window",
            new Glazing("Excluded glazing", 1.4, 0.45),
            TestDomainFactory.Square(0.4, x: 0.2, y: 0.2));
        var door = new Door(
            new EntityId("OPENING-DOOR"),
            "Excluded door",
            doorOnly,
            TestDomainFactory.Square(0.4, x: 1.0, y: 0.2));
        Surface opaqueHost = Surface(
            "SURFACE-OPAQUE",
            "Opaque host",
            first,
            new IOpening[] { window, door });
        Surface airBoundaryHost = Surface(
            "SURFACE-AIR",
            "Air-boundary host",
            new AirBoundary("Excluded air boundary"));
        Surface noMassHost = Surface(
            "SURFACE-NOMASS",
            "No-mass host",
            new NoMassConstruction("Excluded no-mass", 2.5));
        Surface equivalentHost = Surface(
            "SURFACE-EQUIVALENT",
            "Equivalent opaque host",
            equivalent);
        Surface unequalHost = Surface(
            "SURFACE-UNEQUAL",
            "Unequal opaque host",
            unequalSameName);
        Surface caseHost = Surface(
            "SURFACE-CASE",
            "Case-variant opaque host",
            caseVariant);
        var model = new EnergyModel(
            "Construction projection",
            new[]
            {
                Zone(
                    "ZONE-CONSTRUCTIONS",
                    "Construction zone",
                    Profile("PROFILE-CONSTRUCTIONS", "Construction profile"),
                    opaqueHost,
                    airBoundaryHost,
                    noMassHost,
                    equivalentHost,
                    unequalHost,
                    caseHost),
            });

        IReadOnlyList<OpaqueConstruction> constructions = model.UsedConstructions;

        Assert.True(first.Equals(equivalent));
        Assert.False(first.Equals(unequalSameName));
        Assert.Collection(
            constructions,
            item => Assert.Same(first, item),
            item => Assert.Same(unequalSameName, item),
            item => Assert.Same(caseVariant, item));
        Assert.DoesNotContain(constructions, item => ReferenceEquals(item, equivalent));
        Assert.DoesNotContain(constructions, item => ReferenceEquals(item, doorOnly));
        AssertFreshReadOnly(() => model.UsedConstructions, first);
    }

    [Fact]
    public void UsedLayersApplyPinnedNameHashMembershipInFirstUseOrder()
    {
        var core = new Material(
            "Layer material",
            0.8,
            1600,
            850,
            thermalAbsorptance: 0.1,
            solarAbsorptance: 0.2,
            visibleAbsorptance: 0.3,
            roughness: MaterialRoughness.VeryRough);
        var secondaryVariantMaterial = new Material(
            "Layer material",
            0.8,
            1600,
            850,
            thermalAbsorptance: 0.9,
            solarAbsorptance: 0.8,
            visibleAbsorptance: 0.7,
            roughness: MaterialRoughness.Smooth);
        var changedCoreMaterial = new Material("Layer material", 0.9, 1600, 850);
        var first = new Layer("Shared layer", core, 0.12);
        var following = new Layer("Following layer", new Material("Following", 0.04, 40, 1400), 0.08);
        var equivalentSecondaryVariant = new Layer(
            "Shared layer",
            secondaryVariantMaterial,
            0.12);
        var unequalSameName = new Layer("Shared layer", changedCoreMaterial, 0.12);
        var equalDifferentName = new Layer(
            "Renamed equal layer",
            secondaryVariantMaterial,
            0.12);
        var model = new EnergyModel(
            "Layer projection",
            new[]
            {
                Zone(
                    "ZONE-LAYERS",
                    "Layer zone",
                    Profile("PROFILE-LAYERS", "Layer profile"),
                    Surface(
                        "SURFACE-LAYERS-FIRST",
                        "First layer host",
                        new OpaqueConstruction("Construction first", new[] { first, following })),
                    Surface(
                        "SURFACE-LAYERS-EQUIVALENT",
                        "Equivalent layer host",
                        new OpaqueConstruction(
                            "Construction equivalent",
                            new[] { equivalentSecondaryVariant })),
                    Surface(
                        "SURFACE-LAYERS-RETAINED",
                        "Retained layer host",
                        new OpaqueConstruction(
                            "Construction retained",
                            new[] { unequalSameName, equalDifferentName }))),
            });

        IReadOnlyList<Layer> layers = model.UsedLayers;

        Assert.True(first.Equals(equivalentSecondaryVariant));
        Assert.True(first.Equals(equalDifferentName));
        Assert.False(first.Equals(unequalSameName));
        Assert.Collection(
            layers,
            item => Assert.Same(first, item),
            item => Assert.Same(following, item),
            item => Assert.Same(unequalSameName, item),
            item => Assert.Same(equalDifferentName, item));
        Assert.DoesNotContain(
            layers,
            item => ReferenceEquals(item, equivalentSecondaryVariant));
        AssertFreshReadOnly(() => model.UsedLayers, first);
    }

    [Fact]
    public void UsedProfilesKeepFirstNamePositionAndLastObjectCaseSensitively()
    {
        ZoneProfile sharedFirst = Profile("PROFILE-SHARED-FIRST", "Shared profile");
        ZoneProfile unique = Profile("PROFILE-UNIQUE", "Unique profile");
        ZoneProfile sharedLast = Profile("PROFILE-SHARED-LAST", "Shared profile");
        ZoneProfile caseVariant = Profile("PROFILE-CASE", "shared profile");
        var model = new EnergyModel(
            "Profile projection",
            new[]
            {
                Zone("ZONE-PROFILE-FIRST", "First profile zone", sharedFirst),
                Zone("ZONE-PROFILE-UNIQUE", "Unique profile zone", unique),
                Zone("ZONE-PROFILE-LAST", "Last profile zone", sharedLast),
                Zone("ZONE-PROFILE-CASE", "Case profile zone", caseVariant),
            });

        IReadOnlyList<ZoneProfile> profiles = model.UsedProfiles;

        Assert.Collection(
            profiles,
            item => Assert.Same(sharedLast, item),
            item => Assert.Same(unique, item),
            item => Assert.Same(caseVariant, item));
        Assert.DoesNotContain(profiles, item => ReferenceEquals(item, sharedFirst));
        AssertFreshReadOnly(() => model.UsedProfiles, sharedLast);
    }

    [Fact]
    public void EmptyModelReturnsEmptyFreshProjectionViews()
    {
        var model = new EnergyModel("Empty projections", Array.Empty<Zone>());

        Assert.Empty(model.Surfaces);
        Assert.Empty(model.UsedConstructions);
        Assert.Empty(model.UsedLayers);
        Assert.Empty(model.UsedProfiles);
        Assert.NotSame(model.Surfaces, model.Surfaces);
        Assert.NotSame(model.UsedConstructions, model.UsedConstructions);
        Assert.NotSame(model.UsedLayers, model.UsedLayers);
        Assert.NotSame(model.UsedProfiles, model.UsedProfiles);
    }

    private static void AssertFreshReadOnly<T>(Func<IReadOnlyList<T>> read, T existingItem)
    {
        IReadOnlyList<T> first = read();
        IReadOnlyList<T> second = read();
        Assert.NotSame(first, second);
        IList<T> mutableView = Assert.IsAssignableFrom<IList<T>>(first);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(existingItem));
    }

    private static ZoneProfile Profile(string id, string name)
    {
        return new ZoneProfile(new EntityId(id), name);
    }

    private static Zone Zone(
        string id,
        string name,
        ZoneProfile profile,
        params Surface[] surfaces)
    {
        return new Zone(new EntityId(id), name, surfaces, profile);
    }

    private static Surface Surface(
        string id,
        string name,
        ISurfaceConstruction construction,
        IEnumerable<IOpening>? openings = null)
    {
        return new Surface(
            new EntityId(id),
            name,
            SurfaceType.Wall,
            construction,
            SurfaceBoundary.Outdoors,
            TestDomainFactory.Square(2),
            openings);
    }

    private static OpaqueConstruction Construction(string name, string layerName)
    {
        return new OpaqueConstruction(
            name,
            new[]
            {
                new Layer(layerName, new Material(layerName + " material", 1, 1000, 1000), 0.1),
            });
    }
}
