using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// One element present in both document versions, correlated by
/// <see cref="Identity"/>. <see cref="A"/> and <see cref="B"/> are the
/// baseline and target element nodes respectively; this slice carries no
/// property-level comparison between them (that is 7.2's job), only the
/// fact that they are, per the ratified identity model, the same element.
/// </summary>
public sealed record MatchedPair(YamlNode A, YamlNode B, ElementIdentity Identity);

/// <summary>
/// One reason a matcher is not fully confident in a verdict it still had to
/// produce. Warnings are DATA, not log lines (mission ruling 4): they ride
/// alongside the match result so a later stage (7.3's diff-aware gates) can
/// turn specific warning shapes into refusals instead of the diff engine
/// deciding that on its own. <see cref="Path"/> is a human-readable
/// description of the element the warning concerns, not a filesystem path.
/// </summary>
public sealed record MatchWarning(string Path, string Reason);

/// <summary>
/// What one matcher concluded about one class family across two document
/// versions: which elements are the same element (<see cref="Matched"/>),
/// which exist only in the target (<see cref="Added"/>), which exist only
/// in the baseline (<see cref="Removed"/>), and every reason
/// (<see cref="Warnings"/>) the matcher was not fully confident in a
/// verdict it still produced. No verdict is ever silently withheld: an
/// element that cannot be matched, added, or removed with confidence still
/// lands in one of those three buckets, accompanied by a warning explaining
/// why, per the model's ruling that unknown shapes warn rather than match
/// silently or vanish.
/// </summary>
public sealed record MatchResult(
    IReadOnlyList<MatchedPair> Matched,
    IReadOnlyList<YamlNode> Added,
    IReadOnlyList<YamlNode> Removed,
    IReadOnlyList<MatchWarning> Warnings)
{
    /// <summary>Zero elements on both sides. Every matcher returns this for two empty containers.</summary>
    public static readonly MatchResult Empty = new([], [], [], []);
}
