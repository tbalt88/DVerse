# Wave 6.4: blind transfer eval of the evolved dv-architect skill

Run 2026-08-14. Design: a fresh Sonnet agent was booted with `plugins/dv-architect/**` as its ONLY permitted knowledge source (forbidden: loop/, docs/, harness/, demo-solution/, seed/, repo README, git, web) and answered ten scenarios whose correct answers are determined by the gates and burned lessons. The seat held the answer key (persisted in `loop/specs/wave6-4-skill-eval.md`) and graded after.

**Blinding caveat, stated honestly:** the spec containing the answer key was committed to the repo before the spawn, so blinding rested on the executor's compliance with its forbidden list rather than physical isolation. Its answers quote the skill's own phrasing throughout and its tool use was consistent with reading only the skill directory. Future evals should hold the key out of the executor-reachable tree until grading; recorded as a process ratchet.

## Scoring

2 = correct substance AND cites the skill's own gate/lesson reference; 1 = correct substance, no citation; 0 = wrong or missing the silent-failure point. Pass bar: 16/20, zero 0-scores on scenarios 1, 2, 5.

| # | Scenario (short) | Score | Notes |
|---|---|---|---|
| 1 | Inverted document-location relationship | 2 | Refused with [G4], named the silently-empty Documents tab, correctly separated pack exit 0 from gate coverage |
| 2 | PascalCase datafieldname | 2 | Silent render drop, lowercase fix, and re-verify by driving the rendered app; [L14] |
| 3 | What pack exit 0 leaves unproven | 2 | All three classes: zip presence [L2], import acceptance [L3], behavior [L8/L17] |
| 4 | First PluginAssembly pitfalls, in order | 2 | Complete rung sequence incl. sibling layout [L9], committed snk [L15], FullName schemaName, part-URI slash, platform-mirror over docs [L4/L15], gate-suspicion [L16], --activate-plugins [L17] |
| 5 | Is the imported plugin running? | 2 | "Not necessarily; nothing in import/publish tells you"; --activate-plugins check + negative-input probe; [L17] |
| 6 | Canvas Properties without `=` | 2 | Silent formula failure; G11 refuses mechanically at source scan |
| 7 | Test project location | 2 | Sibling, never nested; glob-capture defect; [L9] with [G6] mechanism |
| 8 | snk: ignore or commit | 2 | Commit; platform requires signing; the seed's defect was hiding the key, not signing; [L9/L15] |
| 9 | Docs vs tooling disagreement | 2 | Tooling/export wins; decompile + platform-mirror procedure; [L4] with the G9 precedent |
| 10 | The verification ladder | 2 | All five rungs in order, each rung's exact scope, and the necessary-but-not-sufficient close |

**Total: 20/20. Verdict: PASS with no amendments required.** No scenario surfaced a rule the skill fails to carry; the skill's own citations were sufficient for every answer. The full verbatim answers are preserved in the session transcript and reproduced faithfully by the scores above.

## What this proves and does not prove

Proves: the skill transfers the house's burned knowledge to an agent with no access to the history that produced it, with traceable citations. Does not prove: performance on OPEN-ENDED builds (the eval is Q&A-shaped, not build-shaped); a build-shaped eval under the gates is the natural follow-on when a fresh greenfield use case next arises.
