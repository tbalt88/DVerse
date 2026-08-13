using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DVerse.Harness.Gates;

/// <summary>
/// G2. Enforces the publisher customization prefix across a solution's YAML
/// source control export.
/// <para>
/// Two rules, both mechanical:
/// </para>
/// <list type="number">
/// <item>Every <c>publishers/*/publisher.yml</c> declares
/// <c>CustomizationPrefix: dv</c>, exactly, case sensitive, no underscore.</item>
/// <item>Every directory under <c>entities/</c> is named with the <c>dv_</c>
/// prefix.</item>
/// </list>
/// <para>
/// A wrong prefix is legal to author, imports cleanly, and only surfaces later
/// as a naming collision or an unreadable component list once several solutions
/// share an environment. One verdict is emitted per violation found; when
/// nothing violates the rule, exactly one Pass verdict is emitted.
/// </para>
/// </summary>
public sealed class PublisherPrefixGate : IGate
{
    private const string ExpectedPrefix = "dv";
    private const string ExpectedEntityPrefix = "dv_";
    private const string PublishersFolder = "publishers";
    private const string EntitiesFolder = "entities";

    public string Id => "G2";
    public string Name => "publisher-prefix";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var violations = new List<GateVerdict>();
        var evidenceParts = new List<string>();

        violations.AddRange(CheckPublishers(context, evidenceParts));
        violations.AddRange(CheckEntities(context, evidenceParts));

        if (violations.Count > 0)
        {
            foreach (var v in violations)
                yield return v;

            yield break;
        }

        yield return Verdict(
            context,
            GateOutcome.Pass,
            RelativeArtifact(context, context.SolutionRoot),
            string.Join(" ", evidenceParts));
    }

    /// <summary>Rule 1: publisher.yml under every publishers/*/ declares the expected prefix.</summary>
    private IEnumerable<GateVerdict> CheckPublishers(GateContext context, List<string> evidenceParts)
    {
        var publishersDir = Path.Combine(context.SolutionRoot, PublishersFolder);

        if (!Directory.Exists(publishersDir))
        {
            yield return Verdict(
                context,
                GateOutcome.Refuse,
                RelativeArtifact(context, publishersDir),
                $"Looked for a {PublishersFolder}/ directory under the solution root; it does not exist.",
                $"No {PublishersFolder}/ directory found under the solution root; cannot verify CustomizationPrefix.");
            yield break;
        }

        var publisherDirs = Directory
            .EnumerateDirectories(publishersDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        if (publisherDirs.Count == 0)
        {
            yield return Verdict(
                context,
                GateOutcome.Refuse,
                RelativeArtifact(context, publishersDir),
                $"Looked inside {PublishersFolder}/; it contains no publisher folders.",
                $"{PublishersFolder}/ directory contains no publisher folders; cannot verify CustomizationPrefix.");
            yield break;
        }

        var checkedCount = 0;

        foreach (var publisherDir in publisherDirs)
        {
            var publisherYamlPath = Path.Combine(publisherDir, "publisher.yml");
            var artifact = RelativeArtifact(context, publisherYamlPath);

            if (!File.Exists(publisherYamlPath))
            {
                yield return Verdict(
                    context,
                    GateOutcome.Refuse,
                    artifact,
                    $"Looked for publisher.yml at {artifact}; it does not exist.",
                    $"publisher.yml not found under {RelativeArtifact(context, publisherDir)}/.");
                continue;
            }

            string? prefix;
            string? parseError;
            try
            {
                prefix = ParsePublisher(publisherYamlPath).CustomizationPrefix;
                parseError = null;
            }
            catch (YamlException ex)
            {
                prefix = null;
                parseError = ex.Message;
            }

            if (parseError is not null)
            {
                yield return Verdict(
                    context,
                    GateOutcome.Refuse,
                    artifact,
                    $"Attempted to parse {artifact} as YAML.",
                    $"publisher.yml at {artifact} could not be parsed as YAML: {parseError}");
                continue;
            }

            checkedCount++;

            if (!string.Equals(prefix, ExpectedPrefix, StringComparison.Ordinal))
            {
                yield return Verdict(
                    context,
                    GateOutcome.Refuse,
                    artifact,
                    $"Read CustomizationPrefix from {artifact}.",
                    $"CustomizationPrefix is '{prefix ?? "<missing>"}' in {artifact}, "
                    + $"expected exactly '{ExpectedPrefix}'.");
            }
        }

        if (checkedCount > 0)
        {
            evidenceParts.Add(
                $"Checked CustomizationPrefix in {checkedCount} publisher.yml file(s) under {PublishersFolder}/.");
        }
    }

    /// <summary>Rule 2: every directory under entities/ is dv_ prefixed.</summary>
    private IEnumerable<GateVerdict> CheckEntities(GateContext context, List<string> evidenceParts)
    {
        var entitiesDir = Path.Combine(context.SolutionRoot, EntitiesFolder);

        if (!Directory.Exists(entitiesDir))
        {
            // No entities folder means no entity name to get wrong. Genuine
            // pass condition, not a skip: the rule holds vacuously.
            evidenceParts.Add(
                $"No {EntitiesFolder}/ directory present under the solution root; no entity names to check.");
            yield break;
        }

        var entityDirs = Directory
            .EnumerateDirectories(entitiesDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        foreach (var entityDir in entityDirs)
        {
            var name = Path.GetFileName(entityDir);

            if (!name.StartsWith(ExpectedEntityPrefix, StringComparison.Ordinal))
            {
                yield return Verdict(
                    context,
                    GateOutcome.Refuse,
                    RelativeArtifact(context, entityDir),
                    $"Read entity directory name '{name}' under {EntitiesFolder}/.",
                    $"Entity directory '{name}' does not start with the '{ExpectedEntityPrefix}' prefix.");
            }
        }

        evidenceParts.Add(
            $"Checked {entityDirs.Count} entity directory name(s) under {EntitiesFolder}/ for the "
            + $"'{ExpectedEntityPrefix}' prefix.");
    }

    private static PublisherSection ParsePublisher(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var text = File.ReadAllText(path);

        var doc = deserializer.Deserialize<PublisherDocument>(text)
            ?? throw new InvalidDataException($"{path}: empty publisher document.");

        return doc.Publisher ?? throw new InvalidDataException($"{path}: no Publisher node found.");
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
            GateId = "G2",
            GateName = "publisher-prefix",
            Outcome = outcome,
            Artifact = artifact,
            Evidence = evidence,
            Reason = reason,
            At = context.Now,
            Stage = context.Stage
        };

    // Serialization shape.
    //
    // HONEST LIMIT, same as G4: this shape is inferred from the documented YAML
    // source-control format, not observed from a real `pac solution clone`. No
    // Dataverse tenant exists yet. The rule is grounded in the documented
    // publisher.yml layout given in the slice spec; the exact YAML node names
    // must be confirmed against real clone output once a tenant is available.

    private sealed class PublisherDocument
    {
        public PublisherSection? Publisher { get; set; }
    }

    private sealed class PublisherSection
    {
        public string? UniqueName { get; set; }
        public string? CustomizationPrefix { get; set; }
        public string? CustomizationOptionValuePrefix { get; set; }
    }
}
