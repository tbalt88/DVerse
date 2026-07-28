using DVerse.Harness;

namespace DVerse.Harness.Tests;

/// <summary>
/// The runner's job is to make an unrecorded refusal impossible, and to fail
/// closed when it cannot tell. These are the cases that prove it.
/// </summary>
public sealed class GateRunnerTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-07-27T18:00:00-07:00");

    private static GateContext Context(bool hasTenant = false) => new(
        RepositoryRoot: "/repo",
        SolutionRoot: "demo-solution",
        Stage: GateStage.Generation,
        HasTenantCredentials: hasTenant);

    private static GateRunner Runner(IRefusalLedger ledger) =>
        new(ledger, new FakeTime(FixedNow));

    [Fact]
    public void A_refusal_is_in_the_ledger_before_the_run_reports_it()
    {
        var ledger = new InMemoryLedger();
        var result = Runner(ledger).Run([new RefusingGate()], Context());

        Assert.False(result.Passed);
        Assert.Single(result.Refusals);

        // The point of the whole type: the record exists, not merely the verdict.
        Assert.Single(ledger.Recorded);
        Assert.Equal(GateOutcome.Refuse, ledger.Recorded[0].Outcome);
    }

    [Fact]
    public void A_gate_that_throws_refuses_rather_than_passes()
    {
        var ledger = new InMemoryLedger();
        var result = Runner(ledger).Run([new ThrowingGate()], Context());

        Assert.False(result.Passed);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("Gate defect", refusal.Reason);
        Assert.Contains("InvalidOperationException", refusal.Reason);
        Assert.Single(ledger.Recorded);
    }

    [Fact]
    public void A_gate_that_throws_lazily_still_refuses()
    {
        // A lazily evaluated iterator throws on enumeration, not on call. If the
        // runner did not materialise inside its own try block, this would escape
        // the fail-closed guarantee entirely.
        var ledger = new InMemoryLedger();
        var result = Runner(ledger).Run([new LazyThrowingGate()], Context());

        Assert.False(result.Passed);
        Assert.Contains("Gate defect", Assert.Single(result.Refusals).Reason);
    }

    [Fact]
    public void An_online_gate_without_credentials_skips_with_a_reason_and_does_not_fail_the_run()
    {
        var ledger = new InMemoryLedger();
        var result = Runner(ledger).Run([new TenantGate()], Context(hasTenant: false));

        Assert.True(result.Passed, "A skip is not a failure. Fork pull requests must stay green.");
        var skip = Assert.Single(result.Skips);
        Assert.Contains("fork pull requests", skip.Reason);
        Assert.Single(ledger.Recorded);
    }

    [Fact]
    public void An_online_gate_with_credentials_actually_runs()
    {
        var ledger = new InMemoryLedger();
        var result = Runner(ledger).Run([new TenantGate()], Context(hasTenant: true));

        Assert.True(result.Passed);
        Assert.Empty(result.Skips);
        Assert.Equal("G7", Assert.Single(ledger.Recorded).GateId);
    }

    [Fact]
    public void Exit_code_is_non_zero_on_refusal_and_zero_otherwise()
    {
        var ledger = new InMemoryLedger();

        Assert.Equal(0, Runner(ledger).Run([new PassingGate()], Context()).ExitCode);
        Assert.Equal(1, Runner(ledger).Run([new RefusingGate()], Context()).ExitCode);
        Assert.Equal(0, Runner(ledger).Run([new TenantGate()], Context(false)).ExitCode);
    }

    [Theory]
    [InlineData(true)]   // gate crash path
    [InlineData(false)]  // tenant skip path
    public void Runner_authored_verdicts_never_carry_an_absolute_path(bool crashing)
    {
        // Regression guard. The runner authors verdicts of its own for crashes
        // and skips, and both originally emitted SolutionRoot raw. With an
        // absolute root that leaked a local filesystem path into a ledger bound
        // for a public repo. The original tests missed it by passing a relative
        // string as the root, so the defect could not manifest. This one uses a
        // realistic absolute root on purpose.
        var ledger = new InMemoryLedger();
        var context = new GateContext(
            RepositoryRoot: Path.Combine(Path.GetTempPath(), "repo"),
            SolutionRoot: Path.Combine(Path.GetTempPath(), "repo", "demo-solution"),
            Stage: GateStage.Integration,
            HasTenantCredentials: false);

        IGate gate = crashing ? new ThrowingGate() : new TenantGate();
        Runner(ledger).Run([gate], context);

        var verdict = Assert.Single(ledger.Recorded);
        Assert.False(Path.IsPathRooted(verdict.Artifact),
            $"Runner emitted absolute Artifact '{verdict.Artifact}'.");
        Assert.Equal("demo-solution", verdict.Artifact);
    }

    [Fact]
    public void A_ledger_failure_stops_the_run_rather_than_reporting_green()
    {
        var result = Record.Exception(() =>
            Runner(new ExplodingLedger()).Run([new PassingGate()], Context()));

        Assert.IsType<IOException>(result);
    }

    // Fakes.

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryLedger : IRefusalLedger
    {
        public List<GateVerdict> Recorded { get; } = [];

        public void Record(GateVerdict verdict)
        {
            verdict.Validate();
            Recorded.Add(verdict);
        }

        public IReadOnlyList<GateVerdict> Read() => Recorded;
    }

    private sealed class ExplodingLedger : IRefusalLedger
    {
        public void Record(GateVerdict verdict) => throw new IOException("disk full");
        public IReadOnlyList<GateVerdict> Read() => [];
    }

    private static GateVerdict Verdict(string id, GateOutcome outcome, string? reason = null) => new()
    {
        GateId = id,
        GateName = $"{id}-fake",
        Outcome = outcome,
        Artifact = "demo-solution",
        Evidence = "Fake gate, no real inspection.",
        Reason = reason,
        At = FixedNow,
        Stage = GateStage.Generation
    };

    private sealed class PassingGate : IGate
    {
        public string Id => "G1";
        public string Name => "passing-fake";
        public bool RequiresTenant => false;
        public IEnumerable<GateVerdict> Evaluate(GateContext c) => [Verdict(Id, GateOutcome.Pass)];
    }

    private sealed class RefusingGate : IGate
    {
        public string Id => "G4";
        public string Name => "refusing-fake";
        public bool RequiresTenant => false;
        public IEnumerable<GateVerdict> Evaluate(GateContext c) =>
            [Verdict(Id, GateOutcome.Refuse, "N:1 relationship to SharePointDocumentLocation.")];
    }

    private sealed class ThrowingGate : IGate
    {
        public string Id => "G3";
        public string Name => "throwing-fake";
        public bool RequiresTenant => false;
        public IEnumerable<GateVerdict> Evaluate(GateContext c) =>
            throw new InvalidOperationException("malformed FormXML");
    }

    private sealed class LazyThrowingGate : IGate
    {
        public string Id => "G5";
        public string Name => "lazy-throwing-fake";
        public bool RequiresTenant => false;

        public IEnumerable<GateVerdict> Evaluate(GateContext c)
        {
            yield return Verdict(Id, GateOutcome.Pass);
            throw new InvalidOperationException("failed midway through enumeration");
        }
    }

    private sealed class TenantGate : IGate
    {
        public string Id => "G7";
        public string Name => "power-apps-checker";
        public bool RequiresTenant => true;
        public IEnumerable<GateVerdict> Evaluate(GateContext c) => [Verdict(Id, GateOutcome.Pass)];
    }
}
