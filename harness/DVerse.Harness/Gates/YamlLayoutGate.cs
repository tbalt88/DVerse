namespace DVerse.Harness.Gates;

/// <summary>
/// G10: YAML manifest layout conformance.
/// <para>
/// Microsoft's SolutionPackager auto-detects the YAML format solely by looking
/// for <c>solutions/&lt;name&gt;/solution.yml</c>. Every manifest file
/// (<c>solution.yml</c>, <c>solutioncomponents.yml</c>, <c>rootcomponents.yml</c>,
/// <c>missingdependencies.yml</c>, <c>publisher.yml</c>) belongs one directory
/// deeper than a naive author expects: under
/// <c>solutions/&lt;SolutionUniqueName&gt;/</c> or
/// <c>publishers/&lt;PublisherUniqueName&gt;/</c>, never at the solution root.
/// </para>
/// <para>
/// A manifest left at the root is invisible to that detection. The packer then
/// falls back to the legacy XML format and fails with an error about a missing
/// <c>Other\Customizations.xml</c> — a symptom that points a developer at
/// entirely the wrong file. This gate exists to catch the real cause (a
/// misplaced or absent YAML manifest) and say so plainly, before that
/// misleading error ever has a chance to fire.
/// </para>
/// </summary>
public sealed class YamlLayoutGate : IGate
{
    public string Id => "G10";

    public string Name => "yaml-layout";

    public bool RequiresTenant => false;

    /// <summary>
    /// Manifest file names that must never sit directly at the solution root.
    /// Every one of these belongs under solutions/&lt;name&gt;/ or
    /// publishers/&lt;name&gt;/ instead.
    /// </summary>
    private static readonly string[] ManifestFileNames =
    [
        "solution.yml",
        "solutioncomponents.yml",
        "rootcomponents.yml",
        "missingdependencies.yml",
        "publisher.yml"
    ];

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var root = context.SolutionRoot;
        var verdicts = new List<GateVerdict>();

        var strayAtRoot = ManifestFileNames
            .Where(name => File.Exists(Path.Combine(root, name)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var solutionsDir = Path.Combine(root, "solutions");
        var solutionSubdirs = Directory.Exists(solutionsDir)
            ? Directory.GetDirectories(solutionsDir).OrderBy(d => d, StringComparer.Ordinal).ToList()
            : [];
        var solutionsWithManifest = solutionSubdirs
            .Where(d => File.Exists(Path.Combine(d, "solution.yml")))
            .ToList();

        // Rule 1: solutions/ must exist and contain at least one solution.yml.
        // This is where the documented trap actually bites, so when the cause
        // is visible (a manifest sitting at root) name it here explicitly.
        if (solutionsWithManifest.Count == 0)
        {
            var strayNote = strayAtRoot.Count > 0
                ? $" Found {string.Join(", ", strayAtRoot)} sitting directly at the solution root " +
                  "instead of under solutions/<name>/ — that is exactly what causes this."
                : string.Empty;

            verdicts.Add(new GateVerdict
            {
                GateId = Id,
                GateName = Name,
                Outcome = GateOutcome.Refuse,
                Artifact = RelativeArtifact(context, solutionsDir),
                Evidence = Directory.Exists(solutionsDir)
                    ? $"Inspected solutions/ for */solution.yml; found {solutionSubdirs.Count} " +
                      "subdirectory(ies), none containing solution.yml."
                    : "Inspected the solution root for a solutions/ subdirectory; none exists.",
                Reason = "No solutions/<name>/solution.yml found." + strayNote +
                    " Per Microsoft's format auto-detection, with no solutions/*/solution.yml present " +
                    "the packer silently falls back to the legacy XML format and expects " +
                    "Other\\Customizations.xml — producing a misleading error about a missing " +
                    "Customizations.xml whose real cause is the missing or misplaced YAML manifest, " +
                    "not a missing XML file.",
                At = context.Now,
                Stage = context.Stage
            });
        }
        else if (solutionsWithManifest.Count > 1)
        {
            var names = string.Join(", ", solutionsWithManifest.Select(Path.GetFileName));

            verdicts.Add(new GateVerdict
            {
                GateId = Id,
                GateName = Name,
                Outcome = GateOutcome.Pass,
                Artifact = RelativeArtifact(context, solutionsDir),
                Evidence = $"Found {solutionsWithManifest.Count} solution subdirectories under " +
                    $"solutions/ each containing solution.yml: {names}. Multiple solutions is a legal " +
                    "layout, but packing will require an explicit solution name rather than relying " +
                    "on auto-detection.",
                At = context.Now,
                Stage = context.Stage
            });
        }

        // Rule 2: no manifest file may sit at the solution root, independent of
        // whether solutions/ happens to also be laid out correctly elsewhere.
        foreach (var strayFile in strayAtRoot)
        {
            verdicts.Add(new GateVerdict
            {
                GateId = Id,
                GateName = Name,
                Outcome = GateOutcome.Refuse,
                Artifact = RelativeArtifact(context, Path.Combine(root, strayFile)),
                Evidence = $"Found {strayFile} at the solution root, outside solutions/<name>/ " +
                    "or publishers/<name>/.",
                Reason = $"{strayFile} must live under its subdirectory (solutions/<name>/ or " +
                    "publishers/<name>/), not at the solution root. YAML format detection only looks " +
                    "for solutions/*/solution.yml, so a manifest left at root is invisible to it, which " +
                    "causes SolutionPackager to fall back to the XML format and fail with a misleading " +
                    "error about a missing Customizations.xml.",
                At = context.Now,
                Stage = context.Stage
            });
        }

        // Rule 3: publishers/ must exist and contain at least one publisher.yml.
        var publishersDir = Path.Combine(root, "publishers");
        var publisherSubdirs = Directory.Exists(publishersDir)
            ? Directory.GetDirectories(publishersDir).OrderBy(d => d, StringComparer.Ordinal).ToList()
            : [];
        var publishersWithManifest = publisherSubdirs
            .Where(d => File.Exists(Path.Combine(d, "publisher.yml")))
            .ToList();

        if (publishersWithManifest.Count == 0)
        {
            verdicts.Add(new GateVerdict
            {
                GateId = Id,
                GateName = Name,
                Outcome = GateOutcome.Refuse,
                Artifact = RelativeArtifact(context, publishersDir),
                Evidence = Directory.Exists(publishersDir)
                    ? $"Inspected publishers/ for */publisher.yml; found {publisherSubdirs.Count} " +
                      "subdirectory(ies), none containing publisher.yml."
                    : "Inspected the solution root for a publishers/ subdirectory; none exists.",
                Reason = "No publishers/<name>/publisher.yml found. A YAML solution requires a " +
                    "publisher manifest under publishers/<name>/.",
                At = context.Now,
                Stage = context.Stage
            });
        }

        // Fully conforming, single-solution layout: exactly one Pass for the
        // whole gate, per the "one verdict per violation, one Pass if
        // conforming" rule.
        if (verdicts.Count == 0)
        {
            var solutionName = Path.GetFileName(solutionsWithManifest[0]);
            var publisherName = Path.GetFileName(publishersWithManifest[0]);

            verdicts.Add(new GateVerdict
            {
                GateId = Id,
                GateName = Name,
                Outcome = GateOutcome.Pass,
                Artifact = RelativeArtifact(context, solutionsDir),
                Evidence = $"Found solutions/{solutionName}/solution.yml and " +
                    $"publishers/{publisherName}/publisher.yml; no stray manifests " +
                    "(solution.yml, solutioncomponents.yml, rootcomponents.yml, " +
                    "missingdependencies.yml, publisher.yml) at the solution root.",
                At = context.Now,
                Stage = context.Stage
            });
        }

        return verdicts;
    }

    /// <summary>
    /// Repo-relative form of an absolute path under <see cref="GateContext.RepositoryRoot"/>,
    /// with forward slashes so the ledger stays reproducible across machines and
    /// never carries an absolute path onto a public repo.
    /// </summary>
    private static string RelativeArtifact(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');
}
