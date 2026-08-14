# dv-architect

A Dataverse/Power Platform architect skill for Claude Code, scoped to DVerse v2.

## Provenance

Evolved from `d365-architect` (v3 of its own numbering), a skill in the
archived public seed repository
[`tbalt88/DVerseClaudeSkills`](https://github.com/tbalt88/DVerseClaudeSkills).
That repo is where DVerse v2 itself began (see `docs/upstream-map.md` and
`ARCHITECTURE.md` for the wider provenance story); this port carries one of
its skills forward, not the repo's code.

Ported wave 6.1. The seed's `SKILL.md` was read at 101 lines at clone time
(shallow clone, `git clone --depth 1`), not the 81 the originating spec
described; noted here rather than silently corrected, since the discrepancy
was never resolved against the seed's own history.

## What changed porting it

- **Identity**: renamed `dynamics-365-solo-architect` to `dv-architect`, and
  the skill body now speaks in DVerse v2 terms throughout, not generic D365 CE.
- **New section**: "Rules with mechanical enforcement" in `skill/SKILL.md`,
  mapping every rule this harness actually checks to its gate ID (G1, G2, G3,
  G4, G6, G7, G8, G9, G10). Nothing in that section is asserted without a
  corresponding gate file under `harness/DVerse.Harness/Gates/` backing it.
- **Corrected stale v1 claims** carried in the seed's example content:
  - Publisher prefix is `dv` and the schema prefix is `dv_`, not the seed's
    `dexx` / `dexx_` placeholder. Verified against
    `demo-solution/publishers/dversepublisher/publisher.yml` and
    `demo-solution/entities/dv_matter/`.
  - Solutions in this repository are YAML source (`solution.yml`,
    `solutioncomponents.yml`, `rootcomponents.yml`,
    `missingdependencies.yml`, `publisher.yml`), not the legacy XML unpack
    shape the seed's ALM reference assumed. `skill/references/ce-alm.md`
    keeps the legacy shape as labeled background and adds the real one,
    sourced to `ARCHITECTURE.md` and the gate doc comments that found it by
    decompiling `pac`.
  - The seed's Claude Code tooling references (`bash_tool`, `create_file`,
    `str_replace`, `web_fetch`) were generic placeholder tool names from
    whatever runtime the seed assumed; the port uses this environment's
    actual tool names (`Bash`, `Write`/`Edit`, `Grep`/`Glob`, `WebFetch`).
- **Preserved substantively intact**: all six domain reference files
  (`ce-alm`, `ce-bootstrap`, `ce-data-access`, `ce-integration`,
  `ce-plugin-dev`, `ce-security`), including the seed's 12 plug-in best
  practices (BP-1 through BP-12 in `skill/references/ce-plugin-dev.md`) and
  the doc-grounded architecture defaults. Only the prefix examples, a few
  tool names, and the ALM file's format assumptions were corrected; the
  underlying Dataverse guidance is unchanged from the seed.

## Layout

```
skills/plugins/dv-architect/
├── README.md          (this file)
└── skill/
    ├── SKILL.md
    └── references/
        ├── ce-alm.md
        ├── ce-bootstrap.md
        ├── ce-data-access.md
        ├── ce-integration.md
        ├── ce-plugin-dev.md
        └── ce-security.md
```

This follows Microsoft's `plugins/<name>/` marketplace convention on purpose,
per the frozen ruling that shipped this skill (`loop/specs/wave6-1-skill-port.md`).
It keeps a deferred option open: contributing this skill upstream to
`microsoft/power-platform-skills` later, from a position of demonstrated work
in this repo rather than to establish that position.

## What this skill does not claim

It does not claim gate coverage this repo has not built. G5 (plugin
registration conformance, stage/mode/`FilteringAttributes` against code) is
reserved in the gate numbering but not implemented; `skill/SKILL.md` says so
explicitly rather than listing it as enforced. Any future gate added to
`harness/DVerse.Harness.Cli/GateRegistry.cs` should get a corresponding line
in `skill/SKILL.md`'s "Rules with mechanical enforcement" table, not the
other way around: the gate is the ground truth, the skill describes it.
