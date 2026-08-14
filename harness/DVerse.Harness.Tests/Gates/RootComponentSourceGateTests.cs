using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G8 exists because SolutionPackager's documented behaviour turns a missing
/// root component source into a silent, exit-code-0 pack (standing
/// obligation O2). These tests prove the gate catches that before the pack
/// ever runs, honors the unmapped-type honesty rule (an unrecognized
/// component type is noted, never refused), and fails closed (Refuse, never
/// a crash or a Pass) when rootcomponents.yml is absent, malformed, or in
/// the wrong (list) shape.
/// </summary>
public sealed class RootComponentSourceGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-13T09:00:00-07:00");

    private static readonly RootComponentSourceGate Gate = new();

    [Fact]
    public void Empty_mapping_and_fully_resolved_solutions_both_pass()
    {
        var verdicts = Evaluate("pass");

        Assert.Equal(2, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Pass, v.Outcome));
        Assert.All(verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));
        Assert.All(verdicts, v => Assert.Null(v.Reason));

        var emptySolutionVerdict = verdicts.Single(v => v.Artifact.Contains("empty-solution"));
        Assert.Contains("empty mapping", emptySolutionVerdict.Evidence);

        var richSolutionVerdict = verdicts.Single(v => v.Artifact.Contains("rich-solution"));
        Assert.Contains("resolved 2 of 4", richSolutionVerdict.Evidence);
    }

    [Fact]
    public void Unmapped_component_type_is_noted_in_pass_evidence_not_refused()
    {
        var verdicts = Evaluate("pass");

        var richSolutionVerdict = verdicts.Single(v => v.Artifact.Contains("rich-solution"));
        Assert.Equal(GateOutcome.Pass, richSolutionVerdict.Outcome);
        Assert.Contains("dv_customrole", richSolutionVerdict.Evidence);
        Assert.Contains("type 20", richSolutionVerdict.Evidence);
        Assert.Contains("Not refused", richSolutionVerdict.Evidence);
    }

    [Fact]
    public void Id_only_root_component_is_accepted_and_noted_in_pass_evidence_not_refused()
    {
        // Slice 4.4c / G8 fix: the platform's own canonical export
        // (loop/specs/wave4-4c-canonical-plugin-shapes.md) carries a
        // SdkMessageProcessingStep RootComponent with '@id' and NO
        // '@schemaName' at all. G8 must accept that shape, skipping source
        // verification with an honest note, not refuse it.
        var verdicts = Evaluate("pass");

        var richSolutionVerdict = verdicts.Single(v => v.Artifact.Contains("rich-solution"));
        Assert.Equal(GateOutcome.Pass, richSolutionVerdict.Outcome);
        Assert.Null(richSolutionVerdict.Reason);
        Assert.Contains("10998a3b-9a97-f111-b8de-70a8a59a66f9", richSolutionVerdict.Evidence);
        Assert.Contains("'@id' only", richSolutionVerdict.Evidence);
        Assert.Contains("Not refused", richSolutionVerdict.Evidence);
    }

    [Fact]
    public void Root_component_with_neither_id_nor_schema_name_still_refuses()
    {
        // The red half of the G8 fix's fixture pair: an entry with NEITHER
        // '@id' NOR '@schemaName' has no way to be resolved OR honestly
        // skipped, so it must still refuse.
        var verdicts = Evaluate("refuse-no-identifier");

        var verdict = verdicts.Single(v => v.Artifact.Contains("only-solution"));
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("neither a '@schemaName' nor an '@id' value", verdict.Reason);
        Assert.Contains("both its", verdict.Evidence);
    }

    [Fact]
    public void Missing_entity_sources_yield_individually_visible_refuse_verdicts()
    {
        var verdicts = Evaluate("refuse-missing-entity-source");

        Assert.Equal(2, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));

        var namedPaths = verdicts.Select(v => v.Evidence).ToList();
        Assert.Contains(namedPaths, e => e.Contains("entities/dv_ghost_one"));
        Assert.Contains(namedPaths, e => e.Contains("entities/dv_ghost_two"));

        Assert.All(verdicts, v =>
        {
            Assert.NotNull(v.Reason);
            Assert.Contains("exit 0", v.Reason);
        });
    }

    [Fact]
    public void Absent_rootcomponents_file_yields_refuse_not_a_crash_or_a_pass()
    {
        var verdicts = Evaluate("refuse-missing-file");

        var verdict = verdicts.Single(v => v.Artifact.Contains("no-manifest"));
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("rootcomponents.yml is missing", verdict.Reason);
    }

    [Fact]
    public void Malformed_yaml_yields_refuse_not_a_crash_or_a_pass()
    {
        var verdicts = Evaluate("refuse-missing-file");

        var verdict = verdicts.Single(v => v.Artifact.Contains("malformed"));
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("could not be parsed as YAML", verdict.Reason);
    }

    [Fact]
    public void List_shaped_root_components_yields_refuse_naming_the_pac_verified_mapping_shape()
    {
        var verdicts = Evaluate("refuse-missing-file");

        var verdict = verdicts.Single(v => v.Artifact.Contains("list-shape"));
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("YAML list", verdict.Reason);
        Assert.Contains("RootComponents: {}", verdict.Reason);
    }

    [Fact]
    public void Refuse_missing_file_fixture_yields_exactly_three_refusals_one_per_solution()
    {
        var verdicts = Evaluate("refuse-missing-file");

        Assert.Equal(3, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));
    }

    [Fact]
    public void No_verdict_across_any_fixture_contains_an_absolute_filesystem_path()
    {
        string[] fixtures =
        [
            "pass", "refuse-missing-entity-source", "refuse-missing-file", "refuse-no-identifier"
        ];

        foreach (var fixture in fixtures)
        {
            foreach (var verdict in Evaluate(fixture))
            {
                Assert.False(Path.IsPathRooted(verdict.Artifact),
                    $"[{fixture}] Artifact must be repo relative: {verdict.Artifact}");
                Assert.DoesNotContain(FixtureRoot(fixture), verdict.Evidence);
                Assert.DoesNotContain(FixtureRoot(fixture), verdict.Reason ?? string.Empty);
            }
        }
    }

    // Helpers.

    private static List<GateVerdict> Evaluate(string fixtureName)
    {
        var root = FixtureRoot(fixtureName);
        var context = new GateContext(
            RepositoryRoot: root,
            SolutionRoot: root,
            Stage: GateStage.Generation,
            HasTenantCredentials: false)
        {
            Time = new FakeTime(FixedNow)
        };

        var verdicts = Gate.Evaluate(context).ToList();

        // Every verdict must satisfy the contract's own invariants; a gate test
        // that never calls Validate could pass while shipping an unrecordable verdict.
        foreach (var verdict in verdicts)
            verdict.Validate();

        Assert.All(verdicts, v => Assert.Equal(FixedNow, v.At));
        Assert.All(verdicts, v => Assert.Equal("G8", v.GateId));

        return verdicts;
    }

    private static string FixtureRoot(string fixtureName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException(
                $"Could not locate a 'fixtures' folder above {AppContext.BaseDirectory}.");

        var fixturePath = Path.Combine(dir.FullName, "fixtures", "g8", fixtureName);

        if (!Directory.Exists(fixturePath))
            throw new DirectoryNotFoundException($"Fixture not found: {fixturePath}");

        return fixturePath;
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
