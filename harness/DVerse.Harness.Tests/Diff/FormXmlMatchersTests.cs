using DVerse.Harness.Diff;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// FormXml control, cell, and row matchers (ratified model rows 12-14),
/// exercised against the real-tree mutation fixtures ruling 5 names by
/// name: an added control, a removed cell, and the FLAGSHIP reordered row
/// (harness/fixtures/diff/formxml-control-added,
/// formxml-cell-removed, formxml-row-reordered -- each derived verbatim
/// from demo-solution/entities/dv_matter/FormXml/main/dv_matter_main.yml).
/// The reordered-row test asserts the emitted warning text verbatim, per
/// the mission's Done means.
/// </summary>
public sealed class FormXmlMatchersTests
{
    private static readonly MatcherRegistry Registry = MatcherRegistry.CreateDefault();

    // --- Added control -----------------------------------------------------------------------

    [Fact]
    public void Added_control_fixture_matches_four_existing_controls_and_adds_the_fifth()
    {
        var a = DiffFixtures.LoadRoot("formxml-control-added/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-control-added/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlControl).Match(a, b);

        Assert.Equal(4, result.Matched.Count);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Warnings); // every matched classid is surveyed; the new control is a plain add

        var added = Assert.Single(result.Added);
        Assert.Equal("dv_status", ControlId(added));

        Assert.All(result.Matched, pair =>
        {
            Assert.Equal(ControlId(pair.A), pair.Identity.Key);
            Assert.Equal(ControlId(pair.A), ControlId(pair.B));
            Assert.False(pair.Identity.IsWarning);
        });
    }

    [Fact]
    public void Added_control_fixture_also_adds_the_matching_cell()
    {
        var a = DiffFixtures.LoadRoot("formxml-control-added/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-control-added/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlCell).Match(a, b);

        Assert.Equal(4, result.Matched.Count);
        var added = Assert.Single(result.Added);
        Assert.Equal("{9B111111-1111-4111-8111-111111111111}", CellId(added));
    }

    // --- Removed cell -------------------------------------------------------------------------

    [Fact]
    public void Removed_cell_fixture_matches_three_and_removes_the_dv_openedon_cell()
    {
        var a = DiffFixtures.LoadRoot("formxml-cell-removed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-cell-removed/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlCell).Match(a, b);

        Assert.Equal(3, result.Matched.Count);
        Assert.Empty(result.Added);
        var removed = Assert.Single(result.Removed);
        Assert.Equal("{A5583A68-DD4A-4FAF-ACD9-1288F0FB415D}", CellId(removed));
    }

    [Fact]
    public void Removed_cell_fixture_also_removes_the_dv_openedon_control()
    {
        var a = DiffFixtures.LoadRoot("formxml-cell-removed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-cell-removed/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlControl).Match(a, b);

        Assert.Equal(3, result.Matched.Count);
        var removed = Assert.Single(result.Removed);
        Assert.Equal("dv_openedon", ControlId(removed));
    }

    // --- FLAGSHIP: reordered row --------------------------------------------------------------

    [Fact]
    public void Reordered_row_fixture_matches_all_four_rows_positionally_with_a_warning_on_every_pair()
    {
        var a = DiffFixtures.LoadRoot("formxml-row-reordered/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-row-reordered/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlRow).Match(a, b);

        Assert.Equal(4, result.Matched.Count);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);

        // Ruling 3: "positional WITH a warning on every such match" -- not only the pairs that
        // actually moved.
        Assert.Equal(4, result.Warnings.Count);
        Assert.All(result.Matched, pair => Assert.True(pair.Identity.IsWarning));

        // Verbatim warning text, asserted exactly (mission Done means: "the report must show
        // the warning text verbatim").
        const string expectedWarning =
            "FormXmlRow: matched by ordinal position 1 only; no identity attribute exists in the " +
            "real committed shape (docs/design/element-identity-model.md section 4, honest " +
            "fallback). A reordered sibling reads as a false match at this position; treat this " +
            "pairing as unverified until property-level content confirms it, per mission ruling 3.";
        Assert.Contains(result.Warnings, w => w.Reason == expectedWarning);
    }

    [Fact]
    public void Reordered_row_fixture_proves_positions_one_and_two_pair_the_wrong_rows()
    {
        var a = DiffFixtures.LoadRoot("formxml-row-reordered/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-row-reordered/b.yml");

        var result = Registry.Get(DiffClassFamilies.FormXmlRow).Match(a, b);

        // Baseline order: dv_name, dv_matternumber, dv_openedon, ownerid.
        // Target order:   dv_name, dv_openedon,    dv_matternumber, ownerid.
        Assert.Equal("{7FD3466F-2E44-4BD5-9553-79521D920A68}", RowCellId(result.Matched[1].A)); // dv_matternumber
        Assert.Equal("{A5583A68-DD4A-4FAF-ACD9-1288F0FB415D}", RowCellId(result.Matched[1].B)); // dv_openedon
        Assert.NotEqual(RowCellId(result.Matched[1].A), RowCellId(result.Matched[1].B));

        Assert.Equal("{A5583A68-DD4A-4FAF-ACD9-1288F0FB415D}", RowCellId(result.Matched[2].A)); // dv_openedon
        Assert.Equal("{7FD3466F-2E44-4BD5-9553-79521D920A68}", RowCellId(result.Matched[2].B)); // dv_matternumber
        Assert.NotEqual(RowCellId(result.Matched[2].A), RowCellId(result.Matched[2].B));

        // Positions 0 and 3 (dv_name, ownerid) did not move, but still warn (ruling 3: every pair).
        Assert.Equal(RowCellId(result.Matched[0].A), RowCellId(result.Matched[0].B));
        Assert.Equal(RowCellId(result.Matched[3].A), RowCellId(result.Matched[3].B));
    }

    // --- Unknown control class (ruling 3: unsurveyed control classes warn, never silently match) --

    [Fact]
    public void Control_with_an_unsurveyed_classid_still_matches_by_id_but_warns()
    {
        const string surveyedClassId = "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}"; // textbox, surveyed
        const string unsurveyedClassId = "{FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF}"; // never observed

        var a = DiffFixtures.ParseRoot(FormXmlWithOneControl(surveyedClassId));
        var b = DiffFixtures.ParseRoot(FormXmlWithOneControl(unsurveyedClassId));

        var result = Registry.Get(DiffClassFamilies.FormXmlControl).Match(a, b);

        var pair = Assert.Single(result.Matched);
        Assert.Equal("ctrl-1", pair.Identity.Key);
        Assert.True(pair.Identity.IsWarning);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("is not one of the three surveyed classids", warning.Reason);
        Assert.Contains(unsurveyedClassId, warning.Reason);
    }

    [Fact]
    public void Control_with_a_surveyed_classid_on_both_sides_matches_with_no_warning()
    {
        const string classId = "{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}"; // datetime, surveyed

        var a = DiffFixtures.ParseRoot(FormXmlWithOneControl(classId));
        var b = DiffFixtures.ParseRoot(FormXmlWithOneControl(classId));

        var result = Registry.Get(DiffClassFamilies.FormXmlControl).Match(a, b);

        var pair = Assert.Single(result.Matched);
        Assert.False(pair.Identity.IsWarning);
        Assert.Empty(result.Warnings);
    }

    private static string FormXmlWithOneControl(string classId) => $"""
        systemform:
          form:
            tabs:
              tab:
                columns:
                  column:
                    sections:
                      section:
                        rows:
                          row:
                            - cell:
                                '@id': cell-1
                                control:
                                  '@id': ctrl-1
                                  '@classid': '{classId}'
        """;

    // --- Helpers ------------------------------------------------------------------------------

    private static string? ControlId(YamlNode control) => ScalarChild(control, "@id");

    private static string? CellId(YamlNode cell) => ScalarChild(cell, "@id");

    /// <summary>Given a FormXmlRow element (a "row" node whose only child is "cell"), the cell's own '@id'.</summary>
    private static string? RowCellId(YamlNode row)
    {
        if (row is not YamlMappingNode mapping)
            return null;

        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode key && key.Value == "cell")
                return ScalarChild(child.Value, "@id");
        }

        return null;
    }

    private static string? ScalarChild(YamlNode node, string key)
    {
        if (node is not YamlMappingNode mapping)
            return null;

        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalarKey && scalarKey.Value == key)
                return (child.Value as YamlScalarNode)?.Value;
        }

        return null;
    }
}
