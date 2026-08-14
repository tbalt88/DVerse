using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G3 exists because a declared missing dependency is not a warning; it is a
/// guaranteed import failure on any environment that lacks the dependency,
/// and pac only reports that at import time. These tests prove the gate
/// refuses on the pac-verified non-empty shape (decompiled from pac 2.10.1's
/// SolutionPackagerLib.dll, see the gate's WHY comment), passes on the
/// pac-verified empty shape, and fails closed (Refuse, never a crash or a
/// silent Pass) on every other file state: absent, malformed, and
/// unrecognised-but-non-empty.
/// </summary>
public sealed class DependencyIntegrityGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private static readonly DependencyIntegrityGate Gate = new();

    [Fact]
    public void Empty_mapping_fixture_yields_a_single_pass_with_non_empty_evidence()
    {
        var verdicts = Evaluate("pass");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Declared_dependency_yields_refuse_naming_the_dependency()
    {
        var verdicts = Evaluate("refuse-declared-dependency");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("External Entity", verdict.Reason);
        Assert.Contains("fails at import", verdict.Reason);
    }

    [Fact]
    public void Absent_missingdependencies_file_yields_refuse_not_a_crash_or_a_pass()
    {
        var verdicts = Evaluate("refuse-missing-file");

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("missingdependencies.yml", verdict.Reason);
    }

    [Fact]
    public void Malformed_yaml_yields_refuse_naming_the_parse_failure()
    {
        var root = CreateTempFixture("MissingDependencies: [this is not: valid: yaml");
        try
        {
            var verdicts = EvaluateAt(root);

            var verdict = Assert.Single(verdicts);
            Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
            Assert.NotNull(verdict.Reason);
            Assert.Contains("could not be parsed as YAML", verdict.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Two_declared_dependencies_yield_two_individually_visible_refuse_verdicts()
    {
        var yaml = """
            MissingDependencies:
              MissingDependency:
                - Required:
                    '@schemaName': 'new_first'
                    '@displayName': 'First Dependency'
                  Dependent:
                    '@schemaName': 'dv_matter'
                - Required:
                    '@schemaName': 'new_second'
                    '@displayName': 'Second Dependency'
                  Dependent:
                    '@schemaName': 'dv_client'
            """;

        var root = CreateTempFixture(yaml);
        try
        {
            var verdicts = EvaluateAt(root);

            Assert.Equal(2, verdicts.Count);
            Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));

            var reasons = verdicts.Select(v => v.Reason).ToList();
            Assert.Contains(reasons, r => r!.Contains("First Dependency"));
            Assert.Contains(reasons, r => r!.Contains("Second Dependency"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Non_empty_mapping_of_unrecognised_shape_still_refuses()
    {
        // Per the frozen ruling: if a future pac version's shape does not
        // match what was decompiled, presence alone under MissingDependencies
        // is still the finding. This proves the fallback, not the primary
        // shape (covered by refuse-declared-dependency).
        var yaml = """
            MissingDependencies:
              SomeUnexpectedChild:
                '@foo': 'bar'
            """;

        var root = CreateTempFixture(yaml);
        try
        {
            var verdicts = EvaluateAt(root);

            var verdict = Assert.Single(verdicts);
            Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
            Assert.NotNull(verdict.Reason);
            Assert.Contains("regardless of shape", verdict.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Every_verdict_satisfies_the_ledger_contract_and_carries_gate_identity()
    {
        foreach (var fixture in new[] { "pass", "refuse-declared-dependency", "refuse-missing-file" })
        {
            foreach (var verdict in Evaluate(fixture))
            {
                verdict.Validate();
                Assert.Equal("G3", verdict.GateId);
                Assert.Equal("dependency-integrity", verdict.GateName);
                Assert.Equal(FixedNow, verdict.At);
            }
        }
    }

    [Fact]
    public void No_verdict_across_any_fixture_contains_an_absolute_filesystem_path()
    {
        string[] fixtures = ["pass", "refuse-declared-dependency", "refuse-missing-file"];

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

    [Fact]
    public void Also_requires_a_tenant_is_false_and_id_and_name_are_frozen()
    {
        Assert.False(Gate.RequiresTenant);
        Assert.Equal("G3", Gate.Id);
        Assert.Equal("dependency-integrity", Gate.Name);
    }

    // Helpers.

    private static List<GateVerdict> Evaluate(string fixtureName) => EvaluateAt(FixtureRoot(fixtureName));

    private static List<GateVerdict> EvaluateAt(string root)
    {
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
        Assert.All(verdicts, v => Assert.Equal("G3", v.GateId));

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

        var fixturePath = Path.Combine(dir.FullName, "fixtures", "g3", fixtureName);

        if (!Directory.Exists(fixturePath))
            throw new DirectoryNotFoundException($"Fixture not found: {fixturePath}");

        return fixturePath;
    }

    /// <summary>
    /// Builds a scratch fixture outside the repository for shapes the frozen
    /// ruling requires covering (malformed YAML, multi-entry, unrecognised
    /// shape) but that are not among this slice's three owned fixture
    /// directories. Deleted by the calling test, never left on disk.
    /// </summary>
    private static string CreateTempFixture(string missingDependenciesYaml)
    {
        var root = Path.Combine(Path.GetTempPath(), "dverse-g3-tests", Guid.NewGuid().ToString("N"));
        var solutionDir = Path.Combine(root, "solutions", "dversesolution");
        Directory.CreateDirectory(solutionDir);
        File.WriteAllText(Path.Combine(solutionDir, "missingdependencies.yml"), missingDependenciesYaml);
        return root;
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
