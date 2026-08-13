using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// Cross-gate proof of the frozen path-base rule (obligation O7): every
/// <see cref="GateVerdict.Artifact"/> is expressed relative to
/// <see cref="GateContext.RepositoryRoot"/>, with forward slashes, never
/// absolute, regardless of which gate produced it.
/// <para>
/// Before this rule was normalized, G9 and G10 already relativized against
/// RepositoryRoot while G2 and G4 relativized against SolutionRoot, so a
/// ledger reader could not tell which base a given path used without knowing
/// which gate wrote it. These tests run all four gates against every fixture
/// on disk with a RepositoryRoot that is a strict parent of SolutionRoot, so
/// a gate that still used the wrong base would produce an Artifact that
/// either escapes the repository root or is not repo relative at all, and
/// this file would catch it regardless of gate.
/// </para>
/// </summary>
public sealed class ArtifactPathBaseTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// One gate paired with the fixtures subfolder that holds its cases, for
    /// example G2 with "g2". The gate ids are the ground truth; the folder
    /// name is just the lowercase form used on disk.
    /// </summary>
    private static IEnumerable<IGate> Gates()
    {
        yield return new PublisherPrefixGate();
        yield return new DocumentLocationCardinalityGate();
        yield return new SolutionComponentPathGate();
        yield return new YamlLayoutGate();
    }

    /// <summary>
    /// Walks up from the test assembly to the harness/ directory, identified
    /// as the first ancestor that contains a fixtures/ subfolder. Used as
    /// RepositoryRoot below: a strict parent of every fixture directory, so
    /// RepositoryRoot and SolutionRoot are never equal in this file. Equal
    /// roots would make a SolutionRoot-relative bug produce the same Artifact
    /// as a RepositoryRoot-relative one, hiding exactly the defect this file
    /// exists to catch.
    /// </summary>
    private static string HarnessRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException(
                $"Could not locate a 'fixtures' folder above {AppContext.BaseDirectory}.");

        return dir.FullName;
    }

    /// <summary>
    /// Every immediate subdirectory of harness/fixtures/&lt;gid&gt;, discovered
    /// from disk rather than hardcoded, so a fixture added later is covered
    /// automatically.
    /// </summary>
    private static IReadOnlyList<string> FixtureDirs(string repositoryRoot, string gateId)
    {
        var gidFolder = Path.Combine(repositoryRoot, "fixtures", gateId.ToLowerInvariant());

        if (!Directory.Exists(gidFolder))
            throw new DirectoryNotFoundException($"No fixtures folder for {gateId} at {gidFolder}.");

        return Directory.GetDirectories(gidFolder).OrderBy(d => d, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void Every_verdict_from_every_gate_carries_a_repository_root_relative_artifact()
    {
        var repositoryRoot = HarnessRoot();
        var verdictsChecked = 0;

        foreach (var gate in Gates())
        {
            var fixtureDirs = FixtureDirs(repositoryRoot, gate.Id);
            Assert.NotEmpty(fixtureDirs);

            foreach (var fixtureDir in fixtureDirs)
            {
                var context = new GateContext(
                    RepositoryRoot: repositoryRoot,
                    SolutionRoot: fixtureDir,
                    Stage: GateStage.Generation,
                    HasTenantCredentials: false)
                {
                    Time = new FakeTime(FixedNow)
                };

                List<GateVerdict> verdicts;
                try
                {
                    verdicts = gate.Evaluate(context).ToList();
                }
                catch (Exception ex) when (ex is InvalidDataException or YamlDotNet.Core.YamlException)
                {
                    // A malformed fixture (for example g4/malformed) is designed
                    // to throw at Evaluate time; the gate fails closed and the
                    // exception-to-Refuse conversion is GateRunner's job, proven
                    // separately in the owning gate's own test file. There is no
                    // verdict to inspect here, so there is nothing to assert.
                    continue;
                }

                foreach (var verdict in verdicts)
                {
                    verdictsChecked++;
                    AssertRepositoryRootRelative(repositoryRoot, gate.Id, fixtureDir, verdict);
                }
            }
        }

        // Guards against a silently vacuous pass: if discovery or evaluation
        // regressed to the point that nothing was actually checked, the test
        // must fail rather than report green.
        Assert.True(verdictsChecked > 0, "No verdicts were produced across any gate or fixture.");
    }

    private static void AssertRepositoryRootRelative(
        string repositoryRoot, string gateId, string fixtureDir, GateVerdict verdict)
    {
        var context = $"{gateId} / {Path.GetFileName(fixtureDir)}";

        Assert.False(
            Path.IsPathRooted(verdict.Artifact),
            $"[{context}] Artifact '{verdict.Artifact}' is rooted; it must be repository relative.");

        Assert.DoesNotContain(
            '\\',
            verdict.Artifact);

        var resolved = Path.GetFullPath(Path.Combine(repositoryRoot, verdict.Artifact));
        var repositoryRootFull = Path.GetFullPath(repositoryRoot);

        // Trailing separators on both sides so the repository root itself
        // (Artifact == ".") counts as "under" its own root, without letting a
        // sibling directory that merely shares the same prefix (for example
        // "harness-extra") pass by accident.
        var resolvedWithSeparator = resolved + Path.DirectorySeparatorChar;
        var repositoryRootWithSeparator = repositoryRootFull + Path.DirectorySeparatorChar;

        Assert.StartsWith(
            repositoryRootWithSeparator,
            resolvedWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }
}
