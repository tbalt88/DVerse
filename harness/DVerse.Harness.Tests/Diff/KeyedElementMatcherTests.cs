using DVerse.Harness.Diff;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// Direct unit tests of the generic engine every "exact" and "case
/// insensitive" row of docs/design/element-identity-model.md section 3 is
/// built from. Uses small literal YAML rather than fixture files: these
/// prove the engine's own contract (key equality, comparer, duplicate/
/// unkeyed handling), independent of which real class family a given
/// caller configures it for.
/// </summary>
public sealed class KeyedElementMatcherTests
{
    private static IEnumerable<(string? Key, YamlNode Element)> Items(YamlNode root)
    {
        var mapping = (YamlMappingNode)root;
        var items = (YamlSequenceNode)mapping.Children.Single(c => ((YamlScalarNode)c.Key).Value == "Items").Value;

        foreach (var item in items.Children)
        {
            var itemMapping = (YamlMappingNode)item;
            var keyNode = itemMapping.Children.SingleOrDefault(c => ((YamlScalarNode)c.Key).Value == "Id").Value;
            yield return ((keyNode as YamlScalarNode)?.Value, item);
        }
    }

    private static readonly KeyedElementMatcher ExactMatcher =
        new("TestFamilyExact", Items, StringComparer.Ordinal);

    private static readonly KeyedElementMatcher CaseInsensitiveMatcher =
        new("TestFamilyCi", Items, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Same_key_on_both_sides_is_matched_not_added_or_removed()
    {
        var a = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n    Name: original\n");
        var b = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n    Name: renamed-in-place\n");

        var result = ExactMatcher.Match(a, b);

        var pair = Assert.Single(result.Matched);
        Assert.Equal("guid-1", pair.Identity.Key);
        Assert.False(pair.Identity.IsWarning);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Key_only_in_target_is_added_key_only_in_baseline_is_removed()
    {
        var a = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n  - Id: guid-gone\n");
        var b = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n  - Id: guid-new\n");

        var result = ExactMatcher.Match(a, b);

        Assert.Single(result.Matched);
        var added = Assert.Single(result.Added);
        Assert.Equal("guid-new", ((YamlMappingNode)added).Children
            .Single(c => ((YamlScalarNode)c.Key).Value == "Id").Value.ToString());
        var removed = Assert.Single(result.Removed);
        Assert.Equal("guid-gone", ((YamlMappingNode)removed).Children
            .Single(c => ((YamlScalarNode)c.Key).Value == "Id").Value.ToString());
    }

    [Fact]
    public void Ordinal_comparer_treats_different_casing_as_a_different_key()
    {
        var a = DiffFixtures.ParseRoot("Items:\n  - Id: dv_Name\n");
        var b = DiffFixtures.ParseRoot("Items:\n  - Id: dv_name\n");

        var result = ExactMatcher.Match(a, b);

        Assert.Empty(result.Matched);
        Assert.Single(result.Added);
        Assert.Single(result.Removed);
    }

    [Fact]
    public void Case_insensitive_comparer_matches_a_pure_casing_difference()
    {
        // The ratified model's own rule for Entity and Attribute (rows 6/7):
        // SchemaName/PhysicalName vs LogicalName are the same identifier
        // under two casing conventions.
        var a = DiffFixtures.ParseRoot("Items:\n  - Id: dv_Name\n");
        var b = DiffFixtures.ParseRoot("Items:\n  - Id: dv_name\n");

        var result = CaseInsensitiveMatcher.Match(a, b);

        var pair = Assert.Single(result.Matched);
        Assert.False(pair.Identity.IsWarning);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void Element_with_no_extractable_key_warns_instead_of_matching_or_vanishing()
    {
        var a = DiffFixtures.ParseRoot("Items:\n  - Name: no-id-here\n");
        var b = DiffFixtures.ParseRoot("Items: []\n");

        var result = ExactMatcher.Match(a, b);

        Assert.Empty(result.Matched);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("no extractable key", warning.Reason);
    }

    [Fact]
    public void Warn_on_match_hook_attaches_a_warning_to_a_specific_matched_pair()
    {
        var withHook = new KeyedElementMatcher(
            "TestFamilyHook",
            Items,
            StringComparer.Ordinal,
            warnOnMatch: (_, b) =>
            {
                var name = ((YamlMappingNode)b).Children
                    .SingleOrDefault(c => ((YamlScalarNode)c.Key).Value == "Name").Value?.ToString();
                return name == "suspicious" ? "flagged by test hook" : null;
            });

        var a = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n    Name: fine\n");
        var b = DiffFixtures.ParseRoot("Items:\n  - Id: guid-1\n    Name: suspicious\n");

        var result = withHook.Match(a, b);

        Assert.Single(result.Matched);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("flagged by test hook", warning.Reason);
    }
}
