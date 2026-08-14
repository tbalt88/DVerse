using DVerse.Harness.Diff;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// The semantic diff engine (wave 7.2): <see cref="ArtifactDiffer"/> walking
/// the ratified family chains built on top of 7.1i's matching layer. Covers
/// the four mandatory fixtures ruling 7 names by name (the lesson-14
/// flagship, a canvas Children-nesting change two levels deep, a property
/// change under a positionally matched row proving warning propagation, and
/// an unchanged-everything pair), plus lighter coverage of every other
/// family chain the mission ships (ruling 6) using small literal YAML in
/// the same style 7.1i's own KeyedElementMatcherTests and
/// CanvasMatchersTests already established.
/// </summary>
public sealed class ArtifactDifferTests
{
    private static readonly ArtifactDiffer Differ = new();

    // --- FLAGSHIP: lesson-14 shape, identical ids, one datafieldname changed -----------------------

    [Fact]
    public void Lesson14_flagship_identical_ids_one_datafieldname_changed_is_caught_by_recursion()
    {
        var a = DiffFixtures.LoadRoot("formxml-datafieldname-changed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-datafieldname-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        // Exactly one Changed verdict in the whole walk: the dv_openedon control's datafieldname.
        var changed = Assert.Single(diff.Verdicts, v => v.Kind == DiffVerdictKind.Changed);
        Assert.Equal(DiffClassFamilies.FormXmlControl, changed.Identity.ClassKind);
        Assert.Equal("dv_openedon", changed.Identity.Key);

        var propertyChange = Assert.Single(changed.PropertyChanges);
        Assert.Equal("@datafieldname", propertyChange.PropertyPath);
        Assert.Equal("dv_openedon", propertyChange.ValueInA);
        Assert.Equal("dv_OpenedOn", propertyChange.ValueInB);

        Assert.DoesNotContain(diff.Verdicts, v => v.Kind is DiffVerdictKind.Added or DiffVerdictKind.Removed);
        Assert.Equal(1, diff.Summary.Changed);

        // Every other control (three of the four) is Unchanged, proving the recursion actually
        // walked all the way down through every identical-id level rather than stopping early
        // once systemform/tab/section/row/cell all matched.
        var otherControls = diff.Verdicts.Where(v =>
            v.Identity.ClassKind == DiffClassFamilies.FormXmlControl && v.Identity.Key != "dv_openedon");
        Assert.Equal(3, otherControls.Count());
        Assert.All(otherControls, v => Assert.Equal(DiffVerdictKind.Unchanged, v.Kind));
    }

    [Fact]
    public void Lesson14_flagship_changed_verdict_inherits_the_positional_row_warning_and_the_ratified_phrase()
    {
        var a = DiffFixtures.LoadRoot("formxml-datafieldname-changed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-datafieldname-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        var changed = Assert.Single(diff.Verdicts, v => v.Kind == DiffVerdictKind.Changed);

        // The control's own match is a confident, exact '@id' match: no warning of its own.
        Assert.False(changed.Identity.IsWarning);

        // But it sits under a positionally matched row, so ruling 5's propagation clause still
        // attaches that row's warning, and the doctrine phrase, to this Changed verdict.
        Assert.NotEmpty(changed.Warnings);
        Assert.Contains(changed.Warnings, w => w.Reason.Contains("matched by ordinal position"));
        Assert.Contains("unverified pairing, content-confirmed only", changed.Description);
    }

    // Mutation-check target for the recursion rule (mission ruling 7, "lesson 6"): the slice report
    // records a run where ArtifactDiffer.Walk's "foreach (var child in step.Children) Walk(...)"
    // line was temporarily removed and this exact test was confirmed to fail red -- proving the
    // test truly exercises the recursion rule, not just the matching layer underneath it -- before
    // the recursion call was restored and the suite re-verified green.
    [Fact]
    public void Lesson14_flagship_would_go_red_without_recursion_into_matched_parents()
    {
        var a = DiffFixtures.LoadRoot("formxml-datafieldname-changed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-datafieldname-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        // If a Matched systemform/tab/column/section/row/cell parent did not recurse, the
        // dv_openedon control (and everything below the top family) would never be visited at
        // all, and this walk would report zero Changed verdicts -- the exact false-negative
        // lesson 14 burned. This assertion is the automated half of that mutation-check; the
        // report records the actual disable-and-rerun.
        Assert.True(diff.Summary.Changed > 0, "recursion rule regression: no Changed verdict reached the leaf control");
    }

    // --- Canvas: Children-nesting change two levels deep --------------------------------------------

    [Fact]
    public void Canvas_nested_children_two_levels_deep_change_is_found_by_self_recursion()
    {
        var a = DiffFixtures.LoadRoot("canvas-nested-children-changed/a.yml");
        var b = DiffFixtures.LoadRoot("canvas-nested-children-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.CanvasScreen);

        var changed = Assert.Single(diff.Verdicts, v => v.Kind == DiffVerdictKind.Changed);
        Assert.Equal(DiffClassFamilies.CanvasControl, changed.Identity.ClassKind);
        Assert.Equal("DataCardValue1", changed.Identity.Key);

        var propertyChange = Assert.Single(changed.PropertyChanges);
        Assert.Equal("Properties.Width", propertyChange.PropertyPath);
        Assert.Equal("=Parent.Width - 60", propertyChange.ValueInA);
        Assert.Equal("=Parent.Width - 80", propertyChange.ValueInB);

        Assert.Empty(diff.Warnings); // canvas identity is exact-key, never positional
        Assert.DoesNotContain(diff.Verdicts, v => v.Kind is DiffVerdictKind.Added or DiffVerdictKind.Removed);

        // Proves the self-recursion actually descended: Form1 and the DataCard both got their own
        // Unchanged verdicts, meaning CanvasControl matched itself twice below the screen before
        // reaching the changed leaf.
        Assert.Contains(diff.Verdicts, v => v.Identity.Key == "Form1" && v.Kind == DiffVerdictKind.Unchanged);
        Assert.Contains(diff.Verdicts, v => v.Identity.Key == "Matter Name_DataCard1" && v.Kind == DiffVerdictKind.Unchanged);
        Assert.Contains(diff.Verdicts, v => v.Identity.Key == "DataCardKey1" && v.Kind == DiffVerdictKind.Unchanged);
    }

    // --- Property change under a positionally matched row: warning propagation ----------------------

    [Fact]
    public void Property_change_under_a_positionally_matched_row_inherits_the_row_warning()
    {
        var a = DiffFixtures.LoadRoot("formxml-row-property-changed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-row-property-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        var changedCell = Assert.Single(diff.Verdicts,
            v => v.Kind == DiffVerdictKind.Changed && v.Identity.ClassKind == DiffClassFamilies.FormXmlCell);

        // The cell's own match is a confident, exact '@id' GUID match: no warning of its own.
        Assert.False(changedCell.Identity.IsWarning);

        // Yet its verdict still carries the row's inherited positional warning (ruling 5's
        // propagation clause: "PROPAGATE into every verdict under that pairing") and the ratified
        // "unverified pairing" phrase, precisely because it sits under an unverified pairing even
        // though its own match is confident.
        Assert.NotEmpty(changedCell.Warnings);
        Assert.Contains(changedCell.Warnings, w => w.Reason.Contains("matched by ordinal position"));
        Assert.Contains("unverified pairing, content-confirmed only", changedCell.Description);

        var propertyChange = Assert.Single(changedCell.PropertyChanges);
        Assert.Equal("labels.label.@description", propertyChange.PropertyPath);
        Assert.Equal("Matter Number", propertyChange.ValueInA);
        Assert.Equal("Matter #", propertyChange.ValueInB);
    }

    [Fact]
    public void Property_change_under_a_positionally_matched_row_does_not_leak_the_warning_to_untouched_siblings()
    {
        var a = DiffFixtures.LoadRoot("formxml-row-property-changed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-row-property-changed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        // dv_name's cell/control did not change: Unchanged, but STILL carries its own row's
        // positional warning (every row pairing warns, ruling 3), just not the "unverified
        // pairing" Changed-only phrase (nothing to phrase, it is Unchanged).
        var unchangedCell = diff.Verdicts.Single(v =>
            v.Kind == DiffVerdictKind.Unchanged && v.Identity.ClassKind == DiffClassFamilies.FormXmlCell
            && v.Identity.Key == "{040EF0AB-C03F-4F37-9A4C-B87C47DA740E}");
        Assert.NotEmpty(unchangedCell.Warnings);
        Assert.DoesNotContain("unverified pairing", unchangedCell.Description);
    }

    // --- Unchanged-everything: all Unchanged, zero warnings ------------------------------------------

    [Fact]
    public void Unchanged_everything_appmodule_fixture_is_all_unchanged_with_zero_warnings()
    {
        var a = DiffFixtures.LoadRoot("appmodule-unchanged/a.yml");
        var b = DiffFixtures.LoadRoot("appmodule-unchanged/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.AppModule);

        Assert.NotEmpty(diff.Verdicts);
        Assert.All(diff.Verdicts, v => Assert.Equal(DiffVerdictKind.Unchanged, v.Kind));
        Assert.Empty(diff.Warnings);
        Assert.Equal(diff.Verdicts.Count, diff.Summary.Unchanged);
        Assert.Equal(0, diff.Summary.Added + diff.Summary.Removed + diff.Summary.Changed);

        // AppModule itself, both AppModuleComponents, both AppModuleRoleMaps Roles.
        Assert.Equal(5, diff.Verdicts.Count);
    }

    // --- Added/Removed short-circuit recursion (ruling 3) ---------------------------------------------

    [Fact]
    public void Added_control_fixture_short_circuits_at_the_row_no_separate_cell_or_control_verdict()
    {
        var a = DiffFixtures.LoadRoot("formxml-control-added/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-control-added/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        // Exactly one Added verdict for the whole walk: the new row. Its cell and control never
        // get their own verdicts, because an Added parent short-circuits its subtree (ruling 3).
        var added = Assert.Single(diff.Verdicts, v => v.Kind == DiffVerdictKind.Added);
        Assert.Equal(DiffClassFamilies.FormXmlRow, added.Identity.ClassKind);
        Assert.Equal(1, diff.Summary.Added);
        Assert.DoesNotContain(diff.Verdicts, v => v.Kind == DiffVerdictKind.Removed);
    }

    [Fact]
    public void Removed_cell_fixture_walks_without_error_and_reports_at_least_one_removal()
    {
        var a = DiffFixtures.LoadRoot("formxml-cell-removed/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-cell-removed/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        // The removed row shifts every row's positional pairing (a's dv_openedon row lines up
        // against b's ownerid row at position 2, since dv_openedon disappeared entirely) -- exactly
        // the false-match risk the model's own honest-fallback section names. The row level alone
        // reports that pairing as Unchanged (rows carry no scalar of their own), but the cell level
        // beneath it catches the real content mismatch as a Removed/Added pair, each still carrying
        // the row's inherited positional warning.
        Assert.True(diff.Summary.Removed >= 1);
        Assert.NotEmpty(diff.Warnings);
    }

    // --- Reordered row through the full walker (7.1i's own flagship, re-proven end to end) -----------

    // The reordered-row fixture proves the exact false-match risk the model's own honest-fallback
    // section names: positions 1 and 2 pair the WRONG rows together (dv_matternumber's row lines up
    // against dv_openedon's row and vice versa). The ROW level alone cannot see this (a row's own
    // scalar diff is always empty, its entire content lives in the excluded "cell" child), so both
    // swapped rows report Unchanged at the row level -- but the CELL family beneath each swapped
    // pairing correctly reports a real mismatch (an Added cell plus a Removed cell per swapped
    // position, since the two positions' cell ids genuinely differ), each still carrying the
    // parent row's inherited positional warning. This is precisely why the recursion rule is law:
    // stopping at the row's own Unchanged verdict would hide the real content difference entirely.
    [Fact]
    public void Reordered_row_fixture_walked_end_to_end_all_four_rows_report_unchanged_but_two_swapped_positions_surface_as_cell_level_mismatches()
    {
        var a = DiffFixtures.LoadRoot("formxml-row-reordered/a.yml");
        var b = DiffFixtures.LoadRoot("formxml-row-reordered/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.FormXmlSystemForm);

        var rowVerdicts = diff.Verdicts.Where(v => v.Identity.ClassKind == DiffClassFamilies.FormXmlRow).ToList();
        Assert.Equal(4, rowVerdicts.Count);
        Assert.All(rowVerdicts, v => Assert.Equal(DiffVerdictKind.Unchanged, v.Kind));
        Assert.All(rowVerdicts, v => Assert.NotEmpty(v.Warnings)); // ruling 3: every positional pairing warns

        // Positions 0 (dv_name) and 3 (ownerid) did not move: their cells genuinely match, Unchanged.
        var untouchedCells = diff.Verdicts.Where(v =>
            v.Identity.ClassKind == DiffClassFamilies.FormXmlCell && v.Kind == DiffVerdictKind.Unchanged);
        Assert.Equal(2, untouchedCells.Count());
        Assert.All(untouchedCells, v => Assert.NotEmpty(v.Warnings)); // still inherits the row's warning

        // Positions 1 and 2 (dv_matternumber and dv_openedon) swapped: each swapped row-pairing's
        // cell family sees genuinely different content on each side, surfacing as one Added and one
        // Removed cell per swapped position -- never a silent false match at the cell level.
        var addedCells = diff.Verdicts.Where(v => v.Identity.ClassKind == DiffClassFamilies.FormXmlCell && v.Kind == DiffVerdictKind.Added);
        var removedCells = diff.Verdicts.Where(v => v.Identity.ClassKind == DiffClassFamilies.FormXmlCell && v.Kind == DiffVerdictKind.Removed);
        Assert.Equal(2, addedCells.Count());
        Assert.Equal(2, removedCells.Count());
        Assert.All(addedCells.Concat(removedCells), v => Assert.NotEmpty(v.Warnings)); // inherited from the row

        Assert.NotEmpty(diff.Warnings);
    }

    // --- Every other family chain the mission ships (ruling 6), lighter literal-YAML coverage --------

    [Fact]
    public void All_fifteen_artifact_classes_are_supported()
    {
        var expected = new[]
        {
            DiffClassFamilies.SolutionManifest, DiffClassFamilies.SolutionComponentsEntry,
            DiffClassFamilies.RootComponentsEntry, DiffClassFamilies.MissingDependencies,
            DiffClassFamilies.Publisher, DiffClassFamilies.Entity, DiffClassFamilies.Attribute,
            DiffClassFamilies.FormXmlSystemForm, DiffClassFamilies.SavedQuery,
            DiffClassFamilies.EntityRelationship, DiffClassFamilies.AppModule,
            DiffClassFamilies.AppModuleSiteMap, DiffClassFamilies.PluginAssembly,
            DiffClassFamilies.SdkMessageProcessingStep, DiffClassFamilies.CanvasScreen
        };

        Assert.Equal(expected.Length, Differ.SupportedArtifactClasses.Count);
        foreach (var family in expected)
            Assert.Contains(family, Differ.SupportedArtifactClasses);
    }

    [Fact]
    public void Unknown_artifact_class_throws_rather_than_returning_an_empty_diff()
    {
        var node = DiffFixtures.ParseRoot("foo: bar\n");
        Assert.Throws<KeyNotFoundException>(() => Differ.Diff(node, node, "NotARealArtifactClass"));
    }

    [Fact]
    public void Solution_manifest_chain_detects_a_version_change()
    {
        var a = DiffFixtures.ParseRoot("""
            ImportExportXml:
              '@version': '9.2'
              SolutionManifest:
                UniqueName: DVerseCore
                Version: 0.5.0.0
                Managed: 0
                Publisher:
                  UniqueName: dversepublisher
            """);
        var b = DiffFixtures.ParseRoot("""
            ImportExportXml:
              '@version': '9.2'
              SolutionManifest:
                UniqueName: DVerseCore
                Version: 0.6.0.0
                Managed: 0
                Publisher:
                  UniqueName: dversepublisher
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.SolutionManifest);

        var verdict = Assert.Single(diff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, verdict.Kind);
        Assert.Contains(verdict.PropertyChanges, p => p.PropertyPath == "Version" && p.ValueInA == "0.5.0.0" && p.ValueInB == "0.6.0.0");
    }

    [Fact]
    public void Publisher_chain_detects_a_rename_as_added_plus_removed()
    {
        var a = DiffFixtures.ParseRoot("Publisher:\n  UniqueName: dversepublisher\n");
        var b = DiffFixtures.ParseRoot("Publisher:\n  UniqueName: dversepublisher2\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.Publisher);

        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Removed);
    }

    [Fact]
    public void Entity_and_attribute_chains_are_independent_leaf_walks()
    {
        var entityA = DiffFixtures.ParseRoot("Entity:\n  Name: dv_Matter\n  EntityInfo:\n    entity:\n      OwnershipTypeMask: UserOwned\n");
        var entityB = DiffFixtures.ParseRoot("Entity:\n  Name: dv_Matter\n  EntityInfo:\n    entity:\n      OwnershipTypeMask: OrganizationOwned\n");

        var entityDiff = Differ.Diff(entityA, entityB, DiffClassFamilies.Entity);
        var entityVerdict = Assert.Single(entityDiff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, entityVerdict.Kind);

        var attributeA = DiffFixtures.ParseRoot("attribute:\n  LogicalName: dv_openedon\n  Format: date\n");
        var attributeB = DiffFixtures.ParseRoot("attribute:\n  LogicalName: dv_openedon\n  Format: DateOnly\n");

        var attributeDiff = Differ.Diff(attributeA, attributeB, DiffClassFamilies.Attribute);
        var attributeVerdict = Assert.Single(attributeDiff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, attributeVerdict.Kind);
        Assert.Contains(attributeVerdict.PropertyChanges, p => p.PropertyPath == "Format" && p.ValueInA == "date" && p.ValueInB == "DateOnly");
    }

    [Fact]
    public void SavedQuery_chain_recurses_into_layout_columns()
    {
        var a = DiffFixtures.ParseRoot("""
            savedquery:
              savedqueryid: 4521f2c8-0e1b-4e51-b549-8161ccbe8979
              querytype: '0'
              layoutxml:
                grid:
                  row:
                    cell:
                      - '@name': dv_name
                        '@width': '300'
                      - '@name': createdon
                        '@width': '125'
            """);
        var b = DiffFixtures.ParseRoot("""
            savedquery:
              savedqueryid: 4521f2c8-0e1b-4e51-b549-8161ccbe8979
              querytype: '0'
              layoutxml:
                grid:
                  row:
                    cell:
                      - '@name': dv_name
                        '@width': '400'
                      - '@name': createdon
                        '@width': '125'
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.SavedQuery);

        var savedQueryVerdict = diff.Verdicts.Single(v => v.Identity.ClassKind == DiffClassFamilies.SavedQuery);
        Assert.Equal(DiffVerdictKind.Unchanged, savedQueryVerdict.Kind); // the change is inside layoutxml, excluded from SavedQuery's own scalars

        var columnVerdict = diff.Verdicts.Single(v => v.Identity.ClassKind == DiffClassFamilies.SavedQueryLayoutColumn && v.Identity.Key == "dv_name");
        Assert.Equal(DiffVerdictKind.Changed, columnVerdict.Kind);
        Assert.Contains(columnVerdict.PropertyChanges, p => p.PropertyPath == "@width" && p.ValueInA == "300" && p.ValueInB == "400");
    }

    [Fact]
    public void EntityRelationship_chain_is_a_leaf_that_detects_a_property_change()
    {
        var a = DiffFixtures.ParseRoot("EntityRelationship:\n  '@Name': dv_matter_SharePointDocumentLocations\n  CascadeAssign: NoCascade\n");
        var b = DiffFixtures.ParseRoot("EntityRelationship:\n  '@Name': dv_matter_SharePointDocumentLocations\n  CascadeAssign: Cascade\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.EntityRelationship);

        var verdict = Assert.Single(diff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, verdict.Kind);
        Assert.Equal(DiffClassFamilies.EntityRelationship, verdict.Identity.ClassKind);
    }

    [Fact]
    public void AppModule_chain_recurses_into_components_and_role_maps_independently()
    {
        var a = DiffFixtures.ParseRoot("""
            AppModule:
              UniqueName: dv_MatterApp
              AppModuleComponents:
                AppModuleComponent:
                  - '@type': '1'
                    '@schemaName': dv_matter
              AppModuleRoleMaps:
                Role:
                  - '@id': '{627090ff-40a3-4053-8790-584edc5be201}'
            """);
        var b = DiffFixtures.ParseRoot("""
            AppModule:
              UniqueName: dv_MatterApp
              AppModuleComponents:
                AppModuleComponent:
                  - '@type': '1'
                    '@schemaName': dv_matter
                  - '@type': '62'
                    '@schemaName': dv_MatterApp
              AppModuleRoleMaps:
                Role: []
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.AppModule);

        Assert.Contains(diff.Verdicts, v => v.Kind == DiffVerdictKind.Added && v.Identity.ClassKind == DiffClassFamilies.AppModuleComponent);
        Assert.Contains(diff.Verdicts, v => v.Kind == DiffVerdictKind.Removed && v.Identity.ClassKind == DiffClassFamilies.AppModuleRoleMapRole);
    }

    [Fact]
    public void SiteMap_chain_walks_area_group_subarea_hierarchically()
    {
        var a = DiffFixtures.ParseRoot("""
            AppModuleSiteMap:
              SiteMapUniqueName: dv_MatterApp
              SiteMap:
                Area:
                  '@Id': area_400223f7
                  Group:
                    '@Id': group_0598b9c1
                    SubArea:
                      '@Id': subarea_eb161827
                      '@Entity': dv_matter
            """);
        var b = DiffFixtures.ParseRoot("""
            AppModuleSiteMap:
              SiteMapUniqueName: dv_MatterApp
              SiteMap:
                Area:
                  '@Id': area_400223f7
                  Group:
                    '@Id': group_0598b9c1
                    SubArea:
                      '@Id': subarea_eb161827
                      '@Entity': dv_probe
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.AppModuleSiteMap);

        var subArea = diff.Verdicts.Single(v => v.Identity.ClassKind == DiffClassFamilies.SiteMapSubArea);
        Assert.Equal(DiffVerdictKind.Changed, subArea.Kind);
        Assert.Contains(subArea.PropertyChanges, p => p.PropertyPath == "@Entity" && p.ValueInA == "dv_matter" && p.ValueInB == "dv_probe");

        Assert.Contains(diff.Verdicts, v => v.Identity.ClassKind == DiffClassFamilies.SiteMapArea && v.Kind == DiffVerdictKind.Unchanged);
        Assert.Contains(diff.Verdicts, v => v.Identity.ClassKind == DiffClassFamilies.SiteMapGroup && v.Kind == DiffVerdictKind.Unchanged);
    }

    [Fact]
    public void Plugin_chain_recurses_from_assembly_into_types_and_sdk_step_is_its_own_leaf()
    {
        var a = DiffFixtures.ParseRoot("""
            PluginAssembly:
              '@PluginAssemblyId': '5af6f42c-9a97-f111-b8de-70a8a59a66f9'
              PluginTypes:
                PluginType:
                  '@PluginTypeId': '0f998a3b-9a97-f111-b8de-70a8a59a66f9'
                  '@Name': DVerse.Plugins.MatterNumberValidator
            """);
        var b = DiffFixtures.ParseRoot("""
            PluginAssembly:
              '@PluginAssemblyId': '5af6f42c-9a97-f111-b8de-70a8a59a66f9'
              PluginTypes:
                PluginType:
                  '@PluginTypeId': '0f998a3b-9a97-f111-b8de-70a8a59a66f9'
                  '@Name': DVerse.Plugins.MatterNumberValidatorV2
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.PluginAssembly);

        var pluginType = diff.Verdicts.Single(v => v.Identity.ClassKind == DiffClassFamilies.PluginType);
        Assert.Equal(DiffVerdictKind.Changed, pluginType.Kind);

        var stepA = DiffFixtures.ParseRoot("SdkMessageProcessingStep:\n  '@SdkMessageProcessingStepId': '{10998a3b-9a97-f111-b8de-70a8a59a66f9}'\n  Stage: '20'\n");
        var stepB = DiffFixtures.ParseRoot("SdkMessageProcessingStep:\n  '@SdkMessageProcessingStepId': '{10998a3b-9a97-f111-b8de-70a8a59a66f9}'\n  Stage: '40'\n");

        var stepDiff = Differ.Diff(stepA, stepB, DiffClassFamilies.SdkMessageProcessingStep);
        var stepVerdict = Assert.Single(stepDiff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, stepVerdict.Kind);
    }

    [Fact]
    public void Canvas_screen_rename_via_the_walker_is_delete_plus_add_not_a_silent_match()
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

        var diff = Differ.Diff(a, b, DiffClassFamilies.CanvasScreen);

        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Removed);
        Assert.Contains(diff.Verdicts, v => v.Identity.Key == "Screen1" && v.Kind == DiffVerdictKind.Unchanged);
        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void RootComponentsEntry_chain_walked_directly_still_warns_on_unsurveyed_types()
    {
        var a = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/a.yml");
        var b = DiffFixtures.LoadRoot("rootcomponent-unsurveyed-type/b.yml");

        var diff = Differ.Diff(a, b, DiffClassFamilies.RootComponentsEntry);

        Assert.NotEmpty(diff.Warnings);
        Assert.Contains(diff.Warnings, w => w.Reason.Contains("is unsurveyed"));
    }

    // --- Unsurveyed families mid-walk never resolve to silence (ruling 6) ----------------------------

    [Fact]
    public void MissingDependencies_real_empty_shape_produces_no_verdict_and_no_warning()
    {
        var a = DiffFixtures.ParseRoot("MissingDependencies: {}\n");
        var b = DiffFixtures.ParseRoot("MissingDependencies: {}\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.MissingDependencies);

        Assert.Empty(diff.Verdicts);
        Assert.Empty(diff.Warnings);
    }

    [Fact]
    public void MissingDependencies_never_before_seen_content_synthesizes_a_conservative_changed_with_warning_verdict()
    {
        var a = DiffFixtures.ParseRoot("MissingDependencies: {}\n");
        var b = DiffFixtures.ParseRoot("MissingDependencies:\n  MissingDependency:\n    - '@type': '1'\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.MissingDependencies);

        // Ruling 6: never silence. Exactly one synthesized Changed-with-warning verdict, even
        // though the underlying UnsurveyedFamilyMatcher itself never populates Matched/Added/Removed.
        var verdict = Assert.Single(diff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, verdict.Kind);
        Assert.True(verdict.Identity.IsWarning);
        Assert.Contains("unsurveyed content present", verdict.Description);
        Assert.NotEmpty(diff.Warnings);
        Assert.Contains(diff.Warnings, w => w.Reason.Contains("is unsurveyed"));
    }

    // --- Missing-vs-empty distinction (ruling 4) ------------------------------------------------------

    [Fact]
    public void Missing_vs_empty_property_distinction_is_preserved()
    {
        var a = DiffFixtures.ParseRoot("Publisher:\n  UniqueName: dversepublisher\n  Description: ''\n");
        var b = DiffFixtures.ParseRoot("Publisher:\n  UniqueName: dversepublisher\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.Publisher);

        var verdict = Assert.Single(diff.Verdicts);
        Assert.Equal(DiffVerdictKind.Changed, verdict.Kind);
        var change = Assert.Single(verdict.PropertyChanges);
        Assert.Equal("Description", change.PropertyPath);
        Assert.Equal("", change.ValueInA);   // present, empty
        Assert.Null(change.ValueInB);        // absent entirely
    }

    [Fact]
    public void Canvas_formula_equals_prefix_is_preserved_verbatim_in_property_changes()
    {
        var a = DiffFixtures.ParseRoot("""
            Screens:
              Screen1:
                Children:
                  - Button1:
                      Properties:
                        OnSelect: =SubmitForm(Form1)
            """);
        var b = DiffFixtures.ParseRoot("""
            Screens:
              Screen1:
                Children:
                  - Button1:
                      Properties:
                        OnSelect: =NewForm(Form1)
            """);

        var diff = Differ.Diff(a, b, DiffClassFamilies.CanvasScreen);

        var verdict = diff.Verdicts.Single(v => v.Identity.ClassKind == DiffClassFamilies.CanvasControl && v.Kind == DiffVerdictKind.Changed);
        var change = Assert.Single(verdict.PropertyChanges);
        Assert.Equal("=SubmitForm(Form1)", change.ValueInA);
        Assert.Equal("=NewForm(Form1)", change.ValueInB);
    }

    [Fact]
    public void Attribute_order_in_yaml_mappings_is_not_significant()
    {
        var a = DiffFixtures.ParseRoot("Publisher:\n  UniqueName: dversepublisher\n  Prefix: dv\n  Description: x\n");
        var b = DiffFixtures.ParseRoot("Publisher:\n  Description: x\n  UniqueName: dversepublisher\n  Prefix: dv\n");

        var diff = Differ.Diff(a, b, DiffClassFamilies.Publisher);

        var verdict = Assert.Single(diff.Verdicts);
        Assert.Equal(DiffVerdictKind.Unchanged, verdict.Kind);
        Assert.Empty(verdict.PropertyChanges);
    }
}
