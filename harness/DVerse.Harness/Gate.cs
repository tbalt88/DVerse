namespace DVerse.Harness;

/// <summary>
/// What a gate is given to inspect.
/// </summary>
/// <param name="RepositoryRoot">Absolute path to the repository root.</param>
/// <param name="SolutionRoot">
/// Absolute path to the unpacked solution folder. Gates read declarative
/// artifacts from here.
/// </param>
/// <param name="Stage">Where this run is happening.</param>
/// <param name="HasTenantCredentials">
/// Whether a Dataverse connection is available. Online gates report
/// <see cref="GateOutcome.Skip"/> with a reason when false, which is the normal
/// and expected state on a fork pull request.
/// </param>
public sealed record GateContext(
    string RepositoryRoot,
    string SolutionRoot,
    GateStage Stage,
    bool HasTenantCredentials);

/// <summary>
/// One rule, mechanically enforced.
/// <para>
/// Implementations must be pure with respect to the filesystem: read artifacts,
/// return a verdict, write nothing. The runner owns all recording, so a gate
/// physically cannot refuse without the refusal reaching the ledger.
/// </para>
/// </summary>
public interface IGate
{
    /// <summary>Stable identifier, for example "G4".</summary>
    string Id { get; }

    /// <summary>Slug name, for example "document-location-cardinality".</summary>
    string Name { get; }

    /// <summary>
    /// True when this gate needs a live Dataverse tenant. Online gates are
    /// skipped rather than failed when credentials are absent.
    /// </summary>
    bool RequiresTenant { get; }

    /// <summary>Inspect the artifacts and conclude. Must not write to disk.</summary>
    IEnumerable<GateVerdict> Evaluate(GateContext context);
}

/// <summary>Aggregate outcome of one gate run.</summary>
/// <param name="Verdicts">Every verdict produced, already recorded in the ledger.</param>
public sealed record GateRunResult(IReadOnlyList<GateVerdict> Verdicts)
{
    /// <summary>Verdicts that refused.</summary>
    public IReadOnlyList<GateVerdict> Refusals { get; } =
        Verdicts.Where(v => v.Outcome == GateOutcome.Refuse).ToList();

    /// <summary>Verdicts that could not run.</summary>
    public IReadOnlyList<GateVerdict> Skips { get; } =
        Verdicts.Where(v => v.Outcome == GateOutcome.Skip).ToList();

    /// <summary>True when nothing refused. Skips do not make a run pass or fail.</summary>
    public bool Passed => Refusals.Count == 0;

    /// <summary>Process exit code. Non zero on any refusal, which is what makes CI red.</summary>
    public int ExitCode => Passed ? 0 : 1;
}

/// <summary>
/// Runs gates and guarantees that every verdict reaches the ledger before the
/// run reports anything.
/// <para>
/// This type exists to make one specific failure impossible: a gate refusing
/// without leaving a record. The refusal and the ledger append are the same
/// action, so an unrecorded refusal cannot occur even if a caller forgets.
/// </para>
/// </summary>
public sealed class GateRunner(IRefusalLedger ledger, TimeProvider? timeProvider = null)
{
    private readonly IRefusalLedger _ledger = ledger
        ?? throw new ArgumentNullException(nameof(ledger));

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public GateRunResult Run(IEnumerable<IGate> gates, GateContext context)
    {
        ArgumentNullException.ThrowIfNull(gates);
        ArgumentNullException.ThrowIfNull(context);

        var all = new List<GateVerdict>();

        foreach (var gate in gates)
        {
            foreach (var verdict in EvaluateSafely(gate, context))
            {
                // Record first, aggregate second. If the ledger throws, the run
                // dies here rather than continuing to a green summary that no
                // evidence supports.
                _ledger.Record(verdict);
                all.Add(verdict);
            }
        }

        return new GateRunResult(all);
    }

    /// <summary>
    /// Evaluates a gate, converting a crash into a refusal.
    /// <para>
    /// Fail closed, deliberately. A governance harness that passes when its own
    /// checker throws is worse than no harness, because it produces a green
    /// receipt for an artifact nothing inspected. An exception means we do not
    /// know whether the rule holds, and "we do not know" must never render as
    /// a pass.
    /// </para>
    /// </summary>
    private IEnumerable<GateVerdict> EvaluateSafely(IGate gate, GateContext context)
    {
        if (gate.RequiresTenant && !context.HasTenantCredentials)
        {
            return
            [
                new GateVerdict
                {
                    GateId = gate.Id,
                    GateName = gate.Name,
                    Outcome = GateOutcome.Skip,
                    Artifact = context.SolutionRoot,
                    Evidence = "Gate not executed.",
                    Reason = "Requires a Dataverse tenant; no credentials present. "
                           + "Expected on fork pull requests.",
                    At = _time.GetUtcNow(),
                    Stage = context.Stage
                }
            ];
        }

        try
        {
            // Materialise inside the try. A lazily evaluated iterator would
            // otherwise throw later, outside this handler, and escape the
            // fail-closed guarantee.
            return gate.Evaluate(context).ToList();
        }
        catch (Exception ex)
        {
            return
            [
                new GateVerdict
                {
                    GateId = gate.Id,
                    GateName = gate.Name,
                    Outcome = GateOutcome.Refuse,
                    Artifact = context.SolutionRoot,
                    Evidence = "Gate threw before reaching a verdict.",
                    Reason = $"Gate defect, refusing rather than passing: "
                           + $"{ex.GetType().Name}: {ex.Message}",
                    At = _time.GetUtcNow(),
                    Stage = context.Stage
                }
            ];
        }
    }
}
