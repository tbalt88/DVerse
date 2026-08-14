using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G11 exists because <c>pac canvas validate</c> is listed by
/// <c>pac canvas help</c> yet is no longer supported by pac 2.10.1, so nothing
/// checked <c>.pa.yaml</c> canvas sources before this gate. These tests prove
/// the gate parses the real committed shape (trimmed from
/// <c>demo-solution/canvasapps/MatterCanvas/</c>), refuses each of the named
/// silent-failure classes (unparseable YAML, a control with no Control:
/// declaration, a Properties value that is not a '='-prefixed formula, an
/// empty file), and passes vacuously when a solution has no canvas sources at
/// all.
/// </summary>
public sealed class CanvasYamlGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-13T12:00:00-07:00");

    private static CanvasYamlGate Gate() => new();

    private static GateContext ContextFor(string fixtureRoot) => new(
        RepositoryRoot: FixturesRoot(),
        SolutionRoot: fixtureRoot,
        Stage: GateStage.Generation,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    /// <summary>
    /// Resolves harness/fixtures relative to the test assembly's own location,
    /// so the tests run correctly regardless of which machine or working
    /// directory dotnet test is invoked from.
    /// </summary>
    private static string FixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixtures", "g11");
            if (Directory.Exists(candidate))
                return Path.Combine(dir.FullName, "fixtures");

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate harness/fixtures/g11 above {AppContext.BaseDirectory}.");
    }

    private static string Fixture(string name) => Path.Combine(FixturesRoot(), "g11", name);

    [Fact]
    public void Conforming_canvas_sources_yield_one_pass_naming_files_and_control_count()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("pass"))).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Equal("G11", verdict.GateId);
        Assert.Equal("canvas-yaml", verdict.GateName);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));
        Assert.Contains("App.pa.yaml", verdict.Evidence);
        Assert.Contains("Screen1.pa.yaml", verdict.Evidence);
        Assert.Contains("_EditorState.pa.yaml", verdict.Evidence);
        // Gallery1, Image1, Title1.
        Assert.Contains("3 control(s)", verdict.Evidence);
        Assert.Null(verdict.Reason);
        Assert.Equal(FixedNow, verdict.At);
    }

    [Fact]
    public void A_solution_with_no_canvasapps_folder_passes_with_no_canvas_sources_evidence()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("pass-no-canvas"))).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("no canvas sources", verdict.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Unparseable_yaml_refuses_and_names_the_parse_error()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-unparseable-yaml"))).ToList();

        var refusal = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, refusal.Outcome);
        Assert.Contains("App.pa.yaml", refusal.Artifact);
        Assert.Contains("does not parse", refusal.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Evidence));
    }

    [Fact]
    public void A_control_entry_with_no_Control_declaration_refuses_and_names_the_control()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-missing-control"))).ToList();

        var refusal = Assert.Single(verdicts, v => v.Outcome == GateOutcome.Refuse);
        Assert.Contains("Gallery1", refusal.Reason);
        Assert.Contains("Control:", refusal.Reason);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Evidence));
    }

    [Fact]
    public void A_non_formula_property_value_refuses_and_names_the_property()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-non-formula-property"))).ToList();

        var refusal = Assert.Single(verdicts, v => v.Outcome == GateOutcome.Refuse);
        Assert.Contains("Theme", refusal.Reason);
        Assert.Contains("'='", refusal.Reason);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Evidence));
    }

    [Fact]
    public void An_empty_pa_yaml_file_refuses_rather_than_being_treated_as_a_valid_empty_tree()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-empty-file"))).ToList();

        var refusal = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, refusal.Outcome);
        Assert.Contains("empty", refusal.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Evidence));
    }

    [Fact]
    public void Every_refuse_fixture_actually_refuses()
    {
        var refuseFixtures = new[]
        {
            "refuse-unparseable-yaml",
            "refuse-missing-control",
            "refuse-non-formula-property",
            "refuse-empty-file"
        };

        foreach (var fixture in refuseFixtures)
        {
            var verdicts = Gate().Evaluate(ContextFor(Fixture(fixture))).ToList();

            Assert.NotEmpty(verdicts);
            Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));
            Assert.All(verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Reason)));
            Assert.All(verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));
        }
    }

    [Fact]
    public void No_verdict_ever_carries_an_absolute_filesystem_path()
    {
        var fixtures = new[]
        {
            "pass", "pass-no-canvas", "refuse-unparseable-yaml",
            "refuse-missing-control", "refuse-non-formula-property", "refuse-empty-file"
        };

        foreach (var fixture in fixtures)
        {
            var context = ContextFor(Fixture(fixture));
            var verdicts = Gate().Evaluate(context).ToList();

            Assert.NotEmpty(verdicts);

            foreach (var verdict in verdicts)
            {
                Assert.False(Path.IsPathRooted(verdict.Artifact),
                    $"Artifact '{verdict.Artifact}' must be repo-relative, never absolute.");
                Assert.DoesNotContain(context.RepositoryRoot, verdict.Artifact, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(context.RepositoryRoot, verdict.Evidence, StringComparison.OrdinalIgnoreCase);

                if (verdict.Reason is not null)
                    Assert.DoesNotContain(context.RepositoryRoot, verdict.Reason, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void RequiresTenant_is_false()
    {
        Assert.False(Gate().RequiresTenant);
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
