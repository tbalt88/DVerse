using DVerse.Harness;

namespace DVerse.Harness.Tests;

/// <summary>
/// The ledger's job is to make a dishonest record impossible. These tests are
/// the red cases it refuses.
/// </summary>
public sealed class RefusalLedgerTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "dverse-ledger-tests", Guid.NewGuid().ToString("N"));

    private string LedgerPath => Path.Combine(_dir, "gates.jsonl");

    private static GateVerdict Pass(string evidence = "Checked 3 relationships.") => new()
    {
        GateId = "G4",
        GateName = "document-location-cardinality",
        Outcome = GateOutcome.Pass,
        Artifact = "demo-solution/Entities/dv_matter/Entity.xml",
        Evidence = evidence,
        At = DateTimeOffset.Parse("2026-07-27T18:00:00-07:00"),
        Stage = GateStage.Generation
    };

    [Fact]
    public void Pass_without_evidence_is_refused_before_it_reaches_disk()
    {
        var ledger = new JsonlRefusalLedger(LedgerPath);
        var verdict = Pass(evidence: "   ");

        var ex = Assert.Throws<InvalidVerdictException>(() => ledger.Record(verdict));

        Assert.Contains("Evidence is required", ex.Message);
        Assert.False(File.Exists(LedgerPath),
            "A rejected verdict must not create or touch the ledger file.");
    }

    [Fact]
    public void Refusal_without_a_reason_is_itself_refused()
    {
        var ledger = new JsonlRefusalLedger(LedgerPath);
        var verdict = Pass() with { Outcome = GateOutcome.Refuse, Reason = null };

        var ex = Assert.Throws<InvalidVerdictException>(() => ledger.Record(verdict));

        Assert.Contains("Reason is required", ex.Message);
        Assert.False(File.Exists(LedgerPath));
    }

    [Fact]
    public void Skip_without_a_reason_is_refused_because_a_silent_skip_reads_as_a_pass()
    {
        var ledger = new JsonlRefusalLedger(LedgerPath);
        var verdict = Pass() with { Outcome = GateOutcome.Skip, Reason = null };

        Assert.Throws<InvalidVerdictException>(() => ledger.Record(verdict));
        Assert.False(File.Exists(LedgerPath));
    }

    [Fact]
    public void Recorded_verdicts_round_trip_in_write_order()
    {
        var ledger = new JsonlRefusalLedger(LedgerPath);

        ledger.Record(Pass());
        ledger.Record(Pass() with
        {
            GateId = "G3",
            GateName = "dependency-integrity",
            Outcome = GateOutcome.Refuse,
            Reason = "Dangling reference to dv_unknown.",
            Stage = GateStage.Integration
        });

        var read = ledger.Read();

        Assert.Equal(2, read.Count);
        Assert.Equal("G4", read[0].GateId);
        Assert.Equal(GateOutcome.Pass, read[0].Outcome);
        Assert.Equal("G3", read[1].GateId);
        Assert.Equal(GateOutcome.Refuse, read[1].Outcome);
        Assert.Equal(GateStage.Integration, read[1].Stage);
        Assert.Equal("Dangling reference to dv_unknown.", read[1].Reason);
    }

    [Fact]
    public void Ledger_is_append_only_across_separate_instances()
    {
        new JsonlRefusalLedger(LedgerPath).Record(Pass());
        new JsonlRefusalLedger(LedgerPath).Record(Pass() with { GateId = "G5" });

        var read = new JsonlRefusalLedger(LedgerPath).Read();

        Assert.Equal(2, read.Count);
        Assert.Equal("G4", read[0].GateId);
        Assert.Equal("G5", read[1].GateId);
    }

    [Fact]
    public void One_verdict_occupies_exactly_one_line()
    {
        var ledger = new JsonlRefusalLedger(LedgerPath);

        // Multi-line text in a field would split one record across two ledger
        // lines and silently corrupt every subsequent read.
        ledger.Record(Pass(evidence: "line one\nline two\rline three"));

        var lines = File.ReadAllLines(LedgerPath).Where(l => !string.IsNullOrWhiteSpace(l));
        Assert.Single(lines);
        Assert.Equal("line one\nline two\rline three", ledger.Read()[0].Evidence);
    }

    [Fact]
    public void Reading_an_absent_ledger_yields_empty_rather_than_throwing()
    {
        Assert.Empty(new JsonlRefusalLedger(LedgerPath).Read());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
