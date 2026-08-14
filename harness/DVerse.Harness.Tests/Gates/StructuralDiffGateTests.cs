using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G12 (structural-diff): the diff-aware gate (slice 7.3). Unlike every
/// other gate's own test file, this one needs TWO trees per fixture
/// (baseline/ and target/ subfolders, discovered from disk rather than
/// hardcoded, mirroring the integration-sweep discovery every other gate's
/// fixture family already gets from WaveOneIntegrationTests -- G12 gets its
/// own sweep here instead of reusing that one, because its two-root shape
/// does not fit that file's single-SolutionRoot-per-fixture loop).
/// </summary>
public sealed class StructuralDiffGateTests : IDisposable
{
    private readonly string _ledgerDir = Path.Combine(
        Path.GetTempPath(), "dverse-g12-tests", Guid.NewGuid().ToString("N"));

    private static string FixtureRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
                dir = dir.Parent;
            return dir is null
                ? throw new DirectoryNotFoundException("fixtures root not found")
                : Path.Combine(dir.FullName, "fixtures");
        }
    }

    private static string G12Root => Path.Combine(FixtureRoot, "g12");

    private GateRunResult Run(string fixtureName, string ledgerName)
    {
        var baseline = Path.Combine(G12Root, fixtureName, "baseline");
        var target = Path.Combine(G12Root, fixtureName, "target");

        var ledger = new JsonlRefusalLedger(Path.Combine(_ledgerDir, ledgerName));
        var gate = new StructuralDiffGate(baselineRoot: baseline);

        var context = new GateContext(
            RepositoryRoot: FixtureRoot,
            SolutionRoot: target,
            Stage: GateStage.Integration,
            HasTenantCredentials: false);

        return new GateRunner(ledger).Run([gate], context);
    }

    // --- Integration sweep: every g12/refuse-* fixture actually refuses, every g12/pass-* passes ------

    public static TheoryData<string> RedFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(G12Root, "refuse-*").OrderBy(d => d, StringComparer.Ordinal))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    public static TheoryData<string> GreenFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(G12Root, "pass-*").OrderBy(d => d, StringComparer.Ordinal))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    [Theory]
    [MemberData(nameof(RedFixtures))]
    public void Every_red_fixture_refuses_with_an_explained_reason(string fixtureName)
    {
        var result = Run(fixtureName, $"{fixtureName}.jsonl");

        Assert.False(result.Passed, $"g12/{fixtureName} is named as a red fixture but G12 let it through.");
        Assert.NotEmpty(result.Refusals);
        Assert.Equal("G12", Assert.Single(result.Refusals.Select(r => r.GateId).Distinct()));
        Assert.All(result.Refusals, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
        Assert.All(result.Refusals, r => Assert.False(string.IsNullOrWhiteSpace(r.Evidence)));

        var persisted = new JsonlRefusalLedger(Path.Combine(_ledgerDir, $"{fixtureName}.jsonl")).Read();
        Assert.Contains(persisted, v => v.Outcome == GateOutcome.Refuse);
    }

    [Theory]
    [MemberData(nameof(GreenFixtures))]
    public void Every_green_fixture_passes_with_real_evidence(string fixtureName)
    {
        var result = Run(fixtureName, $"{fixtureName}.jsonl");

        Assert.True(result.Passed,
            $"g12/{fixtureName} should pass. Refusals: " + string.Join(" | ", result.Refusals.Select(r => r.Reason)));
        Assert.NotEmpty(result.Verdicts);
        Assert.All(result.Verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));
    }

    // --- Refuse-class-specific assertions on the named fixtures -----------------------------------------

    [Fact]
    public void Datafieldname_casing_fixture_names_the_control_and_both_values_in_the_reason()
    {
        var result = Run("refuse-datafieldname-casing", "datafieldname.jsonl");

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("dv_openedon", refusal.Reason);
        Assert.Contains("dv_OpenedOn", refusal.Reason);
        Assert.Contains("LESSONS.md #14", refusal.Reason);
    }

    [Fact]
    public void Unsurveyed_type_fixture_refuses_on_the_matched_but_unsurveyed_root_component()
    {
        var result = Run("refuse-unsurveyed-type", "unsurveyed.jsonl");

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("unsurveyed-type", refusal.Reason);
        Assert.Contains("ruling 8", refusal.Reason);
        Assert.Contains("dv_customrole", refusal.Reason);
    }

    [Fact]
    public void Packaging_removal_fixture_refuses_and_names_the_still_present_source()
    {
        var result = Run("refuse-packaging-removal", "packaging.jsonl");

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("entities/dv_matter/FormXml/main", refusal.Reason);
        Assert.Contains("LESSONS.md #2", refusal.Reason);
        Assert.Contains("still exists on disk", refusal.Reason);
    }

    [Fact]
    public void Label_edit_fixture_passes_with_the_changed_property_path_visible_in_evidence()
    {
        var result = Run("pass-label-edit", "label.jsonl");

        Assert.True(result.Passed);
        var pass = Assert.Single(result.Verdicts);
        Assert.Contains("1 changed", pass.Evidence);
        Assert.Contains("labels.label.@description", pass.Evidence);
    }

    // --- Ruling 1: no baseline supplied is an honest SKIP, verbatim reason, unaffected by target content -

    [Fact]
    public void No_baseline_is_an_honest_skip_with_the_frozen_reason_verbatim()
    {
        var solutionRoot = Path.Combine(G12Root, "pass-label-edit", "target");
        var gate = new StructuralDiffGate(baselineRoot: null);

        var context = new GateContext(
            RepositoryRoot: FixtureRoot,
            SolutionRoot: solutionRoot,
            Stage: GateStage.Integration,
            HasTenantCredentials: false);

        var verdict = Assert.Single(gate.Evaluate(context));

        Assert.Equal(GateOutcome.Skip, verdict.Outcome);
        Assert.Equal("no baseline provided; structural diff requires two trees", verdict.Reason);
    }

    [Fact]
    public void A_missing_baseline_directory_refuses_rather_than_skips()
    {
        var solutionRoot = Path.Combine(G12Root, "pass-label-edit", "target");
        var bogusBaseline = Path.Combine(_ledgerDir, "does-not-exist-" + Guid.NewGuid().ToString("N"));
        var gate = new StructuralDiffGate(baselineRoot: bogusBaseline);

        var context = new GateContext(
            RepositoryRoot: FixtureRoot,
            SolutionRoot: solutionRoot,
            Stage: GateStage.Integration,
            HasTenantCredentials: false);

        var verdict = Assert.Single(gate.Evaluate(context));

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.DoesNotContain(bogusBaseline, verdict.Reason);
        Assert.DoesNotContain(bogusBaseline, verdict.Evidence);
    }

    // --- Whole-file add/remove: no recursion, PASS (mission ruling 3) -----------------------------------

    [Fact]
    public void A_file_present_only_in_the_target_tree_passes_as_added_with_no_recursion()
    {
        var baseline = Path.Combine(_ledgerDir, "wf-baseline");
        var target = Path.Combine(_ledgerDir, "wf-target");
        Directory.CreateDirectory(Path.Combine(baseline, "publishers", "dversepublisher"));
        Directory.CreateDirectory(Path.Combine(target, "publishers", "dversepublisher"));

        File.WriteAllText(
            Path.Combine(target, "publishers", "dversepublisher", "publisher.yml"),
            "Publisher:\n  UniqueName: dversepublisher\n");

        var gate = new StructuralDiffGate(baselineRoot: baseline);
        var context = new GateContext(_ledgerDir, target, GateStage.Integration, false);

        var verdict = Assert.Single(gate.Evaluate(context));
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("present in the current tree only", verdict.Evidence);
    }

    [Fact]
    public void A_file_present_only_in_the_baseline_tree_passes_as_removed_with_no_recursion()
    {
        var baseline = Path.Combine(_ledgerDir, "wf2-baseline");
        var target = Path.Combine(_ledgerDir, "wf2-target");
        Directory.CreateDirectory(Path.Combine(baseline, "publishers", "dversepublisher"));
        Directory.CreateDirectory(target);

        File.WriteAllText(
            Path.Combine(baseline, "publishers", "dversepublisher", "publisher.yml"),
            "Publisher:\n  UniqueName: dversepublisher\n");

        var gate = new StructuralDiffGate(baselineRoot: baseline);
        var context = new GateContext(_ledgerDir, target, GateStage.Integration, false);

        var verdict = Assert.Single(gate.Evaluate(context));
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("present in the baseline tree only", verdict.Evidence);
    }

    // --- Refuse class 2, control side (seat ruling 3): an unsurveyed classid must also refuse -----------

    [Fact]
    public void An_unsurveyed_control_class_refuses_even_when_matched_and_unchanged()
    {
        const string form = """
            systemform:
              formid: 11111111-1111-1111-1111-111111111111
              form:
                tabs:
                  tab:
                    '@id': '{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}'
                    columns:
                      column:
                        '@width': '100%'
                        sections:
                          section:
                            '@id': '{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}'
                            rows:
                              row:
                                cell:
                                  '@id': '{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}'
                                  control:
                                    '@id': dv_owningteam
                                    '@classid': '{5C5600E8-1D6E-4348-9D80-B061B22A2C1E}'
                                    '@datafieldname': dv_owningteam
            """;

        var baseline = Path.Combine(_ledgerDir, "uc-baseline");
        var target = Path.Combine(_ledgerDir, "uc-target");
        Directory.CreateDirectory(Path.Combine(baseline, "entities", "dv_matter", "FormXml", "main"));
        Directory.CreateDirectory(Path.Combine(target, "entities", "dv_matter", "FormXml", "main"));

        var relFile = Path.Combine("entities", "dv_matter", "FormXml", "main", "dv_matter_main.yml");
        File.WriteAllText(Path.Combine(baseline, relFile), form);
        File.WriteAllText(Path.Combine(target, relFile), form);

        var gate = new StructuralDiffGate(baselineRoot: baseline);
        var context = new GateContext(_ledgerDir, target, GateStage.Integration, false);

        var refusal = Assert.Single(gate.Evaluate(context), v => v.Outcome == GateOutcome.Refuse);
        Assert.Contains("unsurveyed-control", refusal.Reason);
        Assert.Contains("ruling 3", refusal.Reason);
        Assert.Contains("dv_owningteam", refusal.Reason);
    }

    // --- Refuse class 3, negative: a removal whose source is genuinely gone must NOT refuse --------------

    [Fact]
    public void A_root_component_removal_whose_source_is_genuinely_absent_does_not_refuse()
    {
        const string baselineYaml = """
            RootComponents:
              RootComponent:
                - '@type': '1'
                  '@schemaName': dv_gonefortgood
                  '@behavior': '0'
            """;
        const string targetYaml = """
            RootComponents:
              RootComponent: {}
            """;

        var baseline = Path.Combine(_ledgerDir, "neg-baseline");
        var target = Path.Combine(_ledgerDir, "neg-target");
        Directory.CreateDirectory(Path.Combine(baseline, "solutions", "DVerseCore"));
        Directory.CreateDirectory(Path.Combine(target, "solutions", "DVerseCore"));

        File.WriteAllText(Path.Combine(baseline, "solutions", "DVerseCore", "rootcomponents.yml"), baselineYaml);
        File.WriteAllText(Path.Combine(target, "solutions", "DVerseCore", "rootcomponents.yml"), targetYaml);

        // Deliberately no entities/dv_gonefortgood folder anywhere: the component was genuinely
        // deleted, source and all, so this is an ordinary removal, not a packaging trap.

        var gate = new StructuralDiffGate(baselineRoot: baseline);
        var context = new GateContext(_ledgerDir, target, GateStage.Integration, false);

        var verdicts = gate.Evaluate(context).ToList();
        Assert.DoesNotContain(verdicts, v => v.Outcome == GateOutcome.Refuse);
        Assert.Contains(verdicts, v => v.Outcome == GateOutcome.Pass);
    }

    // --- No absolute path ever leaks into a verdict, the same standing rule every other gate proves ------

    [Fact]
    public void No_verdict_leaks_an_absolute_path()
    {
        foreach (var fixtureName in new[]
                 {
                     "pass-label-edit", "refuse-datafieldname-casing",
                     "refuse-unsurveyed-type", "refuse-packaging-removal"
                 })
        {
            var result = Run(fixtureName, $"leak-{fixtureName}.jsonl");

            Assert.All(result.Verdicts, v =>
            {
                Assert.False(Path.IsPathRooted(v.Artifact), $"{fixtureName}: Artifact '{v.Artifact}' is rooted.");
                Assert.DoesNotContain("C:\\", v.Artifact, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("C:\\", v.Evidence, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("C:\\", v.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_ledgerDir))
            Directory.Delete(_ledgerDir, recursive: true);
    }
}
