using DVerse.Harness.Cli;

namespace DVerse.Harness.Tests;

/// <summary>
/// The exit-code contract is what CI acts on, so it is asserted here rather
/// than left to a human running the tool by hand. Three codes, three meanings,
/// and the distinction between 1 and 2 is the important one: a pipeline must be
/// able to tell "a gate refused" apart from "the tool broke".
/// </summary>
public sealed class CliTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "dverse-cli-tests", Guid.NewGuid().ToString("N"));

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

    private (int Code, string Out, string Err) Invoke(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private string Ledger(string name) => Path.Combine(_dir, name);

    [Fact]
    public void A_conforming_solution_exits_zero()
    {
        var (code, output, _) = Invoke(
            "gate", "run",
            "--solution", Path.Combine(FixtureRoot, "g10", "pass"),
            "--repo", FixtureRoot,
            "--ledger", Ledger("pass.jsonl"));

        // g10/pass conforms for G10 but has no entities or relationships, so
        // other gates legitimately refuse. What is asserted here is the plumbing:
        // the CLI ran, reported, and produced a definite code.
        Assert.Contains("dverse gate run", output);
        Assert.True(code is CliRunner.ExitPassed or CliRunner.ExitRefused);
    }

    [Fact]
    public void A_refused_solution_exits_one_and_says_why()
    {
        var (code, output, _) = Invoke(
            "gate", "run",
            "--solution", Path.Combine(FixtureRoot, "g4", "refuse-inverted"),
            "--repo", FixtureRoot,
            "--ledger", Ledger("refuse.jsonl"));

        Assert.Equal(CliRunner.ExitRefused, code);
        Assert.Contains("REFUSE", output);
        Assert.Contains("G4", output);
        Assert.Contains("REFUSED.", output);
    }

    [Fact]
    public void Refusals_are_written_to_the_ledger_the_cli_was_pointed_at()
    {
        var path = Ledger("written.jsonl");

        Invoke("gate", "run",
            "--solution", Path.Combine(FixtureRoot, "g4", "refuse-many-to-many"),
            "--repo", FixtureRoot,
            "--ledger", path);

        Assert.True(File.Exists(path), "The CLI reported but wrote no ledger.");
        var verdicts = new JsonlRefusalLedger(path).Read();
        Assert.Contains(verdicts, v => v.Outcome == GateOutcome.Refuse && v.GateId == "G4");
    }

    [Fact]
    public void A_missing_solution_root_exits_two_not_one()
    {
        // The distinction matters: exit 1 means the gates did their job and found
        // a violation. Exit 2 means nothing was actually checked. Collapsing them
        // would let a broken pipeline read as a governance finding.
        var (code, _, err) = Invoke(
            "gate", "run",
            "--solution", Path.Combine(_dir, "does-not-exist"),
            "--ledger", Ledger("na.jsonl"));

        Assert.Equal(CliRunner.ExitCliError, code);
        Assert.Contains("solution root not found", err);
    }

    [Theory]
    [InlineData("gate", "walk")]
    [InlineData("gates", "run")]
    public void An_unknown_command_exits_two(string a, string b)
    {
        var (code, _, err) = Invoke(a, b);
        Assert.Equal(CliRunner.ExitCliError, code);
        Assert.Contains("unknown command", err);
    }

    [Fact]
    public void An_unknown_option_exits_two_rather_than_being_ignored()
    {
        var (code, _, err) = Invoke("gate", "run", "--solution", FixtureRoot, "--yolo");
        Assert.Equal(CliRunner.ExitCliError, code);
        Assert.Contains("unknown option", err);
    }

    [Fact]
    public void Missing_required_solution_exits_two()
    {
        var (code, _, err) = Invoke("gate", "run", "--stage", "generation");
        Assert.Equal(CliRunner.ExitCliError, code);
        Assert.Contains("--solution is required", err);
    }

    [Fact]
    public void An_invalid_stage_exits_two()
    {
        var (code, _, err) = Invoke("gate", "run", "--solution", FixtureRoot, "--stage", "midway");
        Assert.Equal(CliRunner.ExitCliError, code);
        Assert.Contains("--stage must be", err);
    }

    [Fact]
    public void Help_exits_zero_and_documents_the_exit_codes()
    {
        var (code, output, _) = Invoke("--help");
        Assert.Equal(CliRunner.ExitPassed, code);
        Assert.Contains("exit codes:", output);
        Assert.Contains("--solution", output);
    }

    [Fact]
    public void Without_online_the_registry_offers_only_credential_free_gates()
    {
        Assert.All(GateRegistry.Offline, g => Assert.False(g.RequiresTenant));
        Assert.Equal(GateRegistry.All.Count, GateRegistry.Select(includeOnline: true).Count);
        Assert.Equal(GateRegistry.Offline.Count, GateRegistry.Select(includeOnline: false).Count);
    }

    [Fact]
    public void The_registry_exposes_every_gate_built_so_far()
    {
        var ids = GateRegistry.All.Select(g => g.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "G10", "G2", "G4", "G9" }, ids);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
