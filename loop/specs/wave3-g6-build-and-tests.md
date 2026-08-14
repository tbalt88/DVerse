# Slice spec: wave 3, gate G6 build and unit tests

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-g6`, branch `slice/g6`.

## Frozen rulings

1. G6 discovers `*.csproj` under the solution root recursively and shells `dotnet test` per project (which builds first). Build failure or any failed test is a Refuse per project, Reason carrying the tail of the real output. All green is a Pass with counts. Zero projects is a Pass with evidence saying so; the demo solution has no plugin projects until wave 4.4 and the gate must be honest about vacuity.
2. Skipped tests are counted and named in Evidence. Per house law, tests that skip are not tests that pass; a nonzero skip count on an otherwise green project is a Refuse whose Reason says exactly that.
3. Process execution separated from result parsing: a pure, unit-testable method parses `dotnet test` output; unit tests cover it with synthetic output strings. Fixture-driven integration tests then run the REAL dotnet against two tiny real csproj fixtures: one passing (one trivial xunit test), one failing (one deliberately failing test). Give the fixture projects their own minimal .csproj targeting net10.0; keep them tiny so suite time stays sane. Generous process timeouts; a hung dotnet is a Refuse via the runner's fail-closed conversion (throw with a descriptive message).
4. Fixture csproj files must be excluded from the harness solution build (they live under harness/fixtures/, which the .slnx does not glob; verify rather than assume, and report what you verified).
5. `RequiresTenant => false`. Id `G6`, name `build-and-tests`. No em dashes. Artifact repo-root-relative.

## Owned files

- NEW harness/DVerse.Harness/Gates/BuildAndTestsGate.cs
- NEW harness/DVerse.Harness.Tests/Gates/BuildAndTestsGateTests.cs
- NEW harness/fixtures/g6/pass/**, harness/fixtures/g6/refuse-failing-test/**

Forbidden: everything else, including GateRegistry.cs, WaveOneIntegrationTests.cs, demo-solution.

## Definition of done

Red fixture refuses for the stated reason only. Full suite green from the worktree (baseline 96 plus additions, zero skips). Committed "Slice G6:", DDomingo author flags. Report: files, rule in one sentence, the slnx-exclusion verification, verbatim test output, commit hash, assumptions.
