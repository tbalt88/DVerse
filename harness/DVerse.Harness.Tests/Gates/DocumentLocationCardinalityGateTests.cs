using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G4 is the flagship gate, so its red cases matter more than its green one.
/// Each refusal below corresponds to a configuration Dataverse accepts without
/// complaint and then silently fails to honour.
/// </summary>
public sealed class DocumentLocationCardinalityGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Walks up from the test assembly to harness/fixtures/g4. Avoids copying
    /// fixtures to the output directory, which would mean editing the shared
    /// csproj that parallel slices are forbidden from touching.
    /// <para>
    /// Deliberately the g4 folder itself, one level above any individual
    /// fixture directory, so RepositoryRoot and SolutionRoot are never equal
    /// in these tests. A gate that quietly relativized against SolutionRoot
    /// instead of RepositoryRoot would still pass every earlier assertion if
    /// the two roots happened to coincide; keeping them distinct is what
    /// actually pins the path base.
    /// </para>
    /// </summary>
    private static string FixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures", "g4")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException(
                $"Could not locate 'fixtures/g4' walking up from {AppContext.BaseDirectory}.");

        return Path.Combine(dir.FullName, "fixtures", "g4");
    }

    private static string Fixture(string name) => Path.Combine(FixturesRoot(), name);

    private static GateContext Context(string fixture) => new(
        RepositoryRoot: FixturesRoot(),
        SolutionRoot: Fixture(fixture),
        Stage: GateStage.Generation,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    private static IReadOnlyList<GateVerdict> Run(string fixture) =>
        new DocumentLocationCardinalityGate().Evaluate(Context(fixture)).ToList();

    [Fact]
    public void Correct_one_to_many_passes_and_ignores_unrelated_relationships()
    {
        var verdict = Assert.Single(Run("pass"));

        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Equal("G4", verdict.GateId);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Evidence));

        // Two files inspected, only one touches a document table.
        Assert.Contains("2 relationship file", verdict.Evidence);
        Assert.Contains("1 touch", verdict.Evidence);

        // Artifact is relative to RepositoryRoot (fixtures/g4), not SolutionRoot
        // (fixtures/g4/pass), so it must be prefixed with the fixture name.
        Assert.Equal("pass/entityrelationships", verdict.Artifact);
    }

    [Fact]
    public void Inverted_cardinality_is_refused_and_the_reason_names_the_silent_symptom()
    {
        var verdict = Assert.Single(Run("refuse-inverted"));

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("inverted", verdict.Reason, StringComparison.OrdinalIgnoreCase);

        // The whole value of this gate is naming a symptom the developer would
        // otherwise chase for hours with no error to guide them.
        Assert.Contains("silently empty", verdict.Reason, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "refuse-inverted/entityrelationships/dv_matter_documents.yml",
            verdict.Artifact);
    }

    [Fact]
    public void Many_to_many_is_refused()
    {
        var verdict = Assert.Single(Run("refuse-many-to-many"));

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("Many-to-many", verdict.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only 1:N", verdict.Reason);

        Assert.Equal(
            "refuse-many-to-many/entityrelationships/dv_matter_documents.yml",
            verdict.Artifact);
    }

    [Fact]
    public void SharePointSite_is_covered_by_the_same_rule()
    {
        var verdict = Assert.Single(Run("refuse-site-inverted"));

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("sharepointsite", verdict.Evidence, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "refuse-site-inverted/entityrelationships/dv_matter_sites.yml",
            verdict.Artifact);
    }

    [Fact]
    public void Two_violations_yield_two_verdicts_so_neither_hides_behind_the_other()
    {
        var verdicts = Run("refuse-two-violations");

        Assert.Equal(2, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));
        Assert.Contains(verdicts, v => v.Evidence.Contains("dv_matter_docs"));
        Assert.Contains(verdicts, v => v.Evidence.Contains("dv_client_docs"));

        Assert.Contains(verdicts, v =>
            v.Artifact == "refuse-two-violations/entityrelationships/a_matter.yml");
        Assert.Contains(verdicts, v =>
            v.Artifact == "refuse-two-violations/entityrelationships/b_client.yml");
    }

    [Fact]
    public void Absent_relationships_folder_passes_vacuously_with_evidence_saying_why()
    {
        var verdict = Assert.Single(Run("no-relationships"));

        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("No entityrelationships/ directory", verdict.Evidence);
        Assert.Equal("no-relationships/entityrelationships", verdict.Artifact);
    }

    [Fact]
    public void Malformed_relationship_file_fails_closed_through_the_runner()
    {
        // The gate throws. On its own that would be an unhandled crash, so the
        // guarantee under test is the runner's: a gate defect becomes a Refuse,
        // never a Pass, and it reaches the ledger.
        var ledger = new CapturingLedger();
        var result = new GateRunner(ledger, new FakeTime(FixedNow))
            .Run([new DocumentLocationCardinalityGate()], Context("malformed"));

        Assert.False(result.Passed);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("Gate defect", refusal.Reason);
        Assert.Single(ledger.Recorded);
    }

    [Fact]
    public void No_verdict_leaks_an_absolute_path()
    {
        foreach (var fixture in new[] { "pass", "refuse-inverted", "refuse-two-violations" })
        {
            foreach (var verdict in Run(fixture))
            {
                Assert.False(Path.IsPathRooted(verdict.Artifact),
                    $"{fixture}: '{verdict.Artifact}' is absolute and would leak a local path into a public repo.");
                Assert.DoesNotContain(":\\", verdict.Artifact);
            }
        }
    }

    private sealed class CapturingLedger : IRefusalLedger
    {
        public List<GateVerdict> Recorded { get; } = [];
        public void Record(GateVerdict v) { v.Validate(); Recorded.Add(v); }
        public IReadOnlyList<GateVerdict> Read() => Recorded;
    }
}
