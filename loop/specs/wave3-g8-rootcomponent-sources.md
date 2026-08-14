# Slice spec: wave 3, gate G8 rootcomponent source presence

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-g8`, branch `slice/g8`. This closes standing obligation O2, open since wave 1.

## Why this gate exists

Microsoft's own docs: a component declared in `rootcomponents.yml` whose source files are absent is silently OMITTED from the pack while pack still EXITS 0. The build reports success; the shipped solution is missing a piece; the failure surfaces at import or, worse, at runtime. This is the silent-failure class the harness exists for.

## Frozen rulings

1. G8 reads `solutions/*/rootcomponents.yml`. For every declared root component whose type maps to an on-disk source location, verify the source exists; one Refuse per missing source, Reason citing the exit-0 omission behavior.
2. GROUND TRUTH BEFORE SHAPES: decompile SolutionPackagerLib (ilspycmd, slice 4.1 precedent) for how root components are read and which schema-name-to-path conventions apply. Cite class and method. Known pac-verified fact from 4.1: the EMPTY form is a mapping (`RootComponents: {}`), never a YAML list.
3. Type-to-path mapping starts minimal: Entity to `entities/<schemaname>`, CanvasApp to `canvasapps/<name>`. A declared component whose TYPE has no known mapping is NOT a refusal: count it in the Pass evidence with an explicit note naming the unmapped type. Refusing on ignorance would make the gate cry wolf; the note keeps it honest. Extend the map only for types you verified.
4. Empty mapping is a Pass. File absent or malformed is a Refuse (fail closed).
5. `RequiresTenant => false`. Id `G8`, name `rootcomponent-sources`. No em dashes. Artifact repo-root-relative.

## Owned files

- NEW harness/DVerse.Harness/Gates/RootComponentSourceGate.cs
- NEW harness/DVerse.Harness.Tests/Gates/RootComponentSourceGateTests.cs
- NEW harness/fixtures/g8/pass/**, harness/fixtures/g8/refuse-missing-entity-source/**, harness/fixtures/g8/refuse-missing-file/**

Forbidden: everything else, including GateRegistry.cs, WaveOneIntegrationTests.cs, g9 fixtures, demo-solution.

## Definition of done

Red fixtures refuse for the stated reason only. Full suite green from the worktree (baseline 96 plus additions, zero skips). Committed "Slice G8:", DDomingo author flags. Report: files, rule in one sentence, decompilation citation, verbatim test output, commit hash, assumptions.
