# Slice spec: wave 4.4b, declarative plugin registration in source

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-4-4b`, branch `slice/4.4b`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING. Lessons 2, 3, 4 are this slice's daily bread; lesson 14 is the newest member of the same class.

## Mission

The plugin (DVerse.Plugins.dll, MatterNumberValidator, net462, built and tested under G6 since 4.4a) exists only as source. Nothing registers it. This slice makes registration DECLARATIVE: the PluginAssembly and its SdkMessageProcessingStep become solution components authored in the repo, so that importing the solution registers the plugin. This unblocks G5.

## Frozen rulings

1. GROUND TRUTH BY DECOMPILE FIRST: ilspycmd against SolutionPackagerLib.dll (pac 2.10.1, precedent in every gate's WHY comments and slice 4.6t) for the PluginAssembly and SdkMessageProcessingStep processors: exact folder names, file names, YAML element shapes, and how the assembly binary itself is carried (embedded base64, sibling dll file, or path reference; do not guess). Docs alone are FORBIDDEN as authority (lesson 4).
2. AUTHOR the source: the pluginassembly component (IsolationMode sandbox, SourceType database), one SdkMessageProcessingStep on Create of dv_matter, synchronous, PreOperation, targeting MatterNumberValidator. Use the Debug-built net462 dll only as a placeholder if the format embeds the binary; state which build configuration the bytes came from.
3. MANIFESTS: rootcomponents.yml (numeric @type values, verify by decompile, do not trust memory: PluginAssembly and SdkMessageProcessingStep have distinct codes) and solutioncomponents.yml path entries for every new subfolder (lesson 2). PROVE presence of both components in the packed customizations.xml by pasting the blocks.
4. The plugin registers on IMPORT, and only the seat imports. You never write to the org. pac READ operations (clone, list) via the dverse-ci profile are allowed if you need to inspect anything live.
5. VERIFY: pack exit 0, unpack round-trip, all offline gates exit 0, full suite green (baseline 156, zero skips; O11 isolation is merged, a G6 failure is no longer the known flake and must be investigated).
6. Expect the pack-vs-import gap (lessons 3, 14): pack exit 0 proves nothing about import. List every shape decision that import could still reject, as an explicit assumptions table for the seat's import attempt.
7. No em dashes anywhere you write.

## Owned files
- demo-solution/pluginassemblies/** (or the exact folder name your decompilation finds)
- demo-solution/sdkmessageprocessingsteps/** (same caveat)
- demo-solution/solutions/DVerseCore/rootcomponents.yml and solutioncomponents.yml (entries only)

Forbidden: everything else. The plugin source itself (demo-solution/plugins/**), the harness, workflows, docs are read-only. Org writes forbidden absolutely.

## Done means
Committed "Slice 4.4b:" with DDomingo author flags. Report: decompilation citations (type names and the properties you read), files authored, verbatim pack/gate/suite outputs, packed-zip evidence blocks for BOTH components, the import-risk assumptions table, commit hash.
