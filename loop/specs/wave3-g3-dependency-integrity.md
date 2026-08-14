# Slice spec: wave 3, gate G3 dependency integrity

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-g3`, branch `slice/g3`.

## Frozen rulings

1. G3 reads `solutions/*/missingdependencies.yml`. A declared missing dependency is a Refuse, one per dependency, because a solution carrying declared missing dependencies fails at import on any environment that lacks them, and the platform only tells you at import time. Reason names the dependency and says exactly that.
2. Empty mapping (`MissingDependencies: {}`) is a Pass. File absent is a Refuse (fail closed: one of the four required manifests). Malformed YAML is a Refuse.
3. GROUND TRUTH BEFORE SHAPES: the non-empty element shape has never been observed. Before writing the parser, decompile pac's SolutionPackagerLib (ilspycmd is installed as a dotnet global tool; see slice 4.1's precedent) and find how MissingDependencies elements are read. Cite the class and method in a WHY comment. If decompilation is inconclusive, parse defensively for any child elements and Refuse on their presence, stating in the Reason that any declared entry is the finding regardless of its exact shape.
4. `RequiresTenant => false`. Id `G3`, name `dependency-integrity`. No em dashes. Artifact repo-root-relative.

## Owned files

- NEW harness/DVerse.Harness/Gates/DependencyIntegrityGate.cs
- NEW harness/DVerse.Harness.Tests/Gates/DependencyIntegrityGateTests.cs
- NEW harness/fixtures/g3/pass/**, harness/fixtures/g3/refuse-declared-dependency/**, harness/fixtures/g3/refuse-missing-file/**

Forbidden: everything else, including GateRegistry.cs, WaveOneIntegrationTests.cs, and demo-solution (read-only reference).

## Definition of done

Red fixtures refuse for the stated reason only. Full suite green from the worktree (baseline 96 plus additions, zero skips). Committed "Slice G3:", DDomingo author flags. Report: files, rule in one sentence, decompilation citation or the defensive fallback rationale, verbatim test output, commit hash, assumptions.
