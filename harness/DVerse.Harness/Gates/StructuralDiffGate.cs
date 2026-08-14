using System.Text.RegularExpressions;
using DVerse.Harness.Diff;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G12: structural diff, the gate that makes "what changed" refusable.
/// <para>
/// Unlike every other gate, G12 needs TWO trees: a baseline (the caller
/// supplies it; in practice a git worktree or an unpacked prior version) and
/// the current solution tree already carried on <see cref="GateContext.SolutionRoot"/>.
/// <see cref="GateContext"/> carries no second root (touching that shared
/// contract file is outside this slice's owned files), so the baseline is
/// this GATE's own constructor parameter instead: the CLI (Cli/Program.cs)
/// substitutes a baseline-carrying instance into the ladder only when
/// <c>--baseline</c> is supplied, and <see cref="GateRegistry"/>'s always-present
/// catalogue entry carries a null baseline, which is what makes the standard
/// <c>gate run</c> ladder SKIP honestly instead of needing the contract
/// changed everywhere else. Ruling 1's frozen SKIP reason
/// ("no baseline provided; structural diff requires two trees") is emitted
/// verbatim so a reader scanning the ledger sees exactly why, not a paraphrase.
/// </para>
/// <para>
/// PAIRING: every file under both trees whose repo-relative-to-solution-root
/// path matches one of <see cref="ClassificationRules"/> is treated as one
/// artifact instance of the named family (docs/design/element-identity-model.md
/// section 3); everything else (README.md, compiled plugin DLL/C#/csproj/snk,
/// canvas .msapr binary, App.pa.yaml/_EditorState.pa.yaml -- neither of which
/// carries a "Screens:" root the model defines a family for) is not a
/// declarative artifact this survey covers and is silently excluded from
/// pairing, the same scope line the identity model's own "files read for this
/// survey" section draws. A path present under only one tree is reported as a
/// whole-file Added/Removed Pass verdict with no recursion into its contents
/// (mission ruling 3: "no recursion into an added file, per the model's
/// short-circuit rule", docs/design/element-identity-model.md section 5's own
/// short-circuit clause). A path present under both trees is walked with
/// <see cref="ArtifactDiffer"/>.
/// </para>
/// <para>
/// REFUSAL, exactly three classes (mission statement), independent of one
/// another and each checked over every verdict <see cref="ArtifactDiffer.Diff"/>
/// produces for a pair:
/// </para>
/// <list type="number">
/// <item>a Changed verdict whose property path is literally <c>@datafieldname</c>
/// where the new value is not the exact lowercase of itself -- lesson 14's
/// class (loop/LESSONS.md #14), caught here at diff time instead of only by
/// driving the rendered form after the fact;</item>
/// <item>any verdict whose own identity carries an unsurveyed-type
/// (<see cref="DiffClassFamilies.RootComponentsEntry"/>, seat ruling 8) or
/// unsurveyed-control (<see cref="DiffClassFamilies.FormXmlControl"/>, seat
/// ruling 3) warning -- <see cref="ElementIdentity.IsWarning"/> is true for
/// FormXml column/row positional pairings too, so the check is scoped to
/// exactly these two class kinds, never a blanket "IsWarning" test, or every
/// positional-pairing PASS the mission explicitly wants would refuse instead;</item>
/// <item>a Removed verdict on a <see cref="DiffClassFamilies.RootComponentsEntry"/>
/// or <see cref="DiffClassFamilies.SolutionComponentsEntry"/> whose resolved
/// source still exists on disk under the CURRENT tree -- lesson 2's class
/// (a packaging manifest entry silently dropped while the real component is
/// still sitting right there), caught before pack rather than after an import
/// failure. Source resolution for RootComponentsEntry reuses exactly
/// <see cref="RootComponentSourceGate"/>'s own known-type path templates
/// (Entity=1, CanvasApp=300); an entry of an unmapped type is honestly
/// skipped for this check, the same "not evidence of a missing source, only
/// of a gap in this gate's mapping table" policy G8 already documents, not a
/// silent pass dressed up as confidence.</item>
/// </list>
/// <para>
/// Everything else -- adds, removes, ordinary changes, positional-warning
/// pairings -- PASSES, with Evidence carrying the full verdict summary
/// (counts + every changed property path + every warning's text verbatim,
/// including positional-pairing warnings), so a reader never has to go open
/// the ledger's raw JSON to see what a passing diff actually found.
/// </para>
/// </summary>
public sealed class StructuralDiffGate : IGate
{
    public string Id => "G12";
    public string Name => "structural-diff";
    public bool RequiresTenant => false;

    /// <summary>
    /// The baseline tree to compare <see cref="GateContext.SolutionRoot"/>
    /// against, or null when this gate is running in the standard offline
    /// ladder with no baseline supplied (ruling 1's honest SKIP).
    /// </summary>
    private readonly string? _baselineRoot;

    private static readonly ArtifactDiffer Differ = new();

    /// <summary>
    /// File-path-to-artifact-class classification table. Matched against the
    /// path RELATIVE TO THE SOLUTION ROOT (forward-slash, the same convention
    /// every path in this codebase normalizes to), independent of repository
    /// root. Order does not matter: patterns are mutually exclusive by
    /// construction (disjoint folder/filename shapes), never tested for
    /// overlap because none exists in the real layout every other gate in
    /// this codebase already assumes (YamlLayoutGate, RootComponentSourceGate,
    /// SolutionComponentPathGate).
    /// <para>
    /// CanvasScreen is scoped to files literally named "Screen*.pa.yaml"
    /// rather than every ".pa.yaml" under Src/, deliberately: App.pa.yaml
    /// (root key "App", not "Screens") and _EditorState.pa.yaml (Studio's own
    /// editor-only metadata, no declarative content at all) do not carry the
    /// "Screens:" root <see cref="MatcherRegistry"/>'s CanvasScreen matcher
    /// expects; the matcher itself would not crash on them (it degrades to a
    /// harmless empty screen collection when "Screens" is absent), but
    /// pairing them at all would misrepresent them as governed declarative
    /// artifacts when the identity model defines no family for either.
    /// </para>
    /// </summary>
    private static readonly (Regex Pattern, string ArtifactClass)[] ClassificationRules =
    [
        (new Regex(@"^solutions/[^/]+/solution\.yml$"), DiffClassFamilies.SolutionManifest),
        (new Regex(@"^solutions/[^/]+/solutioncomponents\.yml$"), DiffClassFamilies.SolutionComponentsEntry),
        (new Regex(@"^solutions/[^/]+/rootcomponents\.yml$"), DiffClassFamilies.RootComponentsEntry),
        (new Regex(@"^solutions/[^/]+/missingdependencies\.yml$"), DiffClassFamilies.MissingDependencies),
        (new Regex(@"^publishers/[^/]+/publisher\.yml$"), DiffClassFamilies.Publisher),
        (new Regex(@"^entities/[^/]+/Entity\.yml$"), DiffClassFamilies.Entity),
        (new Regex(@"^entities/[^/]+/attributes/[^/]+\.yml$"), DiffClassFamilies.Attribute),
        (new Regex(@"^entities/[^/]+/FormXml/[^/]+/[^/]+\.yml$"), DiffClassFamilies.FormXmlSystemForm),
        (new Regex(@"^entities/[^/]+/SavedQueries/[^/]+\.yml$"), DiffClassFamilies.SavedQuery),
        (new Regex(@"^entityrelationships/[^/]+\.yml$"), DiffClassFamilies.EntityRelationship),
        (new Regex(@"^appmodules/[^/]+/appmodule\.yml$"), DiffClassFamilies.AppModule),
        (new Regex(@"^appmodulesitemaps/[^/]+/appmodulesitemap\.yml$"), DiffClassFamilies.AppModuleSiteMap),
        (new Regex(@"^pluginassemblies/[^/]+\.yml$"), DiffClassFamilies.PluginAssembly),
        (new Regex(@"^sdkmessageprocessingsteps/[^/]+\.yml$"), DiffClassFamilies.SdkMessageProcessingStep),
        (new Regex(@"^canvasapps/[^/]+/Src/Screen[^/]*\.pa\.yaml$"), DiffClassFamilies.CanvasScreen)
    ];

    /// <summary>
    /// Numeric RootComponent <c>@type</c> to on-disk source folder template,
    /// reused verbatim from <see cref="RootComponentSourceGate"/> (G8) rather
    /// than re-derived: the two gates must agree on where a component's
    /// source lives, or a G8 pass and a G12 refusal could disagree about the
    /// same fact from the same decompiled ground truth.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RootComponentPathTemplates =
        new Dictionary<string, string>
        {
            ["1"] = "entities/{0}",     // Entity
            ["300"] = "canvasapps/{0}"  // CanvasApp
        };

    private static readonly Regex RootComponentKeyPattern = new(@"^type=(?<type>[^,]*), (?<id>.*)$");

    public StructuralDiffGate(string? baselineRoot = null)
    {
        _baselineRoot = baselineRoot;
    }

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        if (_baselineRoot is null)
        {
            yield return Skip(
                context,
                RelativePath(context, context.SolutionRoot),
                "Gate not executed.",
                "no baseline provided; structural diff requires two trees");
            yield break;
        }

        if (!Directory.Exists(_baselineRoot))
        {
            yield return Refuse(
                context,
                RelativePath(context, context.SolutionRoot),
                "The baseline tree supplied to this gate does not exist on disk.",
                "No baseline solution tree was found at the supplied path, so no structural diff " +
                "can be computed. A missing baseline is refused rather than silently skipped, " +
                "because --baseline was explicitly supplied: an honest SKIP is reserved for the " +
                "'no baseline at all' case (ruling 1), not for a baseline path that turned out to " +
                "be wrong.");
            yield break;
        }

        var baselineFiles = DiscoverClassifiedFiles(_baselineRoot);
        var targetFiles = DiscoverClassifiedFiles(context.SolutionRoot);

        var allPaths = baselineFiles.Keys
            .Union(targetFiles.Keys, StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var relPath in allPaths)
        {
            var inBaseline = baselineFiles.TryGetValue(relPath, out var classA);
            var inTarget = targetFiles.TryGetValue(relPath, out var classB);
            var artifactClass = classB ?? classA!;

            if (inBaseline && !inTarget)
            {
                yield return Pass(
                    context,
                    RelativeArtifact(context, relPath),
                    $"{relPath} ({artifactClass}): present in the baseline tree only, absent from " +
                    "the current tree. Reported as Removed at the artifact level; not recursed into, " +
                    "per the model's short-circuit rule (docs/design/element-identity-model.md " +
                    "section 5).");
                continue;
            }

            if (!inBaseline && inTarget)
            {
                yield return Pass(
                    context,
                    RelativeArtifact(context, relPath),
                    $"{relPath} ({artifactClass}): present in the current tree only, absent from the " +
                    "baseline. Reported as Added at the artifact level; not recursed into, per the " +
                    "model's short-circuit rule (docs/design/element-identity-model.md section 5).");
                continue;
            }

            foreach (var verdict in EvaluatePair(context, relPath, artifactClass))
                yield return verdict;
        }
    }

    private IEnumerable<GateVerdict> EvaluatePair(GateContext context, string relPath, string artifactClass)
    {
        YamlNode? a = null, b = null;
        Exception? parseError = null;

        try
        {
            a = LoadRoot(Path.Combine(_baselineRoot!, relPath.Replace('/', Path.DirectorySeparatorChar)));
            b = LoadRoot(Path.Combine(context.SolutionRoot, relPath.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is YamlException or InvalidDataException)
        {
            parseError = ex;
        }

        if (parseError is not null)
        {
            yield return Refuse(
                context,
                RelativeArtifact(context, relPath),
                $"Attempted to parse {relPath} as YAML on both sides of the diff.",
                $"{relPath} could not be parsed as YAML: {parseError.Message}. No structural diff can " +
                "be trusted for a pair where one side does not even parse.");
            yield break;
        }

        var diff = Differ.Diff(a!, b!, artifactClass);

        var violations = new List<(string Reason, string Evidence)>();
        CollectDatafieldnameViolations(relPath, diff, violations);
        CollectUnsurveyedViolations(relPath, diff, violations);
        CollectPackagingRemovalViolations(context, relPath, artifactClass, diff, violations);

        if (violations.Count > 0)
        {
            foreach (var (reason, evidence) in violations)
                yield return Refuse(context, RelativeArtifact(context, relPath), evidence, reason);

            yield break;
        }

        yield return Pass(context, RelativeArtifact(context, relPath), SummaryEvidence(relPath, artifactClass, diff));
    }

    // --- Refuse class 1: lesson 14's datafieldname-casing class -------------------------------------

    private static void CollectDatafieldnameViolations(
        string relPath, ArtifactDiff diff, List<(string Reason, string Evidence)> violations)
    {
        foreach (var verdict in diff.Verdicts)
        {
            foreach (var change in verdict.PropertyChanges)
            {
                if (change.PropertyPath != "@datafieldname" || change.ValueInB is null)
                    continue;

                if (change.ValueInB == change.ValueInB.ToLowerInvariant())
                    continue;

                violations.Add((
                    $"{verdict.Identity.ClassKind}[{verdict.Identity.Key}]'s '@datafieldname' changed " +
                    $"to '{change.ValueInB}', which is not the exact lowercase of itself " +
                    $"('{change.ValueInB.ToLowerInvariant()}'). loop/LESSONS.md #14: a FormXml control's " +
                    "datafieldname binds by the attribute's lowercase LogicalName; any other casing " +
                    "makes the control drop SILENTLY at render, after pack, import, and publish all " +
                    "accept it without complaint.",
                    $"{relPath}: {verdict.Description}; '@datafieldname' '{change.ValueInA ?? "(absent)"}' " +
                    $"-> '{change.ValueInB}'."));
            }
        }
    }

    // --- Refuse class 2: unsurveyed-type / unsurveyed-control warnings (rulings 3 and 8) -------------

    private static void CollectUnsurveyedViolations(
        string relPath, ArtifactDiff diff, List<(string Reason, string Evidence)> violations)
    {
        foreach (var verdict in diff.Verdicts)
        {
            if (!verdict.Identity.IsWarning)
                continue;

            var isControl = verdict.Identity.ClassKind == DiffClassFamilies.FormXmlControl;
            var isRootComponent = verdict.Identity.ClassKind == DiffClassFamilies.RootComponentsEntry;

            if (!isControl && !isRootComponent)
                continue; // FormXmlColumn/FormXmlRow positional pairings also carry IsWarning=true; PASS.

            var kind = isControl ? "unsurveyed-control" : "unsurveyed-type";
            var ruling = isControl ? "seat ruling 3" : "seat ruling 8";

            violations.Add((
                $"{verdict.Identity.ClassKind}[{verdict.Identity.Key}] carries an {kind} warning " +
                $"({ruling}): docs/design/element-identity-model.md's own ratified doctrine is that " +
                "matching against a class this model has never surveyed is allowed, but trusting that " +
                "match SILENTLY is not. An unsurveyed change is an unverifiable change.",
                $"{relPath}: {verdict.Description}"));
        }
    }

    // --- Refuse class 3: packaging removal with source still present (lesson 2's class) --------------

    private static void CollectPackagingRemovalViolations(
        GateContext context, string relPath, string artifactClass, ArtifactDiff diff,
        List<(string Reason, string Evidence)> violations)
    {
        if (artifactClass != DiffClassFamilies.RootComponentsEntry &&
            artifactClass != DiffClassFamilies.SolutionComponentsEntry)
        {
            return;
        }

        foreach (var verdict in diff.Verdicts)
        {
            if (verdict.Kind != DiffVerdictKind.Removed)
                continue;

            var sourcePath = ResolveSourcePath(artifactClass, verdict.Identity.Key);
            if (sourcePath is null)
                continue; // Unmapped type or unresolvable key: honest skip, mirrors G8's own gap policy.

            var target = Path.Combine(context.SolutionRoot, sourcePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(target) && !Directory.Exists(target))
                continue;

            violations.Add((
                $"{verdict.Identity.ClassKind}[{verdict.Identity.Key}] was removed from {relPath} " +
                $"between the baseline and the current tree, but '{sourcePath}' still exists on disk " +
                "under the current solution root. loop/LESSONS.md #2: SolutionPackager silently drops " +
                "a component whose packaging entry is absent and still exits 0; this is that same trap, " +
                "caught here before pack rather than after an import failure against a build that " +
                "reported success.",
                $"{relPath}: {verdict.Description}; resolved source '{sourcePath}' still present " +
                "under the current solution root."));
        }
    }

    private static string? ResolveSourcePath(string artifactClass, string identityKey)
    {
        if (artifactClass == DiffClassFamilies.SolutionComponentsEntry)
            return string.IsNullOrWhiteSpace(identityKey) ? null : identityKey;

        var match = RootComponentKeyPattern.Match(identityKey);
        if (!match.Success)
            return null;

        var type = match.Groups["type"].Value;
        var identifier = match.Groups["id"].Value;

        if (identifier is "(unknown)" or "")
            return null;

        return RootComponentPathTemplates.TryGetValue(type, out var template)
            ? string.Format(template, identifier)
            : null;
    }

    // --- Pass evidence: full verdict summary, changed property paths, warnings verbatim ---------------

    private static string SummaryEvidence(string relPath, string artifactClass, ArtifactDiff diff)
    {
        var summary = $"{relPath} ({artifactClass}): {diff.Summary.Added} added, {diff.Summary.Removed} " +
                       $"removed, {diff.Summary.Changed} changed, {diff.Summary.Unchanged} unchanged " +
                       $"(total {diff.Summary.Total} element(s) walked).";

        var propertyPaths = diff.Verdicts
            .SelectMany(v => v.PropertyChanges)
            .Select(p => p.PropertyPath)
            .Distinct()
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (propertyPaths.Count > 0)
            summary += $" Changed property paths: {string.Join(", ", propertyPaths)}.";

        if (diff.Warnings.Count > 0)
        {
            var warningText = string.Join(" | ", diff.Warnings.Select(w => $"{w.Path}: {w.Reason}"));
            summary += $" Warnings: {warningText}";
        }

        return summary;
    }

    // --- File discovery and classification -------------------------------------------------------------

    private static IReadOnlyDictionary<string, string> DiscoverClassifiedFiles(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!Directory.Exists(root))
            return result;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var artifactClass = Classify(relative);

            if (artifactClass is not null)
                result[relative] = artifactClass;
        }

        return result;
    }

    private static string? Classify(string relativePath)
    {
        foreach (var (pattern, artifactClass) in ClassificationRules)
        {
            if (pattern.IsMatch(relativePath))
                return artifactClass;
        }

        return null;
    }

    private static YamlNode LoadRoot(string path)
    {
        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0)
            throw new InvalidDataException($"{path}: empty YAML document.");

        return stream.Documents[0].RootNode;
    }

    // --- Path helpers, verdict builders ----------------------------------------------------------------

    private static string RelativePath(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    /// <summary>Repo-relative path of a file addressed by its solution-root-relative path.</summary>
    private static string RelativeArtifact(GateContext context, string relPathUnderSolution) =>
        RelativePath(context, Path.Combine(context.SolutionRoot, relPathUnderSolution.Replace('/', Path.DirectorySeparatorChar)));

    private static GateVerdict Skip(GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G12",
        GateName = "structural-diff",
        Outcome = GateOutcome.Skip,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Pass(GateContext context, string artifact, string evidence) => new()
    {
        GateId = "G12",
        GateName = "structural-diff",
        Outcome = GateOutcome.Pass,
        Artifact = artifact,
        Evidence = evidence,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Refuse(GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G12",
        GateName = "structural-diff",
        Outcome = GateOutcome.Refuse,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };
}
