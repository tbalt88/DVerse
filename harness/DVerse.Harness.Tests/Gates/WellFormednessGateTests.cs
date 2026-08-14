using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G1 is the entry gate: every YAML and XML file under the solution root must
/// parse before any other gate trusts what it reads. These tests prove the
/// happy path aggregates into one Pass naming counts by extension, that a
/// malformed YAML or XML file refuses with the parser's own line/column detail
/// rather than crashing or passing, that zero candidate files is a Pass and
/// not a refusal, and that no verdict leaks an absolute filesystem path.
/// </summary>
public sealed class WellFormednessGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-13T09:00:00-07:00");

    private static readonly WellFormednessGate Gate = new();

    [Fact]
    public void Conforming_fixture_yields_a_single_pass_naming_counts_by_extension()
    {
        var verdicts = Evaluate("pass");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Null(verdict.Reason);
        Assert.Contains("2 .yml", verdict.Evidence);
        Assert.Contains("1 .yaml", verdict.Evidence);
        Assert.Contains("1 .xml", verdict.Evidence);
        Assert.Contains("Parsed 4 file(s)", verdict.Evidence);
    }

    [Fact]
    public void Malformed_yaml_refuses_with_the_parsers_line_and_column_and_no_other_verdict()
    {
        var verdicts = Evaluate("refuse-bad-yaml");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Equal("broken.yml", verdict.Artifact);
        Assert.NotNull(verdict.Reason);

        // The parser's own message and mark, unmodified: this is the exact
        // text YamlDotNet reports for this fixture's content.
        Assert.Contains("did not find expected ',' or ']'", verdict.Reason);
        Assert.Contains("Line: 2, Col: 8", verdict.Reason);
    }

    [Fact]
    public void Malformed_xml_refuses_with_the_parsers_line_and_position_and_no_other_verdict()
    {
        var verdicts = Evaluate("refuse-bad-xml");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Equal("broken.xml", verdict.Artifact);
        Assert.NotNull(verdict.Reason);

        // XmlException's own message, unmodified: it already carries the line
        // and position it detected.
        Assert.Contains("does not match the end tag", verdict.Reason);
        Assert.Contains("Line 1, position 12", verdict.Reason);
    }

    [Fact]
    public void Zero_candidate_files_is_a_pass_not_a_refusal()
    {
        var solutionRoot = CreateEmptyTempDirectory();
        try
        {
            var context = Context(solutionRoot, solutionRoot);
            var verdicts = Gate.Evaluate(context).ToList();

            var verdict = Assert.Single(verdicts);
            Assert.Equal(GateOutcome.Pass, verdict.Outcome);
            Assert.Null(verdict.Reason);
            Assert.Contains("0 candidate file(s)", verdict.Evidence);
        }
        finally
        {
            Directory.Delete(solutionRoot, recursive: true);
        }
    }

    [Fact]
    public void Gate_identity_matches_the_frozen_ruling()
    {
        Assert.Equal("G1", Gate.Id);
        Assert.Equal("well-formedness", Gate.Name);
        Assert.False(Gate.RequiresTenant);
    }

    [Fact]
    public void No_verdict_across_any_fixture_contains_an_absolute_filesystem_path()
    {
        string[] fixtures = ["pass", "refuse-bad-yaml", "refuse-bad-xml"];

        foreach (var fixture in fixtures)
        {
            var root = FixtureRoot(fixture);

            foreach (var verdict in Evaluate(fixture))
            {
                Assert.False(Path.IsPathRooted(verdict.Artifact),
                    $"[{fixture}] Artifact must be repo relative: {verdict.Artifact}");
                Assert.DoesNotContain(root, verdict.Evidence);
                Assert.DoesNotContain(root, verdict.Reason ?? string.Empty);
            }
        }
    }

    // Helpers.

    private static List<GateVerdict> Evaluate(string fixtureName)
    {
        var root = FixtureRoot(fixtureName);
        var context = Context(root, root);

        var verdicts = Gate.Evaluate(context).ToList();

        // Every verdict must satisfy the contract's own invariants; a gate
        // test that never calls Validate could pass while shipping an
        // unrecordable verdict.
        foreach (var verdict in verdicts)
            verdict.Validate();

        Assert.All(verdicts, v => Assert.Equal(FixedNow, v.At));
        Assert.All(verdicts, v => Assert.Equal("G1", v.GateId));

        return verdicts;
    }

    private static GateContext Context(string repositoryRoot, string solutionRoot) => new(
        RepositoryRoot: repositoryRoot,
        SolutionRoot: solutionRoot,
        Stage: GateStage.Generation,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    private static string CreateEmptyTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dverse-g1-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Locates harness/fixtures/g1/&lt;fixtureName&gt; by walking up from the
    /// test assembly's own directory, so the tests work from any machine and
    /// any build configuration without a hardcoded path or a csproj
    /// content-copy item.
    /// </summary>
    private static string FixtureRoot(string fixtureName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures", "g1")))
            dir = dir.Parent;

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                "Could not locate fixtures/g1 above the test assembly at " + AppContext.BaseDirectory);
        }

        var fixturePath = Path.Combine(dir.FullName, "fixtures", "g1", fixtureName);

        if (!Directory.Exists(fixturePath))
            throw new DirectoryNotFoundException($"Fixture not found: {fixturePath}");

        return fixturePath;
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
