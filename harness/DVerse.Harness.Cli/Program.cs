using DVerse.Harness;
using DVerse.Harness.Cli;
using DVerse.Harness.Gates;

// dverse gate run --solution <path> [--ledger <path>] [--repo <path>]
//                 [--stage generation|integration] [--online] [--baseline <path>]
//
// dverse diff --solution <path> --baseline <path> [--repo <path>] [--ledger <path>]
//
// Exit codes:
//   0  no gate refused
//   1  at least one gate refused; CI goes red here
//   2  the CLI itself could not run; never confused with a clean pass

return CliRunner.Run(args, Console.Out, Console.Error);

namespace DVerse.Harness.Cli
{
    /// <summary>
    /// The entry point CI invokes. Kept as a testable class rather than living
    /// only in top-level statements, so the exit-code contract can be asserted
    /// directly. An exit code verified only by a human running the tool by hand
    /// is not verified.
    /// </summary>
    public static class CliRunner
    {
        public const int ExitPassed = 0;
        public const int ExitRefused = 1;
        public const int ExitCliError = 2;

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            try
            {
                // "diff" is a distinct top-level command from "gate run", not another gate-run
                // option: it needs its own required --baseline and prints a diff-shaped report
                // rather than the ladder report, so it is dispatched before CliOptions.Parse ever
                // sees the args (CliOptions.Parse only recognises "gate run").
                if (args.Length > 0 && args[0] == "diff")
                {
                    var diffOptions = DiffOptions.Parse(args);

                    if (diffOptions.ShowHelp)
                    {
                        stdout.WriteLine(DiffOptions.Usage);
                        return ExitPassed;
                    }

                    return ExecuteDiff(diffOptions, stdout, stderr);
                }

                var options = CliOptions.Parse(args);

                if (options.ShowHelp)
                {
                    stdout.WriteLine(CliOptions.Usage);
                    return ExitPassed;
                }

                return Execute(options, stdout, stderr);
            }
            catch (CliUsageException ex)
            {
                stderr.WriteLine($"dverse: {ex.Message}");
                stderr.WriteLine();
                stderr.WriteLine(CliOptions.Usage);
                return ExitCliError;
            }
            catch (Exception ex)
            {
                // A CLI crash must never be mistaken for a clean run. Exit 2 is
                // distinct from exit 1 so a pipeline can tell "the gates refused"
                // apart from "the tool broke".
                stderr.WriteLine($"dverse: unexpected failure: {ex.GetType().Name}: {ex.Message}");
                return ExitCliError;
            }
        }

        private static int Execute(CliOptions options, TextWriter stdout, TextWriter stderr)
        {
            if (!Directory.Exists(options.SolutionRoot))
            {
                stderr.WriteLine($"dverse: solution root not found: {options.SolutionRoot}");
                return ExitCliError;
            }

            if (!IsUnderOrEqual(options.RepositoryRoot, options.SolutionRoot))
            {
                stderr.WriteLine(
                    $"dverse: solution root is not under the repository root: "
                    + $"solution={options.SolutionRoot} repo={options.RepositoryRoot}");
                return ExitCliError;
            }

            if (options.BaselineRoot is not null && !Directory.Exists(options.BaselineRoot))
            {
                stderr.WriteLine($"dverse: baseline root not found: {options.BaselineRoot}");
                return ExitCliError;
            }

            var ledger = new JsonlRefusalLedger(options.LedgerPath);
            IReadOnlyList<IGate> gates = GateRegistry.Select(options.IncludeOnline);

            if (options.BaselineRoot is not null)
            {
                // Substitute a baseline-carrying G12 for the catalogue's default (null-baseline,
                // always-SKIP) instance, for this run only. GateRegistry.All stays the single
                // source of truth for "which gates exist"; only the one instance that needs a
                // second tree gets rebuilt per invocation.
                gates = gates
                    .Select(g => g is StructuralDiffGate ? new StructuralDiffGate(options.BaselineRoot) : g)
                    .ToList();
            }

            var context = new GateContext(
                RepositoryRoot: options.RepositoryRoot,
                SolutionRoot: options.SolutionRoot,
                Stage: options.Stage,
                HasTenantCredentials: options.HasTenantCredentials);

            var result = new GateRunner(ledger).Run(gates, context);

            Report(result, gates, options, stdout);

            return result.Passed ? ExitPassed : ExitRefused;
        }

        private static void Report(
            GateRunResult result, IReadOnlyList<IGate> gates, CliOptions options, TextWriter stdout)
        {
            stdout.WriteLine(
                $"dverse gate run  stage={options.Stage.ToString().ToLowerInvariant()}  gates={gates.Count}");
            stdout.WriteLine($"ledger: {options.LedgerPath}");
            stdout.WriteLine();

            foreach (var v in result.Verdicts)
            {
                var marker = v.Outcome switch
                {
                    GateOutcome.Pass => "PASS  ",
                    GateOutcome.Refuse => "REFUSE",
                    GateOutcome.Skip => "SKIP  ",
                    _ => "?     "
                };

                stdout.WriteLine($"{marker} {v.GateId,-4} {v.GateName,-34} {v.Artifact}");

                // Reasons are the actionable half. Print them where the failure
                // happens rather than making a reader go open the ledger.
                if (v.Reason is not null)
                    stdout.WriteLine($"       {v.Reason}");
            }

            stdout.WriteLine();

            var passes = result.Verdicts.Count(v => v.Outcome == GateOutcome.Pass);
            stdout.WriteLine(
                $"{passes} passed, {result.Refusals.Count} refused, {result.Skips.Count} skipped.");

            // Not every SKIP means "needs a tenant" any more: G12 (structural-diff) skips
            // offline, honestly, whenever no --baseline was supplied (ruling 1), which has
            // nothing to do with Dataverse credentials. Only print the tenant-specific note
            // when at least one of the actual skips came from a gate that RequiresTenant, so
            // the message stays true rather than a stale blanket assumption.
            var tenantGateIds = gates.Where(g => g.RequiresTenant).Select(g => g.Id).ToHashSet();
            var hasTenantSkip = result.Skips.Any(v => tenantGateIds.Contains(v.GateId));

            if (hasTenantSkip && !options.IncludeOnline)
            {
                stdout.WriteLine(
                    "Skipped gates require a Dataverse tenant. This is expected offline "
                    + "and on fork pull requests.");
            }

            stdout.WriteLine(
                result.Passed
                    ? "No gate refused."
                    : $"REFUSED. {result.Refusals.Count} violation(s) recorded in the ledger.");
        }

        /// <summary>
        /// The "dverse diff" verb: runs G12 (<see cref="StructuralDiffGate"/>) alone, over exactly
        /// the two trees named on the command line, through the SAME <see cref="GateRunner"/> /
        /// <see cref="JsonlRefusalLedger"/> pipeline "gate run" uses. G12's refusals therefore land
        /// in the ledger exactly like any other gate's (mission deliverable 2); this verb is simply
        /// G12's natural entry point (ruling 1) rather than a second, parallel reporting path.
        /// </summary>
        private static int ExecuteDiff(DiffOptions options, TextWriter stdout, TextWriter stderr)
        {
            if (!Directory.Exists(options.SolutionRoot))
            {
                stderr.WriteLine($"dverse: solution root not found: {options.SolutionRoot}");
                return ExitCliError;
            }

            if (!Directory.Exists(options.BaselineRoot))
            {
                stderr.WriteLine($"dverse: baseline root not found: {options.BaselineRoot}");
                return ExitCliError;
            }

            if (!IsUnderOrEqual(options.RepositoryRoot, options.SolutionRoot))
            {
                stderr.WriteLine(
                    $"dverse: solution root is not under the repository root: "
                    + $"solution={options.SolutionRoot} repo={options.RepositoryRoot}");
                return ExitCliError;
            }

            var ledger = new JsonlRefusalLedger(options.LedgerPath);
            var gate = new StructuralDiffGate(options.BaselineRoot);

            var context = new GateContext(
                RepositoryRoot: options.RepositoryRoot,
                SolutionRoot: options.SolutionRoot,
                Stage: GateStage.Integration,
                HasTenantCredentials: false);

            var result = new GateRunner(ledger).Run([gate], context);

            ReportDiff(result, options, stdout);

            return result.Passed ? ExitPassed : ExitRefused;
        }

        private static void ReportDiff(GateRunResult result, DiffOptions options, TextWriter stdout)
        {
            stdout.WriteLine("dverse diff  gate=G12 structural-diff");
            stdout.WriteLine($"ledger: {options.LedgerPath}");
            stdout.WriteLine();

            foreach (var v in result.Verdicts)
            {
                var marker = v.Outcome switch
                {
                    GateOutcome.Pass => "PASS  ",
                    GateOutcome.Refuse => "REFUSE",
                    GateOutcome.Skip => "SKIP  ",
                    _ => "?     "
                };

                stdout.WriteLine($"{marker} {v.Artifact}");
                stdout.WriteLine($"       {v.Evidence}");

                if (v.Reason is not null)
                    stdout.WriteLine($"       reason: {v.Reason}");
            }

            stdout.WriteLine();

            var passes = result.Verdicts.Count(v => v.Outcome == GateOutcome.Pass);
            stdout.WriteLine(
                $"{passes} passed, {result.Refusals.Count} refused, {result.Skips.Count} skipped.");

            stdout.WriteLine(
                result.Passed
                    ? "No gate refused."
                    : $"REFUSED. {result.Refusals.Count} violation(s) recorded in the ledger.");
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is <paramref name="root"/> itself
        /// or a descendant of it. GateVerdict.Artifact is repository-root-relative
        /// by contract, and Path.GetRelativePath happily walks ".." segments to
        /// reach a path outside the root instead of failing, so this containment
        /// check is what actually enforces the contract.
        ///
        /// WHY trailing-separator normalization instead of a bare StartsWith:
        /// both paths are resolved full paths already, but "C:\repo2" starts
        /// with the string "C:\repo" even though it is a sibling directory, not
        /// a descendant. Appending a trailing separator to both sides before
        /// comparing forces the match to land on a full path-segment boundary,
        /// and it also makes candidate == root compare equal (root's own
        /// trailing-separator form is a prefix of itself), so "equal to repo
        /// root" is accepted without a separate equality check.
        /// Comparison is OrdinalIgnoreCase because Windows paths are
        /// case-insensitive.
        /// </summary>
        private static bool IsUnderOrEqual(string root, string candidate)
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            var normalizedCandidate =
                Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;

            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class CliUsageException(string message) : Exception(message);

    public sealed record CliOptions
    {
        public const string Usage = """
            usage: dverse gate run --solution <path> [options]

              --solution <path>   solution source root (required)
              --ledger <path>     ledger file (default: <repo>/loop/gates.jsonl)
              --repo <path>       repository root for relative paths (default: cwd)
              --stage <s>         generation | integration (default: integration)
              --online            include gates that require a Dataverse tenant
              --baseline <path>   baseline solution tree; activates G12 (structural-diff)
              --help

            exit codes: 0 clean, 1 a gate refused, 2 the CLI itself failed
            """;

        public required string SolutionRoot { get; init; }
        public required string RepositoryRoot { get; init; }
        public required string LedgerPath { get; init; }
        public required GateStage Stage { get; init; }
        public required bool IncludeOnline { get; init; }
        public string? BaselineRoot { get; init; }
        public bool ShowHelp { get; init; }

        /// <summary>
        /// Credentials count as present only when the caller opts in with
        /// --online. Defaulting to false means a misconfigured pipeline yields
        /// honest Skip verdicts rather than silently passing gates that never ran.
        /// </summary>
        public bool HasTenantCredentials => IncludeOnline;

        public static CliOptions Parse(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
                return Help();

            if (args.Length < 2 || args[0] != "gate" || args[1] != "run")
            {
                throw new CliUsageException(
                    $"unknown command '{string.Join(' ', args.Take(2))}'. Expected 'gate run'.");
            }

            string? solution = null, repo = null, ledger = null, stage = null, baseline = null;
            var online = false;

            for (var i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--solution": solution = Next(args, ref i); break;
                    case "--repo": repo = Next(args, ref i); break;
                    case "--ledger": ledger = Next(args, ref i); break;
                    case "--stage": stage = Next(args, ref i); break;
                    case "--online": online = true; break;
                    case "--baseline": baseline = Next(args, ref i); break;
                    default: throw new CliUsageException($"unknown option '{args[i]}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(solution))
                throw new CliUsageException("--solution is required.");

            var repoRoot = Path.GetFullPath(repo ?? Directory.GetCurrentDirectory());

            return new CliOptions
            {
                SolutionRoot = Path.GetFullPath(solution),
                RepositoryRoot = repoRoot,
                LedgerPath = ledger is not null
                    ? Path.GetFullPath(ledger)
                    : Path.Combine(repoRoot, "loop", "gates.jsonl"),
                Stage = ParseStage(stage),
                IncludeOnline = online,
                BaselineRoot = baseline is not null ? Path.GetFullPath(baseline) : null
            };
        }

        private static GateStage ParseStage(string? value) => value?.ToLowerInvariant() switch
        {
            null or "integration" => GateStage.Integration,
            "generation" => GateStage.Generation,
            _ => throw new CliUsageException(
                $"--stage must be 'generation' or 'integration', got '{value}'.")
        };

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new CliUsageException($"option '{args[i]}' expects a value.");
            return args[++i];
        }

        private static CliOptions Help() => new()
        {
            SolutionRoot = string.Empty,
            RepositoryRoot = string.Empty,
            LedgerPath = string.Empty,
            Stage = GateStage.Integration,
            IncludeOnline = false,
            ShowHelp = true
        };
    }

    /// <summary>
    /// Options for "dverse diff", G12's natural entry point (ruling 1): unlike "gate run",
    /// --baseline is required here, not optional, because the whole point of this verb is to
    /// compare two trees rather than run the standard offline ladder over one.
    /// </summary>
    public sealed record DiffOptions
    {
        public const string Usage = """
            usage: dverse diff --solution <path> --baseline <path> [options]

              --solution <path>   current solution tree (required)
              --baseline <path>   baseline solution tree to compare against (required)
              --repo <path>       repository root for relative paths (default: cwd)
              --ledger <path>     ledger file (default: <repo>/loop/gates.jsonl)
              --help

            exit codes: 0 clean, 1 the diff gate refused, 2 the CLI itself failed
            """;

        public required string SolutionRoot { get; init; }
        public required string BaselineRoot { get; init; }
        public required string RepositoryRoot { get; init; }
        public required string LedgerPath { get; init; }
        public bool ShowHelp { get; init; }

        public static DiffOptions Parse(string[] args)
        {
            string? solution = null, baseline = null, repo = null, ledger = null;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--solution": solution = Next(args, ref i); break;
                    case "--baseline": baseline = Next(args, ref i); break;
                    case "--repo": repo = Next(args, ref i); break;
                    case "--ledger": ledger = Next(args, ref i); break;
                    case "--help":
                    case "-h":
                        return Help();
                    default: throw new CliUsageException($"unknown option '{args[i]}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(solution))
                throw new CliUsageException("--solution is required.");

            if (string.IsNullOrWhiteSpace(baseline))
                throw new CliUsageException("--baseline is required.");

            var repoRoot = Path.GetFullPath(repo ?? Directory.GetCurrentDirectory());

            return new DiffOptions
            {
                SolutionRoot = Path.GetFullPath(solution),
                BaselineRoot = Path.GetFullPath(baseline),
                RepositoryRoot = repoRoot,
                LedgerPath = ledger is not null
                    ? Path.GetFullPath(ledger)
                    : Path.Combine(repoRoot, "loop", "gates.jsonl")
            };
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new CliUsageException($"option '{args[i]}' expects a value.");
            return args[++i];
        }

        private static DiffOptions Help() => new()
        {
            SolutionRoot = string.Empty,
            BaselineRoot = string.Empty,
            RepositoryRoot = string.Empty,
            LedgerPath = string.Empty,
            ShowHelp = true
        };
    }
}
