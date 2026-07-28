using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G9 exists because SolutionPackager's documented behaviour turns a missing
/// component source into a silent, exit-code-0 pack. These tests prove the
/// gate catches that before the pack ever runs, and that it fails closed
/// (Refuse, never a crash or a Pass) when it cannot verify anything at all.
/// </summary>
public sealed class SolutionComponentPathGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private static readonly SolutionComponentPathGate Gate = new();

    [Fact]
    public void Conforming_fixture_yields_a_single_pass_with_non_empty_evidence()
    {
        var verdicts = Evaluate("pass");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Missing_canvas_app_directory_yields_refuse_naming_the_unresolved_path()
    {
        var verdicts = Evaluate("refuse-missing-canvasapp");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("canvasapps/dv_matterapp", verdict.Evidence);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("canvasapps/dv_matterapp", verdict.Reason);
    }

    [Fact]
    public void Missing_entity_directory_yields_refuse_naming_the_unresolved_path()
    {
        var verdicts = Evaluate("refuse-missing-entity");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("entities/dv_ghost", verdict.Evidence);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("entities/dv_ghost", verdict.Reason);
    }

    [Fact]
    public void Three_bad_paths_yield_three_individually_visible_refuse_verdicts()
    {
        var verdicts = Evaluate("refuse-three-missing");

        Assert.Equal(3, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));

        var namedPaths = verdicts.Select(v => v.Evidence).ToList();
        Assert.Contains(namedPaths, e => e.Contains("entities/dv_ghost_one"));
        Assert.Contains(namedPaths, e => e.Contains("entities/dv_ghost_two"));
        Assert.Contains(namedPaths, e => e.Contains("canvasapps/dv_ghost_app"));
    }

    [Fact]
    public void Absent_solutioncomponents_file_yields_refuse_not_a_crash_or_a_pass()
    {
        var verdicts = Evaluate("refuse-missing-manifest");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("solutioncomponents.yml", verdict.Reason);
    }

    [Fact]
    public void Empty_solutioncomponents_file_yields_refuse_not_a_crash_or_a_pass()
    {
        var verdicts = Evaluate("refuse-empty-manifest");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
    }

    [Fact]
    public void No_verdict_across_any_fixture_contains_an_absolute_filesystem_path()
    {
        string[] fixtures =
        [
            "pass", "refuse-missing-canvasapp", "refuse-missing-entity",
            "refuse-three-missing", "refuse-missing-manifest", "refuse-empty-manifest"
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
        Assert.All(verdicts, v => Assert.Equal("G9", v.GateId));

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

        var fixturePath = Path.Combine(dir.FullName, "fixtures", "g9", fixtureName);

        if (!Directory.Exists(fixturePath))
            throw new DirectoryNotFoundException($"Fixture not found: {fixturePath}");

        return fixturePath;
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
