# Wave 7 receipt: the diff refusal pair

2026-08-14. The structural-diff analog of wave 4.3's refusal pair: two runs of the same `dverse diff` engine, minutes apart, one `datafieldname` casing flip apart. Both runs' G12 verdicts are recorded in the committed ledger (`loop/gates.jsonl`).

## Run 1: the real evolution diff (PASS)

Baseline: the wave-4 close commit `f74983c` (DVerseCore 0.5.0.0, pre-canvas), materialized as a detached git worktree. Target: the current tree.

```
dverse diff --solution demo-solution --baseline <wave4-worktree>/demo-solution --repo . --ledger loop/gates.jsonl
...
19 passed, 0 refused, 0 skipped.
No gate refused.
```

Every one of the 19 classified declarative artifacts diffed clean or as honest adds (the canvas app files exist only in the current tree and report as artifact-level Added without recursion, per the identity model's short-circuit rule). Positional-pairing warnings for FormXml rows/columns ride verbatim in the PASS evidence.

## Run 2: the mutation (REFUSE, exit 1)

A scratch copy of the current tree with exactly one change: `'@datafieldname': dv_openedon` flipped to `dv_OpenedOn` in the Matter main form. This is lesson 14's live shape: pack, import, and publish all accept it, and the control silently drops at render.

```
REFUSE .tmp-mutated/entities/dv_matter/FormXml/main/dv_matter_main.yml
       entities/dv_matter/FormXml/main/dv_matter_main.yml: FormXmlControl[key=dv_openedon]: Changed,
       1 property differ: unverified pairing, content-confirmed only;
       '@datafieldname' 'dv_openedon' -> 'dv_OpenedOn'.
       reason: FormXmlControl[dv_openedon]'s '@datafieldname' changed to 'dv_OpenedOn', which is not
       the exact lowercase of itself ('dv_openedon'). loop/LESSONS.md #14: a FormXml control's
       datafieldname binds by the attribute's lowercase LogicalName; any other casing makes the
       control drop SILENTLY at render, after pack, import, and publish all accept it without
       complaint.

18 passed, 1 refused, 0 skipped.
REFUSED. 1 violation(s) recorded in the ledger.
```

## Why this is the wave's point

Wave 4 proved the harness refuses a silent defect the platform accepts, statically. This pair proves it refuses the same class OF CHANGE: the defect that cost a live diagnosis session in wave 4.6 (the Owner-only form) would now be caught mechanically the moment it entered the tree, named with its lesson, before pack ever ran. The identity model underneath matched every unchanged element across the two trees by stable platform ids, so the one real change is the only thing reported.

Ledger lines: `loop/gates.jsonl`, GateId G12, one Refuse among the Pass entries of the same run.
