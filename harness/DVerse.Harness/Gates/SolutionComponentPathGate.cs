using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G9: every declared component in every <c>solutions/*/solutioncomponents.yml</c>
/// must resolve to a real file or directory under the solution root.
/// <para>
/// This exists because SolutionPackager's own documented behaviour makes the
/// failure silent: if a declared component's source is absent, pack drops the
/// component, still exits 0, and the missing piece only surfaces later as an
/// import failure against a build that reported success. This gate turns that
/// exit-code-0 lie into a refusal before the pack ever runs.
/// </para>
/// <para>
/// WHY the shape below, not the one in Microsoft's docs: Microsoft's published
/// documentation shows solutioncomponents.yml as a flat sequence
/// (<c>- Path: entities/account</c>). Decompiling pac 2.10.1's
/// SolutionPackagerLib.dll (DiskReader.GetSolutionComponentPaths) during slice
/// 4.1 showed the real parser requires a <c>SolutionComponents: Component:</c>
/// mapping whose entries carry a literal <c>@path</c> key instead. This gate
/// parses only the pac-verified shape; the documented shape is refused on
/// sight so a file that passes G9 is a file pac itself will actually pack.
/// This pin is tied to pac 2.10.1's observed behaviour and may need
/// revisiting if a future pac release changes its parser.
/// </para>
/// </summary>
public sealed class SolutionComponentPathGate : IGate
{
    public string Id => "G9";
    public string Name => "solution-component-paths";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var solutionsDir = Path.Combine(context.SolutionRoot, "solutions");

        if (!Directory.Exists(solutionsDir))
        {
            yield return Refuse(
                context,
                RelativePath(context, solutionsDir),
                "Looked for a 'solutions' folder under the solution root; none exists.",
                "No 'solutions' folder was found, so no solutioncomponents.yml can be checked. " +
                "There is nothing here for a pack to succeed over.");
            yield break;
        }

        var solutionDirs = Directory.GetDirectories(solutionsDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        if (solutionDirs.Count == 0)
        {
            yield return Refuse(
                context,
                RelativePath(context, solutionsDir),
                "Listed the 'solutions' folder; it contains zero solution directories.",
                "The 'solutions' folder is empty. There is no solutioncomponents.yml to verify, " +
                "which is indistinguishable from every declared component having been lost.");
            yield break;
        }

        foreach (var solutionDir in solutionDirs)
        {
            foreach (var verdict in EvaluateSolution(context, solutionDir))
                yield return verdict;
        }
    }

    private IEnumerable<GateVerdict> EvaluateSolution(GateContext context, string solutionDir)
    {
        var componentsFile = Path.Combine(solutionDir, "solutioncomponents.yml");
        var relComponentsFile = RelativePath(context, componentsFile);

        if (!File.Exists(componentsFile))
        {
            yield return Refuse(
                context,
                relComponentsFile,
                $"Looked for {relComponentsFile}; the file is absent.",
                "solutioncomponents.yml is missing, so no declared component can be verified to " +
                "exist on disk before packing.");
            yield break;
        }

        ParsedManifest? manifest;
        YamlException? parseError = null;
        try
        {
            manifest = ParseComponents(componentsFile);
        }
        catch (YamlException ex)
        {
            manifest = null;
            parseError = ex;
        }

        if (parseError is not null)
        {
            yield return Refuse(
                context,
                relComponentsFile,
                $"Attempted to parse {relComponentsFile} as YAML.",
                $"solutioncomponents.yml could not be parsed as YAML: {parseError.Message}");
            yield break;
        }

        if (manifest!.IsLegacyShape)
        {
            yield return Refuse(
                context,
                relComponentsFile,
                $"Parsed {relComponentsFile}; its document root is a sequence, the flat " +
                "'- Path: ...' shape shown in Microsoft's documentation.",
                $"{relComponentsFile} uses the shape shown in Microsoft's own documentation for " +
                "solutioncomponents.yml (a flat sequence of '- Path: ...' entries), but pac's " +
                "actual parser does not accept that shape (verified by decompiling pac 2.10.1's " +
                "SolutionPackagerLib.dll, DiskReader.GetSolutionComponentPaths, during slice 4.1). " +
                "pac requires a 'SolutionComponents: Component:' mapping whose entries carry a " +
                "literal '@path' key, for example: SolutionComponents: / Component: / - '@path': " +
                $"entities/account. Rewrite {relComponentsFile} to that shape.");
            yield break;
        }

        var paths = manifest.Paths;

        if (paths.Count == 0)
        {
            yield return Refuse(
                context,
                relComponentsFile,
                $"Parsed {relComponentsFile}; it declares zero Component entries.",
                "solutioncomponents.yml declares no components. An empty component list packs " +
                "successfully but ships nothing, which is indistinguishable from every declared " +
                "component silently having been dropped.");
            yield break;
        }

        var unresolvedCount = 0;

        foreach (var declaredPath in paths)
        {
            if (string.IsNullOrWhiteSpace(declaredPath))
            {
                unresolvedCount++;
                yield return Refuse(
                    context,
                    relComponentsFile,
                    $"Read a Component entry from {relComponentsFile}; its '@path' value is blank.",
                    "solutioncomponents.yml contains a Component entry with no '@path' value, so " +
                    "nothing can be resolved for it and the packer will silently drop that " +
                    "component.");
                continue;
            }

            var target = Path.Combine(
                context.SolutionRoot,
                declaredPath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(target) || Directory.Exists(target))
                continue;

            unresolvedCount++;
            var relTarget = RelativePath(context, target);

            yield return Refuse(
                context,
                relTarget,
                $"Resolved '@path' '{declaredPath}' from {relComponentsFile} against disk; " +
                "no file or directory exists there.",
                $"solutioncomponents.yml declares '{declaredPath}' but no such file or directory " +
                "exists under the solution root. SolutionPackager will omit this component and " +
                "still exit 0, so import will fail on a component the build reported as successful.");
        }

        if (unresolvedCount == 0)
        {
            yield return Pass(
                context,
                relComponentsFile,
                $"Resolved {paths.Count} Component " +
                $"entr{(paths.Count == 1 ? "y" : "ies")} from {relComponentsFile} against disk " +
                $"under {RelativePath(context, context.SolutionRoot)}; all exist.");
        }
    }

    /// <summary>
    /// Parses solutioncomponents.yml with the low-level representation model rather
    /// than a POCO mapping, so the shape of the document root (mapping vs. sequence)
    /// can be inspected directly: that distinction is how the documented legacy shape
    /// is told apart from the pac-verified shape.
    /// </summary>
    private static ParsedManifest ParseComponents(string componentsFile)
    {
        using var reader = new StreamReader(componentsFile);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
            return new ParsedManifest(IsLegacyShape: false, Paths: []);

        var root = yamlStream.Documents[0].RootNode;

        if (root is YamlSequenceNode)
            return new ParsedManifest(IsLegacyShape: true, Paths: []);

        var paths = new List<string?>();

        if (root is YamlMappingNode rootMapping &&
            TryGetMappingValue(rootMapping, "SolutionComponents", out var solutionComponentsNode) &&
            solutionComponentsNode is YamlMappingNode solutionComponentsMapping &&
            TryGetMappingValue(solutionComponentsMapping, "Component", out var componentNode) &&
            componentNode is not null)
        {
            foreach (var entryNode in EnumerateComponentEntries(componentNode))
            {
                string? path = null;
                if (entryNode is YamlMappingNode entryMapping &&
                    TryGetMappingValue(entryMapping, "@path", out var pathNode) &&
                    pathNode is YamlScalarNode pathScalar)
                {
                    path = pathScalar.Value;
                }

                paths.Add(path);
            }
        }

        return new ParsedManifest(IsLegacyShape: false, Paths: paths);
    }

    /// <summary>
    /// Component holds either a single entry (a mapping) or a list of entries
    /// (a sequence of mappings); pac's parser accepts both.
    /// </summary>
    private static IEnumerable<YamlNode> EnumerateComponentEntries(YamlNode componentNode)
    {
        if (componentNode is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
                yield return item;
        }
        else
        {
            yield return componentNode;
        }
    }

    private static bool TryGetMappingValue(YamlMappingNode mapping, string key, out YamlNode? value)
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalarKey && scalarKey.Value == key)
            {
                value = child.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string RelativePath(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Pass(GateContext context, string artifact, string evidence) => new()
    {
        GateId = "G9",
        GateName = "solution-component-paths",
        Outcome = GateOutcome.Pass,
        Artifact = artifact,
        Evidence = evidence,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Refuse(GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G9",
        GateName = "solution-component-paths",
        Outcome = GateOutcome.Refuse,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };

    private sealed record ParsedManifest(bool IsLegacyShape, List<string?> Paths);
}
