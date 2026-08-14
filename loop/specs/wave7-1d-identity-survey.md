# Slice spec: wave 7.1d, element identity survey and model design (no code)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-7-1d`, branch `slice/7.1d`. This is a DESIGN slice: its deliverable is a document, not code. The seat ratifies the model before any implementation slice spawns.

READ loop/LESSONS.md BEFORE WRITING ANYTHING.

## Why this slice exists

ROADMAP wave 7's own warning: a diff engine that gets FormXML element identity wrong produces confident wrong answers, worse than no diff. Identity is NOT positional. Before any diff code, the project needs a ratified identity model: for every element class in every artifact type we gate, what keys an element's identity across two versions of the same artifact?

## Mission

Survey EVERY declarative artifact class in demo-solution and produce the identity model as a design doc at `docs/design/element-identity-model.md`.

## Frozen rulings

1. GROUND TRUTH IS THE REAL TREE: every artifact file under demo-solution (entity yml, attributes, FormXml yml, relationships, appmodule/sitemap, solution manifests, pluginassembly/step yml, canvas .pa.yaml). For each element class, read the REAL instances and identify the candidate identity key: a GUID attribute (formid, PluginAssemblyId), a logical name (datafieldname, attribute LogicalName), a composite, or NOTHING STABLE (state that honestly; those classes get positional-with-warning treatment, never silent positional).
2. FormXml gets the deepest treatment (it is the named risk): tabs, columns, sections, rows, cells, controls. State what the real form yml uses for ids at each level (the committed dv_matter_main.yml carries real platform-authored structure; quote it). Where the platform generates ids (cell ids, section ids), state whether they are stable across clone round-trips: you can test this empirically with two successive `pac solution clone` runs (READ operations, dverse-ci profile allowed) diffed against each other. That empirical stability check is the single most valuable fact this survey can produce; do it and paste the evidence.
3. For canvas .pa.yaml: control identity is the YAML key name (Gallery1, Form1); state the implications (rename = delete + add, which is TRUE to the platform's own semantics; verify what Studio-rename does to the source if you can reason it from the committed history of MatterCanvas, which had controls added across three commits).
4. The model must define, per class: identity key, match rule (how two elements are declared "the same element, possibly changed"), and the honest fallback where no stable key exists. Plus the three diff verdict classes any engine will emit: added, removed, changed(with property-level detail).
5. Decompile is available where the reader's behavior matters (ilspycmd, SolutionPackagerLib) but this slice is about the ARTIFACTS, not the packer; cite decompile only where it settles identity semantics.
6. No em dashes. No code. The doc is the deliverable.

## Owned files
- docs/design/element-identity-model.md (new)

Forbidden: everything else. Org writes forbidden; pac clone/list READ allowed.

## Done means
Committed "Slice 7.1d:" with DDomingo author flags. Report: the identity table (class by class), the clone-stability empirical result verbatim, the honest-fallback list, open questions for the seat's ratification, commit hash.
