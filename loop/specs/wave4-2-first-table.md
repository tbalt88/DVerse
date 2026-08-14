# Slice spec: wave 4.2, the first real table

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-4-2`, branch `slice/4.2`.

## Mission

Author the first real Dataverse table in the YAML source-control format inside `demo-solution/`: schema name `dv_matter`, display name "Matter", with a primary name column and two custom attributes (one string `dv_matternumber`, one datetime `dv_openedon`), plus a main form. Wire it into the solution manifests. This creates the first non-empty `rootcomponents.yml`, which empirically tests G8's inferred numeric `@type` convention (Entity=1) against real tooling.

## Frozen rulings

1. GROUND TRUTH BEFORE AUTHORING: decompile pac 2.10.1's SolutionPackagerLib (ilspycmd; three slices of precedent) for how `entities/<name>/` content is READ during pack: entity metadata file name and shape, attributes, formxml. Cite classes and methods. Author only shapes the reader demonstrably consumes. Where the reader accepts XML-as-YAML via the `@attr` convention, follow the G9/G3/G8 precedents.
2. THE JUDGE IS THE REAL TOOL: `pac solution pack --zipfile <temp> --folder demo-solution` must exit 0, and `pac solution unpack` of that zip must round-trip. Include the packed zip's inner file listing in your report. Import into the tenant is NOT yours; the seat runs it at grading.
3. `solutioncomponents.yml` gains `entities/dv_matter` (pac shape, `'@path'`). `rootcomponents.yml` gains the Entity root component with numeric `@type` 1 and `@schemaName` dv_matter, per G8's decompilation-grounded inference. If pack rejects numeric 1, THAT IS A FINDING: capture verbatim, try the alternative the decompiled reader suggests, and report the correction explicitly so G8 can be realigned.
4. All nine gates green: from the worktree, `dotnet run --project harness/DVerse.Harness.Cli -- gate run --solution demo-solution --repo . --ledger <temp>` must exit 0 with G8 resolving the new entity source. Full suite green: `dotnet test harness/DVerse.Harness.Tests/DVerse.Harness.Tests.csproj --nologo -v minimal`, baseline 145, zero skips.
5. If the main-form shape proves genuinely unresolvable from decompilation plus pack validation, deliver table plus attributes with the form omitted and report the blocker with the decompiled evidence; a partial with honest findings beats an invented FormXML. The seat splits the slice at grading if so.
6. Publisher prefix `dv_` everywhere. No em dashes anywhere you write. Match house conventions.

## Owned files

- demo-solution/entities/** (new)
- demo-solution/solutions/DVerseCore/solutioncomponents.yml and rootcomponents.yml (content edits)
- demo-solution/solutions/DVerseCore/solution.yml ONLY if pack demands a change; report why if touched

Forbidden: everything else. The harness, fixtures, workflows, docs, loop/specs are read-only.

## Definition of done

Pack exit 0 with the entity in the zip listing, round-trip clean, 9 gates exit 0, suite green at 145+, committed "Slice 4.2:" with DDomingo author flags. Report: files, decompilation citations, verbatim pack/unpack/gate/suite outputs, the G8 @type verdict (confirmed or corrected), commit hash, assumptions.
