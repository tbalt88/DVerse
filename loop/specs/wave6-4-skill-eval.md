# Slice spec: wave 6.4, eval the evolved skill (blind transfer test)

Persisted before spawn. Executor: Sonnet, NO worktree; read-only eval against main's `plugins/dv-architect/**` ONLY.

## Design

The eval tests whether the skill TRANSFERS the house knowledge to an agent that has never seen this repo's history. The executor is booted with the skill directory alone and answers ten scenarios. It is FORBIDDEN from reading loop/, docs/, harness/, demo-solution/, seed/, README.md, git log, or anything else in the repo; its only source is `plugins/dv-architect/**`. The grading seat holds the answer key (below, not shown to the executor) and scores each answer against the gate/lesson ground truth.

## Scenarios given to the executor

1. You authored a new 1:N relationship from your entity to `sharepointdocumentlocation`, but reversed: the entity is the referencing side. Pack exits 0. Ship it?
2. Your FormXml control has `'@datafieldname': dv_CaseNumber` for an attribute whose LogicalName is `dv_casenumber`. Pack, import, and publish all exit 0. What happens in the running app and what do you do?
3. `pac solution pack` exits 0 on your new component type. What, if anything, remains unproven?
4. You are adding the first PluginAssembly to a solution as declarative source. Walk the pitfalls in order.
5. You imported a solution carrying a plugin step; import and publish exited 0. Is the plugin running?
6. A canvas `.pa.yaml` Properties entry reads `Width: 640` (no `=`). What happens and what catches it?
7. Where do test projects for a plugin assembly live relative to the plugin project, and why?
8. Your strong-name key file: gitignore it or commit it, and why?
9. Microsoft's docs describe a YAML manifest shape that disagrees with what pac produces. Which wins and what is the procedure?
10. Name the rungs of the verification ladder in order and state what each rung alone proves.

## Answer key (seat-held ground truth; executor never sees this)

1. NO. G4 refuses; the inverted direction silently empties the Documents tab; pack exit 0 proves nothing (G4, L2/L3, refusal-pair receipt).
2. The control DROPS SILENTLY at render; only lowercase logical names bind (L14). Fix casing, re-import, and DRIVE THE RENDERED FORM (L8).
3. Presence in the packed customizations.xml (L2), import acceptance (L3), and rendered/runtime behavior (L8) all remain unproven.
4. Strong-name the assembly and COMMIT the snk (L15, L9); RootComponent schemaName must be the assembly FullName; the DLL part URI needs a leading slash; element sets must match the platform's canonical export, obtained by platform-mirror (register once, clone back, transcribe), never docs (L15, L4); expect sequential import rungs.
5. UNKNOWN until probed. `pac solution import` leaves steps DISABLED without `--activate-plugins`; a negative-input probe after import is mandatory (L17).
6. It is not a formula and the control property misbehaves/drops silently; G11 refuses it mechanically at gate time.
7. SIBLING project, never nested: the plugin csproj's recursive glob would compile test sources into the plugin assembly (L9).
8. COMMIT it. The platform requires strong-named sandbox assemblies; gitignoring the key makes fresh clones unbuildable, the seed repo's own defect (L15, L9).
9. The TOOLING (and the platform's own export) wins. Procedure: decompile SolutionPackagerLib for what pac reads; platform-mirror for what import demands (L4, L15).
10. Gates refuse offline (mechanical floor) -> pack exit 0 (pack-parseable only) -> component present in packed customizations.xml (actually in the zip) -> import (platform accepts) -> publish + rendered UI / runtime probe (it actually behaves). Each rung proves only itself (L2, L3, L8, L17).

## Scoring

Per scenario: 2 = correct substance AND cites the skill's own gate/lesson reference; 1 = correct substance, no citation; 0 = wrong or missing the silent-failure point. Pass bar: 16/20 with zero 0-scores on scenarios 1, 2, 5 (the silent-failure flagships).

## Done means

Seat writes `docs/evals/wave6-skill-eval.md`: scenarios, the executor's verbatim answers, per-scenario scores with justification, total, verdict, and any skill gaps found (gaps become amendments to the skill in the same wave).
