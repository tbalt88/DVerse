using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G10 exists because of a documented misleading failure: a YAML manifest one
/// directory too high makes SolutionPackager fall back to XML and complain
/// about a missing Customizations.xml, pointing a developer at the wrong file
/// entirely. These tests prove the gate catches the real cause and states it
/// plainly, and that it fails closed (Refuse, never a crash or a false Pass)
/// on anything it cannot positively confirm as conforming.
/// </summary>
public sealed class YamlLayoutGateTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private readonly List<string> _tempDirs = [];

    private static YamlLayoutGate Gate() => new();

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
            var candidate = Path.Combine(dir.FullName, "fixtures", "g10");
            if (Directory.Exists(candidate))
                return Path.Combine(dir.FullName, "fixtures");

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate harness/fixtures/g10 above {AppContext.BaseDirectory}.");
    }

    private static string Fixture(string name) => Path.Combine(FixturesRoot(), "g10", name);

    private string NewEmptyDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dverse-g10-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    [Fact]
    public void Conforming_single_solution_layout_yields_one_pass_with_evidence()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("pass"))).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));
        Assert.Contains("solution.yml", verdict.Evidence);
        Assert.Contains("publisher.yml", verdict.Evidence);
        Assert.Null(verdict.Reason);
        Assert.Equal("G10", verdict.GateId);
        Assert.Equal("yaml-layout", verdict.GateName);
        Assert.Equal(FixedNow, verdict.At);
    }

    [Fact]
    public void Multi_solution_layout_passes_and_states_the_count_in_evidence()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("pass-multi"))).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("2", verdict.Evidence);
        Assert.Contains("DVerseCore", verdict.Evidence);
        Assert.Contains("DVerseExtras", verdict.Evidence);
        Assert.Contains("explicit solution name", verdict.Evidence);
    }

    [Fact]
    public void Manifests_at_root_refuse_and_name_both_the_misplaced_file_and_the_Customizations_xml_symptom()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-manifests-at-root"))).ToList();

        Assert.NotEmpty(verdicts);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));

        // At least one verdict must connect the misplaced manifest to the
        // exact misleading symptom a developer would otherwise chase.
        var trapVerdict = verdicts.FirstOrDefault(v =>
            v.Reason is not null &&
            v.Reason.Contains("solution.yml", StringComparison.Ordinal) &&
            v.Reason.Contains("Customizations.xml", StringComparison.Ordinal));

        Assert.NotNull(trapVerdict);
        Assert.False(string.IsNullOrWhiteSpace(trapVerdict!.Evidence));
    }

    [Fact]
    public void Missing_publishers_directory_refuses()
    {
        var verdicts = Gate().Evaluate(ContextFor(Fixture("refuse-no-publishers"))).ToList();

        var refusal = Assert.Single(verdicts, v => v.Outcome == GateOutcome.Refuse);
        Assert.Contains("publisher", refusal.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Evidence));
    }

    [Fact]
    public void An_entirely_empty_directory_refuses_rather_than_crashing_or_passing()
    {
        var empty = NewEmptyDirectory();

        var verdicts = Gate().Evaluate(ContextFor(empty)).ToList();

        Assert.NotEmpty(verdicts);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));
        Assert.All(verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Reason)));
    }

    [Fact]
    public void No_verdict_ever_carries_an_absolute_filesystem_path()
    {
        var fixtures = new[] { "pass", "pass-multi", "refuse-manifests-at-root", "refuse-no-publishers" };

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

        // Also cover the empty-directory case, whose SolutionRoot lives outside
        // the fixtures tree entirely.
        var empty = NewEmptyDirectory();
        var emptyContext = ContextFor(empty);
        foreach (var verdict in Gate().Evaluate(emptyContext))
        {
            Assert.False(Path.IsPathRooted(verdict.Artifact));
            Assert.DoesNotContain(empty, verdict.Artifact, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RequiresTenant_is_false()
    {
        Assert.False(Gate().RequiresTenant);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
