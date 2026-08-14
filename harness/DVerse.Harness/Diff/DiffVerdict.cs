namespace DVerse.Harness.Diff;

/// <summary>
/// The four verdict kinds the semantic diff engine (wave 7.2) ever assigns
/// to one element at one nesting level, per mission ruling 2.
/// </summary>
public enum DiffVerdictKind
{
    Added,
    Removed,
    Changed,
    Unchanged
}

/// <summary>
/// One scalar property that differs between the baseline (A) and target (B)
/// side of a matched element. <see cref="PropertyPath"/> is a dot-joined
/// path rooted at the matched element's own node (for example
/// "labels.label.@description" on a FormXml cell, or "@datafieldname" on a
/// control); it never reaches into a child-family subtree the walker
/// recurses into separately (a FormXml tab's "columns", a row's "cell",
/// AppModule's "AppModuleComponents", ...), because that subtree gets its
/// own verdicts at its own nesting level instead (see
/// <see cref="ArtifactDiffer"/>'s recursion rule).
/// <para>
/// Mission ruling 4: scalar properties compare by string value; the "="
/// prefix on a canvas formula is part of that value, never stripped;
/// attribute order is not significant (paths are compared by name, not
/// position); and missing-vs-empty is preserved -- <see cref="ValueInA"/>
/// or <see cref="ValueInB"/> is null when the path does not exist at all on
/// that side, distinct from an empty string, which means the path exists
/// and holds an empty scalar.
/// </para>
/// </summary>
public sealed record PropertyChange(string PropertyPath, string? ValueInA, string? ValueInB);

/// <summary>
/// One element's verdict from a family-chain walk. <see cref="Identity"/>
/// always carries the ratified identity model's class kind and key (every
/// verdict carries the element's identity, mission ruling 2), including for
/// Added/Removed elements, where the key is the best identity this matcher
/// could still assign on the one side the element exists on.
/// <para>
/// <see cref="Warnings"/> holds every match warning INHERITED from an
/// ancestor positional pairing on the walk down to this element (mission
/// ruling 5: "positional-match warnings [...] propagate into every verdict
/// under that pairing"). It intentionally does not repeat this element's
/// own warning if it has one of its own; that is already visible on
/// <see cref="Identity"/>.IsWarning and in the enclosing
/// <see cref="ArtifactDiff"/>.Warnings aggregate.
/// </para>
/// <para>
/// When <see cref="Kind"/> is <see cref="DiffVerdictKind.Changed"/> and this
/// element sits under a positional pairing (its own match or an ancestor's),
/// <see cref="Description"/> carries the literal ratified-doctrine phrase
/// "unverified pairing, content-confirmed only" (mission ruling 5), because
/// the only thing that makes such a pairing trustworthy at all is the
/// content match underneath it, never a real identity key.
/// </para>
/// </summary>
public sealed record DiffVerdict(
    DiffVerdictKind Kind,
    ElementIdentity Identity,
    string Description,
    IReadOnlyList<PropertyChange> PropertyChanges,
    IReadOnlyList<MatchWarning> Warnings);
