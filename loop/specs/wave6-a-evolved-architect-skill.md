# Slice spec: wave 6A, the evolved dv-architect skill (6.1 + 6.2 + 6.3)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-6-a`, branch `slice/6.a`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING. In this slice the lessons are not just your boot reading; they are your raw material.

## Context

The gates came first, deliberately (ROADMAP wave 6 rationale): every rule the skill states must already be mechanically checkable or empirically burned. The seed skill is committed verbatim at `seed/d365-architect/` (SKILL.md plus six ce-* references, inherited from the archived tbalt88/DVerseClaudeSkills, provenance commit 2c72b66). The repo now carries: 11 live gates (G1-G4, G6-G11; G5 still deferred) in `harness/DVerse.Harness/Gates/` with WHY comments, 17 burned lessons in `loop/LESSONS.md`, receipts in `docs/receipts/`, and a demo solution whose every artifact class passed through those gates.

## Mission

Transform the seed into the evolved `dv-architect` skill: the skill that tells an agent how to build Power Platform artifacts UNDER the DVerse gates, with every rule traceable to a gate or a burned lesson.

## Frozen rulings

1. LAYOUT (6.3): mirror the Microsoft marketplace convention. Read the real convention from the public `microsoft/power-platform-skills` repo via `gh api` (read-only; e.g. the tree of one of their plugins) and mirror its folder shape at `plugins/dv-architect/`. Record in your report exactly which paths you examined and what shape you mirrored. If their convention conflicts with Claude's skill format (SKILL.md with frontmatter), the skill file itself stays Claude-format and the marketplace convention governs the FOLDER layout; note the tension honestly.
2. PORT (6.1): carry forward from the seed what is still true and useful. Every carried claim gets re-verified against this repo's reality before it is written (lesson 7: do not trust stated baselines). Drop what is stale; note drops in the report. The six ce-* references carry forward as references only where their content survives scrutiny; consolidate rather than pad. No obligation to keep the seed's structure.
3. ENCODE (6.2), the heart of the slice: a rules section where EVERY rule cites its enforcement: `[G4]`-style gate references for mechanically-refused rules (gate IDs G1-G11, read the actual gate sources for what each truly checks; their WHY comments are the authority), `[L14]`-style lesson references for spec-only rules (lessons 1-17). Rules with neither citation do not get written. The big ones the gates and lessons already prove: publisher prefix, document-location cardinality, solutioncomponents path entries, rootcomponents requirements, datafieldname lowercase, plugin registration rungs (strong-naming, FullName schemaName, part URI, canonical shapes by platform-mirror), --activate-plugins plus post-import probe, canvas .pa.yaml formula prefixing, pack-vs-import gap, decompile-before-parse and platform-mirror doctrine, sibling test projects, committed snk.
4. The skill must state the VERIFICATION LADDER as doctrine: pack exit 0 proves nothing beyond pack; components prove presence in the packed customizations.xml; import proves platform acceptance; publish plus the RENDERED UI or a runtime probe proves behavior; gates refuse mechanically at the bottom of the ladder. This is the house method distilled and it is what makes the skill "evolved."
5. Provenance section in the skill: inherited (seed d365-architect v3, verbatim at seed/) versus built (everything gate- and lesson-backed). Never present inherited material as new.
6. VERIFY: the suite must stay green (baseline 174, zero skips; you add no code, so this is a sanity run) and the full gate ladder must still exit 0 (you touch nothing it reads, same sanity logic). Markdown only; no em dashes anywhere.

## Owned files
- plugins/dv-architect/** (new)
- README.md (ONLY: one row/line adding the skill to the component table and the provenance section; nothing else)

Forbidden: everything else. seed/ is read-only. The harness, demo-solution, docs, loop are read-only ground truth.

## Done means
Committed "Slice 6A:" with DDomingo author flags. Report: the layout-convention evidence (paths examined in microsoft/power-platform-skills), the rules-to-citation table (every rule with its [G]/[L] cite), what was dropped from the seed and why, verbatim suite and gate outputs, commit hash, assumptions.
