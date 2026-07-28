namespace DVerse.Harness;

/// <summary>
/// What a gate concluded about one artifact.
/// </summary>
public enum GateOutcome
{
    /// <summary>The artifact satisfies the rule.</summary>
    Pass,

    /// <summary>The artifact violates the rule. Output is refused.</summary>
    Refuse,

    /// <summary>
    /// The gate could not run. This is never a pass. Online gates report Skip
    /// when no tenant credentials are present, which is the normal state on a
    /// fork pull request.
    /// </summary>
    Skip
}

/// <summary>
/// One gate's conclusion about one artifact.
/// <para>
/// Two invariants are enforced by the constructor rather than documented and
/// hoped for, because a preventive action that depends on diligence alone will
/// decay:
/// </para>
/// <list type="number">
/// <item><see cref="Evidence"/> is mandatory on every verdict including a pass.
/// A gate that cannot say what it checked has not checked anything, and a pass
/// with no evidence is the exact shape of a green suite that tests nothing.</item>
/// <item><see cref="Reason"/> is mandatory on Refuse and Skip. A refusal nobody
/// can explain is indistinguishable from a bug, and a silent skip reads as a
/// pass to every human who scans the log.</item>
/// </list>
/// </summary>
public sealed record GateVerdict
{
    /// <summary>Stable gate identifier, for example "G4".</summary>
    public required string GateId { get; init; }

    /// <summary>Human readable gate name, for example "document-location-cardinality".</summary>
    public required string GateName { get; init; }

    /// <summary>The conclusion.</summary>
    public required GateOutcome Outcome { get; init; }

    /// <summary>
    /// Repository relative path of the artifact examined. Relative so the
    /// ledger stays reproducible across machines and does not leak absolute
    /// paths into a public repo.
    /// </summary>
    public required string Artifact { get; init; }

    /// <summary>
    /// What was actually inspected, stated concretely enough that a reader can
    /// re-run it by hand. "Checked 3 relationships against SharePointDocumentLocation"
    /// is evidence. "Validated" is not.
    /// </summary>
    public required string Evidence { get; init; }

    /// <summary>
    /// Why the gate refused or skipped. Null is permitted only on <see cref="GateOutcome.Pass"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the verdict was reached. Supplied by the caller rather than read
    /// from the clock inside this type, so tests are deterministic and the
    /// ledger can be replayed.
    /// </summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    /// Where the gate ran. Present so a reader can tell a generation time
    /// refusal from the CI re-check of the same rule.
    /// </summary>
    public required GateStage Stage { get; init; }

    public GateVerdict()
    {
    }

    /// <summary>
    /// Validates the two invariants. Called by the ledger before any write, so
    /// an invalid verdict cannot reach disk.
    /// </summary>
    /// <exception cref="InvalidVerdictException">A required field is absent.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GateId))
            throw new InvalidVerdictException("GateId is required.");

        if (string.IsNullOrWhiteSpace(GateName))
            throw new InvalidVerdictException($"{GateId}: GateName is required.");

        if (string.IsNullOrWhiteSpace(Artifact))
            throw new InvalidVerdictException($"{GateId}: Artifact is required.");

        if (string.IsNullOrWhiteSpace(Evidence))
            throw new InvalidVerdictException(
                $"{GateId}: Evidence is required on every verdict, including a pass. " +
                "A gate that cannot state what it checked has not checked anything.");

        if (Outcome != GateOutcome.Pass && string.IsNullOrWhiteSpace(Reason))
            throw new InvalidVerdictException(
                $"{GateId}: Reason is required on {Outcome}. " +
                "An unexplained refusal is indistinguishable from a defect, and a " +
                "silent skip reads as a pass.");
    }
}

/// <summary>Where a gate ran.</summary>
public enum GateStage
{
    /// <summary>At artifact generation, before anything reached disk.</summary>
    Generation,

    /// <summary>In CI, over what was committed. The externally verifiable rung.</summary>
    Integration
}

/// <summary>A verdict was malformed and was refused before it could be recorded.</summary>
public sealed class InvalidVerdictException(string message) : Exception(message);
