using DVerse.Harness.Diff;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// Canvas screen and control matchers (ratified model rows 26-27, seat
/// ruling 5: "Canvas rename = delete + add is ratified as matching the
/// platform's own semantics"). The control test uses the real-tree
/// "renamed canvas control" fixture ruling 5 names by name
/// (harness/fixtures/diff/canvas-control-renamed, a trimmed excerpt of
/// demo-solution/canvasapps/MatterCanvas/Src/Screen1.pa.yaml's Button
/// controls). The screen-level test is small literal YAML, since the
/// mission's fixture requirement names the CONTROL rename specifically,
/// not a screen rename, but the identical rule applies at both levels per
/// the model (rows 26 and 27 share the same "exact string equality,
/// rename is delete+add" rule) and it costs little to prove both.
/// <para>
/// Note on shape: the CanvasControl matcher's <see cref="MatchedPair.A"/>/
/// <see cref="MatchedPair.B"/>, <see cref="MatchResult.Added"/>, and
/// <see cref="MatchResult.Removed"/> nodes are already the control BODY
/// (the mapping under <c>Control:</c>/<c>Properties:</c>), not the
/// <c>{ButtonName: {...}}</c> wrapper: the wrapper's key is exactly what
/// became the match key, so it is consumed during enumeration and does not
/// survive onto the node itself. Added/Removed controls are therefore
/// identified here by their content (their real, distinguishing
/// <c>OnSelect</c> formula), not by a key that no longer exists on them.
/// </para>
/// </summary>
public sealed class CanvasMatchersTests
{
    private static readonly MatcherRegistry Registry = MatcherRegistry.CreateDefault();

    [Fact]
    public void Renamed_control_fixture_matches_the_two_untouched_buttons_by_exact_key()
    {
        var a = DiffFixtures.LoadRoot("canvas-control-renamed/a.yml");
        var b = DiffFixtures.LoadRoot("canvas-control-renamed/b.yml");

        var result = Registry.Get(DiffClassFamilies.CanvasControl).Match(a, b);

        // Button1 and Button3 are untouched; Button2 -> NewMatterButton is a rename.
        Assert.Equal(2, result.Matched.Count);
        Assert.Contains(result.Matched, p => p.Identity.Key == "Button1");
        Assert.Contains(result.Matched, p => p.Identity.Key == "Button3");
        Assert.All(result.Matched, p => Assert.False(p.Identity.IsWarning));
    }

    [Fact]
    public void Renamed_control_fixture_reports_the_rename_as_one_removed_and_one_added_no_warning()
    {
        var a = DiffFixtures.LoadRoot("canvas-control-renamed/a.yml");
        var b = DiffFixtures.LoadRoot("canvas-control-renamed/b.yml");

        var result = Registry.Get(DiffClassFamilies.CanvasControl).Match(a, b);

        var removed = Assert.Single(result.Removed);
        var added = Assert.Single(result.Added);

        // Both carry Button2's real, distinguishing content (OnSelect: =NewForm(Form1)),
        // proving this is the rename pair and not some other button.
        Assert.Equal("=NewForm(Form1)", OnSelectFormula(removed));
        Assert.Equal("=NewForm(Form1)", OnSelectFormula(added));

        // Delete + add, never a silent match: ruling 5.
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Untouched_controls_are_matched_with_byte_identical_bodies()
    {
        var a = DiffFixtures.LoadRoot("canvas-control-renamed/a.yml");
        var b = DiffFixtures.LoadRoot("canvas-control-renamed/b.yml");

        var result = Registry.Get(DiffClassFamilies.CanvasControl).Match(a, b);

        var button1 = result.Matched.Single(p => p.Identity.Key == "Button1");
        Assert.Equal(OnSelectFormula(button1.A), OnSelectFormula(button1.B));
    }

    [Fact]
    public void Screen_rename_is_also_delete_plus_add_not_a_silent_match()
    {
        var a = DiffFixtures.ParseRoot("""
            Screens:
              Screen1:
                Children: []
              Screen2:
                Children: []
            """);
        var b = DiffFixtures.ParseRoot("""
            Screens:
              Screen1:
                Children: []
              ScreenTwoRenamed:
                Children: []
            """);

        var result = Registry.Get(DiffClassFamilies.CanvasScreen).Match(a, b);

        Assert.Contains(result.Matched, p => p.Identity.Key == "Screen1");
        Assert.Single(result.Matched);
        Assert.Empty(result.Warnings);
        Assert.Single(result.Removed);
        Assert.Single(result.Added);
    }

    // --- Helpers ------------------------------------------------------------------------------

    private static string? OnSelectFormula(YamlNode controlBody)
    {
        var properties = ChildNode(controlBody, "Properties");
        var onSelect = properties is null ? null : ScalarChild(properties, "OnSelect");
        return onSelect?.Trim();
    }

    private static string? ScalarChild(YamlNode node, string key) =>
        (ChildNode(node, key) as YamlScalarNode)?.Value;

    private static YamlNode? ChildNode(YamlNode node, string key)
    {
        if (node is not YamlMappingNode mapping)
            return null;

        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalarKey && scalarKey.Value == key)
                return child.Value;
        }

        return null;
    }
}
