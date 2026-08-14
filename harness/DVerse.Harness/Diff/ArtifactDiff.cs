namespace DVerse.Harness.Diff;

/// <summary>Verdict counts across an entire <see cref="ArtifactDiff"/>, for quick reporting without re-scanning <see cref="ArtifactDiff.Verdicts"/>.</summary>
public sealed record DiffSummary(int Added, int Removed, int Changed, int Unchanged)
{
    public int Total => Added + Removed + Changed + Unchanged;
}

/// <summary>
/// The complete result of walking one artifact class's family chain
/// (<see cref="ArtifactDiffer.Diff"/>) across two versions of the same
/// declarative artifact document: every element's verdict at every nesting
/// level, in the order the walk visited them (a stable, deterministic
/// pre-order: a matched parent's own verdict, immediately followed by its
/// added/removed/matched children, recursively), every warning any matcher
/// on the walk produced (unattributed to a specific verdict; 7.1i mission
/// ruling 4's "warnings are data" contract, aggregated here for the whole
/// diff), and the summary counts.
/// </summary>
public sealed record ArtifactDiff(
    string ArtifactClass,
    IReadOnlyList<DiffVerdict> Verdicts,
    IReadOnlyList<MatchWarning> Warnings,
    DiffSummary Summary);
