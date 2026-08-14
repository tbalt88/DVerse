using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// Explicit "no matcher exists for this yet" registration, per mission
/// ruling 2 ("every ratified class family ... gets a matcher or is
/// explicitly registered as unsurveyed"). Used today for exactly one class
/// family: MissingDependencies (ratified model section 4 item 3), which has
/// zero real instances anywhere in the surveyed corpus
/// (<c>MissingDependencies: {}</c> in every committed
/// missingdependencies.yml) and therefore no basis to derive an identity
/// key from at all -- not "unknown key", but no known shape to key
/// anything from in the first place.
/// <para>
/// This never silently matches, adds, or removes anything: if both sides
/// are empty (the only shape ever observed), it returns
/// <see cref="MatchResult.Empty"/> with no warning, since there is nothing
/// to be honest about. If either side carries any content at all -- a real
/// instance this model has never seen -- every one of its children is
/// reported as a warning rather than guessed at as matched, added, or
/// removed, because this class's honest-fallback status means this matcher
/// has no rule to apply to that content yet.
/// </para>
/// </summary>
public sealed class UnsurveyedFamilyMatcher(string classFamily, Func<YamlNode, YamlNode?> locate) : IElementMatcher
{
    public string ClassFamily { get; } = classFamily;

    public MatchResult Match(YamlNode a, YamlNode b)
    {
        var aContent = locate(a);
        var bContent = locate(b);

        var aIsEmpty = IsEmpty(aContent);
        var bIsEmpty = IsEmpty(bContent);

        if (aIsEmpty && bIsEmpty)
            return MatchResult.Empty;

        var warnings = new List<MatchWarning>
        {
            new(
                ClassFamily,
                $"{ClassFamily} is unsurveyed: no real instance existed anywhere in the corpus at " +
                "ratification time (docs/design/element-identity-model.md section 4), so no identity " +
                "key could be derived. Non-empty content was found where the model has none to " +
                "compare against; this cannot be matched, added, or removed with any confidence " +
                "until a real instance is surveyed and this matcher is replaced.")
        };

        return new MatchResult([], [], [], warnings);
    }

    private static bool IsEmpty(YamlNode? node) => node switch
    {
        null => true,
        YamlMappingNode mapping => mapping.Children.Count == 0,
        YamlSequenceNode sequence => sequence.Children.Count == 0,
        _ => false
    };
}
