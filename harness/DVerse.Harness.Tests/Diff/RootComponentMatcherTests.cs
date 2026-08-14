using DVerse.Harness.Diff;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// RootComponents entry (ratified model row 3, seat ruling 8): the dual key
/// rule (id when present, else type+schemaName) and the unsurveyed-type
/// warning behaviour. The unsurveyed-type coverage uses the real fixture
/// pair derived from demo-solution/solutions/DVerseCore/rootcomponents.yml
/// (harness/fixtures/diff/rootcomponent-unsurveyed-type); the dual-key rule
/// itself is a small, self-contained property of the matcher and is proven
/// with literal YAML instead, independent of any one fixture.
/// </summary>
public sealed class RootComponentMatcherTests
{
    private static readonly RootComponentMatcher Matcher = new();

    [Fact]
    public void Entry_with_an_id_matches_by_type_and_id_even_if_schemaName_differs()
    {
        var a = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '92'\n      '@id': '{same-guid}'\n");
        var b = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '92'\n      '@id': '{same-guid}'\n      '@schemaName': added-later\n");

        var result = Matcher.Match(a, b);

        Assert.Single(result.Matched);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void Entry_with_no_id_falls_back_to_type_plus_schemaName_case_insensitively()
    {
        var a = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '1'\n      '@schemaName': dv_Matter\n");
        var b = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '1'\n      '@schemaName': dv_matter\n");

        var result = Matcher.Match(a, b);

        var pair = Assert.Single(result.Matched);
        Assert.False(pair.Identity.IsWarning);
    }

    [Fact]
    public void Same_id_under_a_different_type_does_not_collide()
    {
        var a = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '91'\n      '@id': '{shared-guid}'\n");
        var b = DiffFixtures.ParseRoot(
            "RootComponents:\n  RootComponent:\n    - '@type': '92'\n      '@id': '{shared-guid}'\n");

        var result = Matcher.Match(a, b);

        Assert.Empty(result.Matched);
        Assert.Single(result.Added);
        Assert.Single(result.Removed);
    }

    [Fact]
    public void Entry_with_neither_id_nor_schemaName_warns_and_still_lands_somewhere()
    {
        var a = DiffFixtures.ParseRoot("RootComponents:\n  RootComponent:\n    - '@type': '1'\n");
        var b = DiffFixtures.ParseRoot("RootComponents: {}\n");

        var result = Matcher.Match(a, b);

        Assert.Empty(result.Matched);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("neither '@id' nor '@schemaName'", warning.Reason);
    }

    // --- Unsurveyed-type warning behaviour, real-tree fixture -------------------------------

    [Fact]
    public void Surveyed_types_from_the_real_solution_match_cleanly_with_no_warnings()
    {
        var a = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/a.yml");
        var b = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/b.yml");

        var result = Matcher.Match(a, b);

        foreach (var surveyedType in RootComponentMatcher.SurveyedTypes)
        {
            var pair = result.Matched.SingleOrDefault(p => p.Identity.Key.StartsWith($"{surveyedType}|"));
            Assert.NotNull(pair);
            Assert.False(pair!.Identity.IsWarning);
        }

        Assert.DoesNotContain(result.Warnings, w => w.Reason.Contains("unsurveyed") && w.Path.Contains("type=1,"));
    }

    [Fact]
    public void Unsurveyed_type_present_unchanged_on_both_sides_still_matches_but_warns()
    {
        var a = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/a.yml");
        var b = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/b.yml");

        var result = Matcher.Match(a, b);

        var pair = Assert.Single(result.Matched, p => p.Identity.Key.StartsWith("20|"));
        Assert.True(pair.Identity.IsWarning);
        Assert.Contains(result.Warnings, w => w.Path.Contains("type=20") && w.Reason.Contains("is unsurveyed"));
    }

    [Fact]
    public void Unsurveyed_type_only_in_baseline_is_removed_and_still_warns()
    {
        var a = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/a.yml");
        var b = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/b.yml");

        var result = Matcher.Match(a, b);

        Assert.Contains(result.Removed, e =>
            e.ToString()?.Contains("dv_legacywidget") == true ||
            YamlText(e).Contains("dv_legacywidget"));
    }

    [Fact]
    public void Unsurveyed_type_only_in_target_is_added_and_still_warns()
    {
        var a = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/a.yml");
        var b = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/b.yml");

        var result = Matcher.Match(a, b);

        Assert.Contains(result.Added, e => YamlText(e).Contains("dv_workflow"));
        Assert.Contains(result.Warnings, w => w.Path.Contains("type=25") && w.Reason.Contains("is unsurveyed"));
    }

    private static string YamlText(YamlDotNet.RepresentationModel.YamlNode node)
    {
        if (node is not YamlDotNet.RepresentationModel.YamlMappingNode mapping)
            return string.Empty;

        return string.Join(
            " ",
            mapping.Children.Select(c => $"{c.Key}={c.Value}"));
    }
}
