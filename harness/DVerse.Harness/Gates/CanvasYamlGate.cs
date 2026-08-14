using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G11: <c>.pa.yaml</c> canvas app source format conformance.
/// <para>
/// Wave 5.1 landed a real canvas app in source
/// (<c>demo-solution/canvasapps/MatterCanvas/Src/*.pa.yaml</c>), produced by
/// <c>pac canvas download</c> + <c>pac canvas unpack --layout SourceCode</c>.
/// The mission statement names <c>.pa.yaml</c> as a gateable artifact class,
/// but nothing checked it: <c>pac canvas validate</c> is listed by
/// <c>pac canvas help</c> yet is NO LONGER SUPPORTED in pac 2.10.1 (verified
/// live this wave, the same docs-contradict-tooling class as lesson 4), so
/// validation has to be ours.
/// </para>
/// <para>
/// SCOPE, deliberately narrow for a first gate over a Preview surface whose
/// format may still shift (ROADMAP risk R1, hence this gate's own isolated
/// file). G11 discovers every <c>*.pa.yaml</c> under
/// <c>&lt;solution&gt;/canvasapps/**</c> and refuses on: (a) YAML that does
/// not parse; (b) a <c>Screens:</c> document whose control entries (items of
/// a <c>Children:</c> sequence, at any nesting depth) lack a <c>Control:</c>
/// declaration; (c) any <c>Properties:</c> entry, anywhere in the document,
/// whose value does not begin with <c>=</c> (Power Fx formulas in this format
/// are always <c>=</c>-prefixed; a bare value is the silent-drop class); (d)
/// an empty <c>.pa.yaml</c> file. Everything else passes. Zero canvas files
/// (no <c>canvasapps/</c> folder, or an empty one) is a genuine Pass with
/// "no canvas sources" evidence: this gate must not force canvas onto
/// solutions that have none.
/// </para>
/// <para>
/// AUTHORITY for the format: the real committed sources in
/// <c>demo-solution/canvasapps/</c> first, Microsoft's published schema
/// (linked from every unpacked file's own header,
/// https://go.microsoft.com/fwlink/?linkid=2304907) second, docs prose never.
/// One disagreement surfaced directly: the real <c>Screen1.pa.yaml</c>'s
/// top-level <c>Screen1:</c> entry under <c>Screens:</c> carries no
/// <c>Control:</c> key of its own, only a <c>Children:</c> list, even though
/// the schema shows every node with a <c>Control:</c> field. Per the frozen
/// ruling the real file wins, so rule (b) above is scoped to items inside a
/// <c>Children:</c> sequence, never to the screen-level entries directly
/// under <c>Screens:</c>: those name the screen, they are not themselves a
/// placed control.
/// </para>
/// </summary>
public sealed class CanvasYamlGate : IGate
{
    private const string CanvasAppsFolder = "canvasapps";
    private const string PropertiesKey = "Properties";
    private const string ScreensKey = "Screens";
    private const string ChildrenKey = "Children";
    private const string ControlKey = "Control";

    public string Id => "G11";
    public string Name => "canvas-yaml";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var canvasRoot = Path.Combine(context.SolutionRoot, CanvasAppsFolder);

        if (!Directory.Exists(canvasRoot))
        {
            yield return Pass(
                context,
                RelativeArtifact(context, canvasRoot),
                $"No {CanvasAppsFolder}/ directory present under the solution root; " +
                "no canvas sources to check.");
            yield break;
        }

        var files = Directory
            .EnumerateFiles(canvasRoot, "*.pa.yaml", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            yield return Pass(
                context,
                RelativeArtifact(context, canvasRoot),
                $"{CanvasAppsFolder}/ exists but contains zero *.pa.yaml files; " +
                "no canvas sources to check.");
            yield break;
        }

        var refused = false;
        var totalControls = 0;

        foreach (var file in files)
        {
            var relative = RelativeArtifact(context, file);
            var raw = File.ReadAllText(file);

            // Rule (d): an empty .pa.yaml carries no app definition at all.
            if (string.IsNullOrWhiteSpace(raw))
            {
                refused = true;
                yield return Refuse(
                    context,
                    relative,
                    $"Read {relative}; the file is empty.",
                    $"{relative} is an empty .pa.yaml file. An empty canvas source carries no app " +
                    "definition; this is refused rather than silently treated as a valid, empty " +
                    "control tree.");
                continue;
            }

            var (yamlStream, parseError) = TryParse(raw);

            if (parseError is not null)
            {
                // Rule (a): unparseable YAML.
                refused = true;
                yield return Refuse(
                    context,
                    relative,
                    $"Attempted to parse {relative} as YAML.",
                    $"{relative} does not parse as YAML: {parseError}");
                continue;
            }

            if (yamlStream is null ||
                yamlStream.Documents.Count == 0 ||
                yamlStream.Documents[0].RootNode is not YamlMappingNode rootMapping)
            {
                // Parses, but carries no mapping document (for example a file that is
                // only the standard header comment). Nothing to check structurally,
                // and nothing named by scope forbids it, so it neither refuses nor
                // contributes files/controls to the Pass evidence.
                continue;
            }

            var fileRefused = false;

            // Rule (c): every Properties: entry's value must be a '='-prefixed formula.
            foreach (var violation in FindNonFormulaProperties(rootMapping, string.Empty))
            {
                refused = true;
                fileRefused = true;
                yield return Refuse(
                    context,
                    relative,
                    $"Inspected Properties entry '{violation.Path}' in {relative}: value " +
                    $"'{violation.Value}'.",
                    $"{relative}: Properties entry '{violation.Path}' has value '{violation.Value}', " +
                    "which does not begin with '='. Power Fx formulas in this format are always " +
                    "'='-prefixed; a bare value silently drops instead of evaluating as a formula.");
            }

            // Rule (b): a Screens: document's control entries must declare Control:.
            if (TryGetMappingValue(rootMapping, ScreensKey, out var screensNode) &&
                screensNode is YamlMappingNode screensMapping)
            {
                var counter = new ControlCounter();

                foreach (var screenPair in screensMapping.Children)
                {
                    var screenName = (screenPair.Key as YamlScalarNode)?.Value ?? "(unnamed screen)";

                    if (screenPair.Value is not YamlMappingNode screenMapping)
                        continue;

                    foreach (var missing in FindControlEntriesMissingControl(screenMapping, screenName, counter))
                    {
                        refused = true;
                        fileRefused = true;
                        yield return Refuse(
                            context,
                            relative,
                            $"Inspected control entry '{missing}' under Screens in {relative}.",
                            $"{relative}: control entry '{missing}' has no Control: declaration. " +
                            "Every control placed under a screen's Children must declare which " +
                            "control type it is.");
                    }
                }

                totalControls += counter.Total;
            }

            if (!fileRefused)
                continue; // nothing else to record per file; the aggregate Pass below lists it.
        }

        if (refused)
            yield break;

        var fileList = string.Join(", ", files.Select(f => RelativeArtifact(context, f)));

        yield return Pass(
            context,
            RelativeArtifact(context, canvasRoot),
            $"Inspected {files.Count} .pa.yaml file(s) under {CanvasAppsFolder}/**: {fileList}; " +
            $"{totalControls} control(s) declared across all Screens documents; all parse, all " +
            "Properties entries are '='-prefixed formulas, all control entries declare Control:.");
    }

    /// <summary>
    /// Parses <paramref name="raw"/> as YAML. Null stream with a non-null error means
    /// unparseable (rule (a)); a non-null stream means it parsed, whatever shape it
    /// turned out to hold. Pulled out of the main loop because an iterator method
    /// cannot <c>yield</c> from inside a <c>catch</c> block.
    /// </summary>
    private static (YamlStream? Stream, string? Error) TryParse(string raw)
    {
        try
        {
            using var reader = new StringReader(raw);
            var yamlStream = new YamlStream();
            yamlStream.Load(reader);
            return (yamlStream, null);
        }
        catch (YamlException ex)
        {
            return (null, $"{ex.Message} ({ex.Start})");
        }
    }

    /// <summary>
    /// Walks a screen's (or a control's) <c>Children:</c> sequence, recursing into
    /// each control's own <c>Children:</c> at any depth. Every mutable count of
    /// controls seen lives on <paramref name="counter"/> because an iterator method
    /// cannot take a <c>ref</c> parameter.
    /// </summary>
    private static IEnumerable<string> FindControlEntriesMissingControl(
        YamlMappingNode containerMapping, string parentLabel, ControlCounter counter)
    {
        if (!TryGetMappingValue(containerMapping, ChildrenKey, out var childrenNode) ||
            childrenNode is not YamlSequenceNode childrenSequence)
        {
            yield break;
        }

        foreach (var item in childrenSequence.Children)
        {
            if (item is not YamlMappingNode itemMapping || itemMapping.Children.Count != 1)
            {
                // Not the single-key "- ControlName: {...}" shape every real control
                // entry uses. Fail closed: count it and report it, rather than
                // silently skipping something that cannot possibly carry Control:.
                counter.Total++;
                yield return $"{parentLabel}.(malformed entry)";
                continue;
            }

            var pair = itemMapping.Children.First();
            var controlName = (pair.Key as YamlScalarNode)?.Value ?? "(unnamed control)";
            var label = $"{parentLabel}.{controlName}";

            counter.Total++;

            if (pair.Value is not YamlMappingNode controlMapping)
            {
                yield return label;
                continue;
            }

            if (!TryGetMappingValue(controlMapping, ControlKey, out _))
                yield return label;

            foreach (var nested in FindControlEntriesMissingControl(controlMapping, label, counter))
                yield return nested;
        }
    }

    /// <summary>
    /// Walks the whole document tree for <c>Properties:</c> mappings, wherever they
    /// appear (under <c>App:</c>, a screen, or any nested control), and reports every
    /// entry whose value is not a '='-prefixed scalar.
    /// </summary>
    private static IEnumerable<PropertyViolation> FindNonFormulaProperties(YamlNode node, string pathLabel)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var pair in mapping.Children)
                {
                    var keyName = (pair.Key as YamlScalarNode)?.Value;

                    if (keyName == PropertiesKey && pair.Value is YamlMappingNode propsMapping)
                    {
                        foreach (var propPair in propsMapping.Children)
                        {
                            var propName = (propPair.Key as YamlScalarNode)?.Value ?? "(unnamed property)";
                            var propPath = string.IsNullOrEmpty(pathLabel) ? propName : $"{pathLabel}.{propName}";

                            if (propPair.Value is YamlScalarNode scalar &&
                                scalar.Value is not null &&
                                scalar.Value.StartsWith('='))
                            {
                                continue;
                            }

                            var displayValue = propPair.Value is YamlScalarNode nonFormula
                                ? nonFormula.Value ?? "(null)"
                                : "(non-scalar)";

                            yield return new PropertyViolation(propPath, displayValue);
                        }

                        continue;
                    }

                    var nextLabel = string.IsNullOrEmpty(keyName)
                        ? pathLabel
                        : string.IsNullOrEmpty(pathLabel) ? keyName : $"{pathLabel}.{keyName}";

                    foreach (var violation in FindNonFormulaProperties(pair.Value, nextLabel))
                        yield return violation;
                }

                break;

            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                {
                    foreach (var violation in FindNonFormulaProperties(item, pathLabel))
                        yield return violation;
                }

                break;
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

    /// <summary>
    /// Repo-relative form of an absolute path, per the frozen rule that every
    /// Artifact is expressed relative to <see cref="GateContext.RepositoryRoot"/>,
    /// never the solution root, so the ledger uses one path base across every gate
    /// and stays reproducible across machines.
    /// </summary>
    private static string RelativeArtifact(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Pass(GateContext context, string artifact, string evidence) => new()
    {
        GateId = "G11",
        GateName = "canvas-yaml",
        Outcome = GateOutcome.Pass,
        Artifact = artifact,
        Evidence = evidence,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Refuse(
        GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G11",
        GateName = "canvas-yaml",
        Outcome = GateOutcome.Refuse,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };

    private sealed class ControlCounter
    {
        public int Total;
    }

    private sealed record PropertyViolation(string Path, string Value);
}
