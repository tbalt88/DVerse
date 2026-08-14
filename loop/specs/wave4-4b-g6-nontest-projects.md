# Slice spec: wave 4.4b-pre, G6 learns non-test projects

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-g6b`, branch `slice/g6b`.

## Context

Slice 4.4a added the first real plugin assembly and G6 refused it: `dotnet test` against a plain class library emits no VSTest summary, and G6 treats "no summary" as unconditionally equivalent to a build failure. The 4.4a agent proved this empirically three ways, refused to game the parser, and escalated. The mandated layout (tests in a SIBLING project, never nested) means non-test projects are now a permanent, correct part of the solution.

## Frozen rulings (the lead decision 4.4a asked for)

1. G6 classifies each discovered csproj FIRST, via `dotnet msbuild <proj> -getProperty:IsTestProject` (the Test SDK sets this property; a plain library reports false or empty). Classification failure (nonzero exit, unparseable output) is a Refuse, fail closed.
2. Test projects (IsTestProject true): behavior unchanged, `dotnet test`, summary line required, failures and skips refuse exactly as today.
3. Non-test projects: `dotnet build`, Refuse on nonzero exit with the sanitized tail, otherwise Pass with Evidence stating "built clean; not a test project (IsTestProject false); its tests live in sibling projects". No summary expected, none required.
4. The existing SanitizePaths guard applies to any tail G6 emits, unchanged.
5. New unit tests for the classification and both branches (synthetic runner results, no real dotnet in unit tests, per the existing separation). Extend the g6 fixture family with `pass-library/` containing a tiny non-test classlib that must Pass, and keep every existing fixture's semantic intact. The existing `BuildVerdict_no_summary_refuses_with_tail_in_reason` behavior narrows to test-classified projects; adjust it accordingly and say so.
6. PROOF: from the worktree, the real CLI over demo-solution must show G6 emitting a Pass for `DVerse.Plugins.csproj` (non-test) AND a Pass for `DVerse.Plugins.Tests.csproj` (test), overall run exit 0, all gates green. Paste verbatim.
7. Full suite green, zero skips (baseline 145 plus your additions). No em dashes anywhere you write.

## Owned files
- harness/DVerse.Harness/Gates/BuildAndTestsGate.cs
- harness/DVerse.Harness.Tests/Gates/BuildAndTestsGateTests.cs
- harness/fixtures/g6/** (additions; existing fixtures keep their semantics)

Forbidden: everything else, including all other gates, the contract files, demo-solution, WaveOneIntegrationTests.cs, GateRegistry.cs.

## Done means
Committed "Slice G6b:" with DDomingo author flags. Report: files, the classification rule in one sentence, verbatim CLI run over demo-solution showing both plugin projects passing, suite output, commit hash, assumptions.
