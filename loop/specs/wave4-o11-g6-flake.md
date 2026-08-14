# Slice spec: wave 4 O11, kill the G6 parallel-execution flake

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-o11`, branch `slice/o11`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING. This slice closes lesson 13.

## The defect

G6's real-dotnet fixture integration tests fail roughly one full-suite run in ten, always pass in isolation, three sightings by three parties. Suspected: concurrent dotnet subprocess invocations from parallel xUnit test classes contending on shared fixture obj/bin state (multiple tests building the same fixture csproj simultaneously).

## Frozen rulings

1. DIAGNOSE before fixing: reproduce the flake if you can (repeated full-suite runs), and identify the actual contention (which tests, which shared state). If it will not reproduce in 10 runs, say so and fix the suspected mechanism anyway, labeled as such.
2. Fix mechanically, choosing the narrowest sufficient tool: an xUnit collection to serialize every test that launches a real dotnet process against shared fixtures, or per-test isolated copies of the fixture projects into unique temp dirs (which also honors lesson 1's path rules). Do NOT disable parallelism suite-wide; the other 140+ tests keep their speed.
3. PROOF: five consecutive full-suite runs (`dotnet test harness/DVerse.Harness.Tests/DVerse.Harness.Tests.csproj --nologo -v minimal`), all green, all pasted verbatim with run numbers. Zero skips throughout.
4. No em dashes anywhere you write.

## Owned files
- harness/DVerse.Harness.Tests/Gates/BuildAndTestsGateTests.cs
- harness/DVerse.Harness.Tests/ (a new collection-definition file if xUnit requires one)

Forbidden: everything else, including the gate itself, all fixtures, all other tests.

## Done means
Committed "Slice O11:" with DDomingo author flags. Report: the diagnosis (reproduced or suspected-only, stated plainly), the mechanism chosen and why it is the narrowest, five verbatim suite outputs, commit hash, assumptions.
