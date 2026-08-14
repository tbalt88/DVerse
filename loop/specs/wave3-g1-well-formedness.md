# Slice spec: wave 3, gate G1 well-formedness

Persisted before spawn per L1 entry gate (HFLA spec-before-spawn, M2). Executor: Sonnet, worktree `.worktrees/slice-g1`, branch `slice/g1`.

## Frozen rulings

1. G1 parses every `*.yml`, `*.yaml`, and `*.xml` file under the solution root, recursively. YAML via YamlDotNet `YamlStream`, XML via `XDocument`.
2. One Refuse per unparseable file, Reason carrying the parser's line/column detail verbatim. Files that parse contribute to one Pass verdict whose Evidence states counts by extension.
3. Zero candidate files is a Pass with evidence saying so, not a refusal; structural absence is G10's territory, not G1's.
4. `RequiresTenant => false`. Id `G1`, name `well-formedness`.
5. No em dashes anywhere. Artifact paths repo-root-relative, forward slashes, per the frozen contract.

## Owned files

- NEW harness/DVerse.Harness/Gates/WellFormednessGate.cs
- NEW harness/DVerse.Harness.Tests/Gates/WellFormednessGateTests.cs
- NEW harness/fixtures/g1/pass/** , harness/fixtures/g1/refuse-bad-yaml/** , harness/fixtures/g1/refuse-bad-xml/**

Forbidden: everything else, including GateRegistry.cs and WaveOneIntegrationTests.cs (seat wires those at merge).

## Definition of done

Red fixtures refuse for the stated reason and no other. Full suite green from the worktree (baseline 96 plus additions, zero skips). Committed with message starting "Slice G1:", author DDomingo flags. Report: files, rule in one sentence, verbatim test output, commit hash, assumptions.
