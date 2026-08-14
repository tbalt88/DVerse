using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests;

/// <summary>
/// Architect validation of wave 1.
/// <para>
/// Each slice's own unit tests prove its gate works in isolation. These do not.
/// These run every gate through the real <see cref="GateRunner"/> against a real
/// on-disk <see cref="JsonlRefusalLedger"/>, and assert the whole chain behaves:
/// gate refuses, runner records, ledger persists, exit code goes non zero.
/// </para>
/// <para>
/// The reason this exists separately: a passing unit suite proves each gate
/// returns the verdict its author expected. It does not prove the gate refuses a
/// genuinely malformed solution, nor that the refusal survives to the ledger a
/// reviewer would actually read. A checked box is a claim; this is the evidence.
/// </para>
/// </summary>
public sealed class WaveOneIntegrationTests : IDisposable
{
    private readonly string _ledgerDir = Path.Combine(
        Path.GetTempPath(), "dverse-wave1-validation", Guid.NewGuid().ToString("N"));

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

    private static IGate GateFor(string id) => id switch
    {
        "g1" => new WellFormednessGate(),
        "g2" => new PublisherPrefixGate(),
        "g3" => new DependencyIntegrityGate(),
        "g6" => new BuildAndTestsGate(),
        "g8" => new RootComponentSourceGate(),
        "g4" => new DocumentLocationCardinalityGate(),
        "g9" => new SolutionComponentPathGate(),
        "g10" => new YamlLayoutGate(),
        "g11" => new CanvasYamlGate(),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown gate fixture family.")
    };

    /// <summary>
    /// Every directory named refuse-* under every gate's fixture family, discovered
    /// rather than listed. A slice that adds a red fixture is covered automatically;
    /// a slice that quietly deletes one shows up as a drop in the discovered count.
    /// </summary>
    public static TheoryData<string, string> RedFixtures()
    {
        var data = new TheoryData<string, string>();

        foreach (var family in Directory.GetDirectories(FixtureRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var gateId = Path.GetFileName(family);

            foreach (var fixture in Directory
                         .GetDirectories(family, "refuse-*")
                         .OrderBy(d => d, StringComparer.Ordinal))
            {
                data.Add(gateId, Path.GetFileName(fixture));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RedFixtures))]
    public void Every_red_fixture_is_actually_refused_and_the_refusal_reaches_the_ledger(
        string gateId, string fixtureName)
    {
        var solutionRoot = Path.Combine(FixtureRoot, gateId, fixtureName);
        var ledgerPath = Path.Combine(_ledgerDir, $"{gateId}-{fixtureName}.jsonl");
        var ledger = new JsonlRefusalLedger(ledgerPath);

        var context = new GateContext(
            RepositoryRoot: solutionRoot,
            SolutionRoot: solutionRoot,
            Stage: GateStage.Integration,
            HasTenantCredentials: false);

        var result = new GateRunner(ledger).Run([GateFor(gateId)], context);

        Assert.False(result.Passed,
            $"{gateId}/{fixtureName} is named as a red fixture but the gate let it through. "
            + "A gate with no red case is an assertion, not a gate.");

        Assert.NotEmpty(result.Refusals);
        Assert.Equal(1, result.ExitCode);

        // The refusal must survive to disk, not merely exist in memory.
        var persisted = new JsonlRefusalLedger(ledgerPath).Read();
        Assert.Contains(persisted, v => v.Outcome == GateOutcome.Refuse);

        // Every refusal must explain itself. An unexplained refusal is
        // indistinguishable from a defect to whoever reads the ledger.
        Assert.All(
            persisted.Where(v => v.Outcome == GateOutcome.Refuse),
            v => Assert.False(string.IsNullOrWhiteSpace(v.Reason)));
    }

    [Theory]
    [InlineData("g1", "pass")]
    [InlineData("g2", "pass")]
    [InlineData("g3", "pass")]
    [InlineData("g6", "pass")]
    [InlineData("g8", "pass")]
    [InlineData("g4", "pass")]
    [InlineData("g9", "pass")]
    [InlineData("g10", "pass")]
    [InlineData("g10", "pass-multi")]
    [InlineData("g4", "no-relationships")]
    [InlineData("g11", "pass")]
    [InlineData("g11", "pass-no-canvas")]
    public void Every_green_fixture_passes_with_real_evidence(string gateId, string fixtureName)
    {
        var solutionRoot = Path.Combine(FixtureRoot, gateId, fixtureName);
        var ledger = new JsonlRefusalLedger(
            Path.Combine(_ledgerDir, $"{gateId}-{fixtureName}.jsonl"));

        var context = new GateContext(solutionRoot, solutionRoot, GateStage.Integration, false);
        var result = new GateRunner(ledger).Run([GateFor(gateId)], context);

        Assert.True(result.Passed,
            $"{gateId}/{fixtureName} should pass. Refusals: "
            + string.Join(" | ", result.Refusals.Select(r => r.Reason)));

        Assert.Equal(0, result.ExitCode);
        Assert.All(result.Verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));
    }

    [Fact]
    public void No_verdict_from_any_gate_leaks_an_absolute_path_into_the_ledger()
    {
        // These land in a public repo. A leaked C:\Users\dmdom path is both a
        // reproducibility failure and a small privacy one.
        var ledgerPath = Path.Combine(_ledgerDir, "leak-scan.jsonl");
        var ledger = new JsonlRefusalLedger(ledgerPath);

        foreach (var family in Directory.GetDirectories(FixtureRoot))
        {
            var gateId = Path.GetFileName(family);

            // Slice 7.1i added harness/fixtures/diff/, a fixture family for the
            // element-identity matching layer (loop/DVerse.Harness/Diff, no gate
            // involved at all). GateFor only knows gate ids; skip any top-level
            // fixtures/ family it does not recognise rather than assuming every
            // one of them is a gate's own fixture tree.
            IGate gate;
            try
            {
                gate = GateFor(gateId);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            foreach (var fixture in Directory.GetDirectories(family))
            {
                var root = fixture;
                var context = new GateContext(root, root, GateStage.Integration, false);
                new GateRunner(ledger).Run([gate], context);
            }
        }

        var verdicts = new JsonlRefusalLedger(ledgerPath).Read();
        Assert.NotEmpty(verdicts);

        Assert.All(verdicts, v =>
        {
            Assert.False(Path.IsPathRooted(v.Artifact),
                $"{v.GateId} emitted absolute Artifact '{v.Artifact}'.");
            Assert.DoesNotContain("C:\\", v.Artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\", v.Evidence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\", v.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void All_four_gates_run_together_over_one_solution_into_one_shared_ledger()
    {
        // D5: both layers, one ledger. Four gates, one file, ordered append.
        var root = Path.Combine(FixtureRoot, "g4", "pass");
        var ledgerPath = Path.Combine(_ledgerDir, "combined.jsonl");
        var ledger = new JsonlRefusalLedger(ledgerPath);

        IGate[] gates =
        [
            new PublisherPrefixGate(),
            new DocumentLocationCardinalityGate(),
            new SolutionComponentPathGate(),
            new YamlLayoutGate()
        ];

        var context = new GateContext(root, root, GateStage.Generation, false);
        var result = new GateRunner(ledger).Run(gates, context);

        // g4/pass has relationships but no solutions/ or publishers/ tree, so the
        // structural gates correctly refuse. What is under test is that all four
        // ran and all their verdicts landed in one ledger.
        var persisted = new JsonlRefusalLedger(ledgerPath).Read();
        Assert.Equal(result.Verdicts.Count, persisted.Count);

        var gateIds = persisted.Select(v => v.GateId).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(new[] { "G10", "G2", "G4", "G9" }, gateIds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_ledgerDir))
            Directory.Delete(_ledgerDir, recursive: true);
    }
}
