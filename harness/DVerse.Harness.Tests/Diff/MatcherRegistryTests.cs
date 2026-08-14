using DVerse.Harness.Diff;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// Mission ruling 2: "Every ratified class family from the model's table
/// gets a matcher or is explicitly registered as unsurveyed." These tests
/// prove the registry, not any one matcher, holds that promise: every
/// family named in docs/design/element-identity-model.md section 3 (minus
/// row 28, ruled out entirely by seat ruling 4) has an entry, every entry's
/// own ClassFamily matches the key it is registered under, and the one
/// family with zero real instances (MissingDependencies) is wired to the
/// explicit unsurveyed matcher rather than silently absent.
/// </summary>
public sealed class MatcherRegistryTests
{
    private static readonly MatcherRegistry Registry = MatcherRegistry.CreateDefault();

    [Fact]
    public void Every_ratified_class_family_is_registered()
    {
        foreach (var family in DiffClassFamilies.All)
        {
            Assert.True(
                Registry.TryGet(family, out _),
                $"Class family '{family}' has no registered matcher.");
        }
    }

    [Fact]
    public void Registry_has_no_entries_beyond_the_ratified_family_list()
    {
        // Guards the other direction: a stray or misspelled key would not be
        // caught by the "every family is registered" test above.
        Assert.Equal(DiffClassFamilies.All.Count, Registry.RegisteredFamilies.Count);

        foreach (var registered in Registry.RegisteredFamilies)
            Assert.Contains(registered, DiffClassFamilies.All);
    }

    [Fact]
    public void Every_registered_matchers_ClassFamily_matches_its_registry_key()
    {
        foreach (var family in DiffClassFamilies.All)
        {
            Registry.TryGet(family, out var matcher);
            Assert.Equal(family, matcher.ClassFamily);
        }
    }

    [Fact]
    public void MissingDependencies_is_wired_to_the_explicit_unsurveyed_matcher()
    {
        Registry.TryGet(DiffClassFamilies.MissingDependencies, out var matcher);
        Assert.IsType<UnsurveyedFamilyMatcher>(matcher);
    }

    [Fact]
    public void RootComponentsEntry_is_wired_to_the_dedicated_dual_rule_matcher()
    {
        Registry.TryGet(DiffClassFamilies.RootComponentsEntry, out var matcher);
        Assert.IsType<RootComponentMatcher>(matcher);
    }

    [Theory]
    [InlineData(DiffClassFamilies.FormXmlColumn)]
    [InlineData(DiffClassFamilies.FormXmlRow)]
    public void Honest_fallback_families_are_wired_to_the_positional_warning_matcher(string family)
    {
        Registry.TryGet(family, out var matcher);
        Assert.IsType<PositionalWarningMatcher>(matcher);
    }

    [Fact]
    public void Canvas_datacard_metadata_key_row_28_is_intentionally_not_registered()
    {
        // Seat ruling 4: "Canvas DataCard MetadataKey is NOT an identity key.
        // It may appear in changed-verdict detail as advisory context only."
        // No matcher, no registry entry, on purpose.
        const string notARegisteredFamily = "CanvasDataCardMetadataKey";
        Assert.DoesNotContain(notARegisteredFamily, DiffClassFamilies.All);
        Assert.False(Registry.TryGet(notARegisteredFamily, out _));
    }

    [Fact]
    public void Looking_up_an_unregistered_family_throws_rather_than_returning_null()
    {
        Assert.Throws<KeyNotFoundException>(() => Registry.Get("NotARealFamily"));
    }
}
