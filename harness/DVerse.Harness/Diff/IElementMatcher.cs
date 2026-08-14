using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// Matches one ratified class family's elements between two versions of the
/// same declarative artifact. One matcher, one class family: the seam is
/// deliberately narrow so the registry (<see cref="MatcherRegistry"/>) can
/// hold one entry per row of docs/design/element-identity-model.md section
/// 3 without any of them knowing about each other.
/// <para>
/// <see cref="Match"/> takes whatever YAML node is the natural container
/// for that family's elements in the parsed document tree: for a top-level,
/// one-per-file component (Publisher, Entity, PluginAssembly, a
/// SavedQuery, ...) that is the parsed file root; for a nested collection
/// (FormXml tabs, AppModuleComponents, PluginTypes, ...) it is the direct
/// parent node whose children are the family (the <c>form</c> node for
/// tabs, the <c>PluginAssembly</c> node for PluginTypes, and so on); for
/// canvas controls specifically it is whatever parent control or screen
/// node the caller wants to compare, because canvas identity is scoped to
/// the immediate parent by ruling, never global. Each concrete matcher's
/// own doc comment states which node it expects. This slice does not walk
/// the whole document recursively on a caller's behalf (matching one
/// already-located pair of tabs down into their sections, for example, is
/// the next matcher call, made by whatever orchestrates the walk); it
/// answers "which element in B is the same element as this one in A" one
/// class family at a time, per the mission's own scope line.
/// </para>
/// </summary>
public interface IElementMatcher
{
    /// <summary>
    /// Stable name of the class family this matcher covers, matching the
    /// naming used in <see cref="MatcherRegistry"/> and in
    /// docs/design/element-identity-model.md section 3 (for example
    /// "FormXmlControl", "RootComponentsEntry").
    /// </summary>
    string ClassFamily { get; }

    /// <summary>
    /// Matches this family's elements between baseline (<paramref name="a"/>)
    /// and target (<paramref name="b"/>). Never throws on a shape this
    /// matcher does not recognise; an element that cannot be keyed lands in
    /// <see cref="MatchResult.Warnings"/> instead, per the model's ruling
    /// that unknown shapes warn rather than silently match or vanish.
    /// </summary>
    MatchResult Match(YamlNode a, YamlNode b);
}
