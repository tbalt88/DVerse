using System.Xml;
using System.Xml.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G1: every YAML and XML file under the solution root parses.
/// <para>
/// This is the entry gate. Every other gate reads a YAML or XML artifact and
/// trusts it parses; without this gate first, a gate further down the chain
/// would report a domain violation for what is actually a syntax error, which
/// points a developer at the wrong file. G1 catches the syntax problem first
/// and says so plainly, with the line and column the parser itself reported.
/// </para>
/// <para>
/// Structural absence (a required file or folder simply missing) is not this
/// gate's concern; other gates own that. G1 only asks whether the files that
/// exist are well formed.
/// </para>
/// </summary>
public sealed class WellFormednessGate : IGate
{
    /// <summary>
    /// Candidate extensions, in the order they are reported in the Pass
    /// evidence summary.
    /// </summary>
    private static readonly string[] CandidateExtensions = [".yml", ".yaml", ".xml"];

    public string Id => "G1";
    public string Name => "well-formedness";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var root = context.SolutionRoot;

        var files = CandidateExtensions
            .SelectMany(ext => Directory.EnumerateFiles(root, "*" + ext, SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            yield return Verdict(
                context,
                GateOutcome.Pass,
                RelativeArtifact(context, root),
                "Found 0 candidate file(s) (*.yml, *.yaml, *.xml) under the solution root; "
                + "nothing to parse.");
            yield break;
        }

        var parsedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relative = RelativeArtifact(context, file);
            var extension = Path.GetExtension(file).ToLowerInvariant();
            var isXml = extension == ".xml";

            var error = isXml ? TryParseXml(file) : TryParseYaml(file);

            if (error is null)
            {
                parsedCounts[extension] = parsedCounts.GetValueOrDefault(extension) + 1;
                continue;
            }

            yield return Verdict(
                context,
                GateOutcome.Refuse,
                relative,
                $"Attempted to parse {relative} as {(isXml ? "XML" : "YAML")}.",
                error);
        }

        var parsedTotal = parsedCounts.Values.Sum();

        if (parsedTotal > 0)
        {
            var byExtension = string.Join(", ", CandidateExtensions
                .Where(ext => parsedCounts.GetValueOrDefault(ext) > 0)
                .Select(ext => $"{parsedCounts[ext]} {ext}"));

            yield return Verdict(
                context,
                GateOutcome.Pass,
                RelativeArtifact(context, root),
                $"Parsed {parsedTotal} file(s) under the solution root ({byExtension}); "
                + "all well formed.");
        }
    }

    /// <summary>
    /// Null when the file parses. Otherwise the parser's own message with the
    /// line and column mark appended, unmodified from what YamlDotNet reports.
    /// </summary>
    private static string? TryParseYaml(string file)
    {
        try
        {
            using var reader = new StreamReader(file);
            var yamlStream = new YamlStream();
            yamlStream.Load(reader);
            return null;
        }
        catch (YamlException ex)
        {
            return $"{ex.Message} ({ex.Start})";
        }
    }

    /// <summary>
    /// Null when the file parses. Otherwise XmlException's own message, which
    /// already carries the line and position it detected.
    /// </summary>
    private static string? TryParseXml(string file)
    {
        try
        {
            XDocument.Load(file);
            return null;
        }
        catch (XmlException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Repo-relative form of an absolute path, per the frozen rule that every
    /// Artifact is expressed relative to <see cref="GateContext.RepositoryRoot"/>,
    /// never the solution root, so the ledger uses one path base across every
    /// gate and stays reproducible across machines.
    /// </summary>
    private static string RelativeArtifact(GateContext context, string absolute)
    {
        var rel = Path.GetRelativePath(context.RepositoryRoot, absolute);
        return rel.Replace('\\', '/');
    }

    private static GateVerdict Verdict(
        GateContext context,
        GateOutcome outcome,
        string artifact,
        string evidence,
        string? reason = null) => new()
        {
            GateId = "G1",
            GateName = "well-formedness",
            Outcome = outcome,
            Artifact = artifact,
            Evidence = evidence,
            Reason = reason,
            At = context.Now,
            Stage = context.Stage
        };
}
