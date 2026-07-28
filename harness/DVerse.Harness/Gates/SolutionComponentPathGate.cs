using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace DVerse.Harness.Gates;

/// <summary>
/// G9: every <c>Path:</c> entry in every <c>solutions/*/solutioncomponents.yml</c>
/// must resolve to a real file or directory under the solution root.
/// <para>
/// This exists because SolutionPackager's own documented behaviour makes the
/// failure silent: if a declared component's source is absent, pack drops the
/// component, still exits 0, and the missing piece only surfaces later as an
/// import failure against a build that reported success. This gate turns that
/// exit-code-0 lie into a refusal before the pack ever runs.
/// </para>
/// </summary>
public sealed class SolutionComponentPathGate : IGate
{
    public string Id => "G9";
    public string Name => "solution-component-paths";
    public bool RequiresTenant => false;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

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

        List<string?>? paths;
        YamlException? parseError = null;
        try
        {
            paths = ReadComponentPaths(componentsFile);
        }
        catch (YamlException ex)
        {
            paths = null;
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

        if (paths is null || paths.Count == 0)
        {
            yield return Refuse(
                context,
                relComponentsFile,
                $"Parsed {relComponentsFile}; it declares zero Path entries.",
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
                    $"Read a Path entry from {relComponentsFile}; the entry is blank.",
                    "solutioncomponents.yml contains a Path entry with no value, so nothing can " +
                    "be resolved for it and the packer will silently drop that component.");
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
                $"Resolved Path '{declaredPath}' from {relComponentsFile} against disk; " +
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
                $"Resolved {paths.Count} Path " +
                $"entr{(paths.Count == 1 ? "y" : "ies")} from {relComponentsFile} against disk " +
                $"under {RelativePath(context, context.SolutionRoot)}; all exist.");
        }
    }

    private static List<string?>? ReadComponentPaths(string componentsFile)
    {
        using var reader = new StreamReader(componentsFile);
        var entries = Deserializer.Deserialize<List<ComponentEntry>?>(reader);
        return entries?.Select(e => e.Path).ToList();
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

    private sealed class ComponentEntry
    {
        public string? Path { get; set; }
    }
}
