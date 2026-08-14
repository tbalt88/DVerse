# Slice spec: wave 5.4, the canvas gating module (G11)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-5-4`, branch `slice/5.4`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING. Lessons 4 and 6 are this slice's daily bread.

## Context

Wave 5.1 landed a real canvas app in source: `demo-solution/canvasapps/MatterCanvas/` (Src/App.pa.yaml, Src/Screen1.pa.yaml, Src/_EditorState.pa.yaml, MatterCanvas.msapr), produced by `pac canvas download` + `pac canvas unpack --layout SourceCode` from the published Matter Canvas app, round-trip proven with `pac canvas pack`. The mission statement names `.pa.yaml` as a gateable artifact class; no gate covers it. Notably, `pac canvas validate` is NO LONGER SUPPORTED in pac 2.10.1 while `pac canvas help` still lists it (lesson 4's docs-contradict-tooling class, verified live this wave), so validation must be ours.

## Frozen rulings

1. NEW GATE G11 `canvas-yaml`, in its OWN isolated area: `harness/DVerse.Harness/Gates/CanvasYamlGate.cs` plus, if you need shared helpers, a `Canvas/` subfolder. The canvas tooling surface is Preview and shifts; the blast radius of a format change must stay inside this gate (ROADMAP risk R1). Do not touch any existing gate.
2. SCOPE, deliberately narrow for a first gate over a Preview surface. G11 discovers every `*.pa.yaml` under `<solution>/canvasapps/**` and refuses on: (a) YAML that does not parse; (b) a `Screens:` document whose control entries lack a `Control:` declaration; (c) any `Properties:` entry whose value does not begin with `=` (Power Fx formulas are `=`-prefixed in this format; a bare value is the silent-drop class); (d) empty `.pa.yaml` files. Everything else passes with Evidence listing files and control counts. Zero canvas files at all = Pass with "no canvas sources" evidence (the demo-solution gate run must not force canvas onto solutions that have none).
3. AUTHORITY for the format: the REAL committed sources in demo-solution/canvasapps/ first, and Microsoft's published schema (the header of each unpacked .pa.yaml links https://go.microsoft.com/fwlink/?linkid=2304907) second, docs prose never. Where the real file and the schema disagree, the real file wins and the WHY comment records the disagreement.
4. Gate contract as everywhere: IGate, Evidence mandatory on ALL verdicts, Reason on Refuse/Skip, fail closed (throw becomes Refuse), repo-root-relative forward-slash Artifact paths, registered in the runner's offline set, discovered by the integration sweep.
5. FIXTURES: green fixture mirroring the real MatterCanvas shape (trimmed), red fixtures for each refuse case (at minimum: unparseable YAML, missing Control declaration, non-formula property value). Mutation-check per lesson 6: revert the gate's refuse logic for one case, confirm the fixture test goes red, restore.
6. VERIFY: full suite green (baseline 159 plus your new tests, zero skips), full gate run over demo-solution exits 0 with G11 PASS visible over the real canvas sources, pack still exits 0 (you change nothing in demo-solution, so this is a sanity re-run).
7. No em dashes anywhere you write.

## Owned files
- harness/DVerse.Harness/Gates/CanvasYamlGate.cs (new, plus optional Canvas/ helpers)
- harness/DVerse.Harness/ (ONLY the runner/catalogue registration line for G11)
- harness/DVerse.Harness.Tests/** (G11 tests)
- harness/fixtures/g11/** (new fixtures)

Forbidden: everything else. demo-solution/** is READ-ONLY (it is your ground truth, not your canvas). All other gates, workflows, docs read-only. Org access not needed; pac not needed.

## Done means
Committed "Slice 5.4:" with DDomingo author flags. Report: gate behavior table (check, verdict, evidence shape), mutation-check evidence, verbatim suite + gate run outputs showing G11 over the real solution, fixture inventory, commit hash, assumptions.
