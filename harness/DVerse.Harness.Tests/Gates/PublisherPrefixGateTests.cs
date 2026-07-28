using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G2 enforces two mechanical rules against the YAML source control export:
/// publisher.yml declares CustomizationPrefix: dv, and every entities/
/// directory is dv_ prefixed. These tests prove both rules fire, that the
/// conforming fixture passes with evidence, that a missing publishers/
/// directory refuses rather than crashing or passing, and that no verdict
/// leaks an absolute filesystem path.
/// </summary>
public sealed class PublisherPrefixGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private static readonly PublisherPrefixGate Gate = new();

    [Fact]
    public void Conforming_fixture_passes_with_evidence()
    {
        var context = Context(Fixture("pass"));
        var verdicts = Gate.Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));
        Assert.Contains("publisher.yml", verdict.Evidence);
        Assert.Contains("entities", verdict.Evidence);
        Assert.Null(verdict.Reason);
        Assert.Equal(FixedNow, verdict.At);
        Assert.Equal("G2", verdict.GateId);
    }

    [Fact]
    public void Wrong_publisher_prefix_refuses_and_names_the_offending_prefix()
    {
        var context = Context(Fixture("refuse-wrong-prefix"));
        var verdicts = Gate.Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("'ms'", verdict.Reason);
        Assert.Contains("dv", verdict.Reason);
    }

    [Fact]
    public void Unprefixed_entity_directory_refuses_and_names_the_offending_directory()
    {
        var context = Context(Fixture("refuse-unprefixed-entity"));
        var verdicts = Gate.Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("'matter'", verdict.Reason);
        Assert.Contains("entities/matter", verdict.Artifact);
    }

    [Fact]
    public void Missing_publishers_directory_refuses_rather_than_crashing_or_passing()
    {
        var solutionRoot = CreateEmptyTempDirectory();
        try
        {
            var context = Context(solutionRoot);

            // Must not throw.
            var verdicts = Gate.Evaluate(context).ToList();

            var verdict = Assert.Single(verdicts);
            Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
            Assert.NotNull(verdict.Reason);
            Assert.Contains("publishers", verdict.Reason);
        }
        finally
        {
            Directory.Delete(solutionRoot, recursive: true);
        }
    }

    [Fact]
    public void No_verdict_contains_an_absolute_filesystem_path()
    {
        var fixtures = new[] { "pass", "refuse-wrong-prefix", "refuse-unprefixed-entity" };

        foreach (var fixture in fixtures)
        {
            var solutionRoot = Fixture(fixture);
            var context = Context(solutionRoot);

            foreach (var verdict in Gate.Evaluate(context))
            {
                Assert.False(Path.IsPathRooted(verdict.Artifact),
                    $"[{fixture}] Artifact '{verdict.Artifact}' looks absolute.");
                Assert.DoesNotContain(solutionRoot, verdict.Artifact);
                Assert.DoesNotContain(":\\", verdict.Artifact);
            }
        }
    }

    // Helpers.

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static GateContext Context(string solutionRoot) => new(
        RepositoryRoot: FixturesRoot(),
        SolutionRoot: solutionRoot,
        Stage: GateStage.Generation,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    private static string CreateEmptyTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dverse-g2-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Locates harness/fixtures/g2 by walking up from the test assembly's own
    /// directory, so the tests work from any machine and any build
    /// configuration without a hardcoded path or a csproj content-copy item.
    /// </summary>
    private static string FixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures", "g2")))
            dir = dir.Parent;

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                "Could not locate fixtures/g2 above the test assembly at " + AppContext.BaseDirectory);
        }

        return Path.Combine(dir.FullName, "fixtures", "g2");
    }

    private static string Fixture(string name) => Path.Combine(FixturesRoot(), name);
}
