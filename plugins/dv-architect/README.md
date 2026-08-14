# dv-architect

A Dataverse/Power Platform architect skill for Claude Code, scoped to DVerse
v2. Evolved wave 6A (slices 6.1 PORT + 6.2 ENCODE + 6.3 LAYOUT, done as one
slice per `loop/specs/wave6-a-evolved-architect-skill.md`).

## Provenance

Evolved from `d365-architect` (v3 of its own numbering), a skill in the
archived public seed repository
[`tbalt88/DVerseClaudeSkills`](https://github.com/tbalt88/DVerseClaudeSkills),
committed verbatim in this repo at `seed/d365-architect/` (commit `2c72b66`).
That repo is where DVerse v2 itself began; this port carries one of its
skills forward, not the repo's code.

**Inherited, unchanged in substance:** the event pipeline stage table, the
`IPlugin` shape and BP-1 through BP-12, the Web API/Organization Service
decision table, the three-layer security model, the Service Bus/webhook/
virtual-entity integration patterns, the day-1 bootstrap runbook shape. These
are standard Dataverse platform behaviors, not something this repo's harness
checks, and are not claimed as new work.

**Built here:** every rule in `skill/SKILL.md`'s two enforcement tables
("Rules with mechanical enforcement", cross-referenced to gate IDs G1-G4,
G6-G11, and "Rules proven by a burned lesson", cross-referenced to lesson
IDs in `loop/LESSONS.md`), the "Verification ladder" doctrine section, the v2
reality corrections (publisher prefix, YAML solution format, the committed
`.snk`), and the plugin-registration-rungs section in
`skill/references/ce-plugin-dev.md`. See `skill/SKILL.md`'s own "Provenance"
section for the complete split.

## What changed from the seed, and why

- **Identity**: renamed `dynamics-365-solo-architect` to `dv-architect`; the
  skill body speaks in DVerse v2 terms throughout.
- **New**: the two rule tables above and the verification-ladder doctrine.
  Nothing in either table is asserted without a corresponding gate file under
  `harness/DVerse.Harness/Gates/` or a numbered entry in `loop/LESSONS.md`
  backing it. This is the point of doing 6.2 after the gates existed
  (ROADMAP wave 6 rationale): every rule stated has already been shown
  enforceable, not asserted and hoped enforceable later.
- **Corrected stale claims carried in the seed's example content:**
  - Publisher prefix is `dv`, schema prefix `dv_`, not the seed's `dexx`/
    `dexx_` placeholder. Verified against
    `demo-solution/publishers/dversepublisher/publisher.yml`
    (`CustomizationPrefix: dv`) and `demo-solution/entities/dv_matter/`.
  - Solutions here are the YAML source format
    (`solution.yml`/`solutioncomponents.yml`/`rootcomponents.yml`/
    `missingdependencies.yml`/`publisher.yml`), not the legacy XML unpack
    shape (`Entities/`, `Other/Customizations.xml`) the seed's ALM reference
    assumed. `skill/references/ce-alm.md` states the real shape and cites
    G1/G10 for it.
  - **The seed's own `.gitignore` templates (`ce-alm.md` and
    `ce-bootstrap.md`) listed `*.snk` as mandatory to ignore.** This is
    backwards for a Sandbox-isolation plugin assembly, which must be signed
    with a public key token; this repo hit that exact defect once (lesson 9,
    lesson 15) and the fix was committing the key, not un-signing. Both
    reference files now say so and carry a corrected `.gitignore` block with
    `*.snk` removed.
  - The seed's generic Claude Code tool names (`bash_tool`, `create_file`,
    `str_replace`, `web_fetch`) are replaced with this environment's actual
    tool names (`Bash`, `Write`/`Edit`, `Grep`/`Glob`, `WebFetch`).
- **Preserved substantively intact, with naming corrected:** all six domain
  reference files, including the 12 plug-in best practices and the doc-
  grounded architecture defaults. `ce-integration.md` and `ce-security.md`
  needed only naming corrections (`dexx_` -> `dv_`); no gate exists over
  either domain yet, so both stay spec-only in substance, honestly labeled
  as such.
- **No 7th reference file for canvas apps.** G11 (`canvas-yaml`) is real and
  cited, but its three rules are short enough to state directly in
  `SKILL.md`'s rule table rather than pad out a seventh reference file for a
  domain the seed never covered; consolidation over padding, per the frozen
  ruling.

## Layout: the Microsoft marketplace convention

Examined via `gh api repos/microsoft/power-platform-skills/git/trees/main?recursive=1`
(read-only) and spot-checked with `gh api .../contents/<path>` for exact file
contents. Paths examined:

- `plugins/power-automate/.claude-plugin/plugin.json` and
  `plugins/power-automate/.plugin/plugin.json` (identical content, two
  locations for the same manifest — this repo mirrors only the `.claude-
  plugin/` one, since that is the one Claude Code's own plugin loader reads)
- `plugins/power-automate/README.md`
- `plugins/power-automate/skills/create-flow/SKILL.md` (frontmatter shape for
  a command-style skill)
- `plugins/canvas-apps/skills/canvas-app/SKILL.md` (frontmatter shape for a
  trigger-style skill, closer to what `dv-architect` is)
- `plugins/mobile-apps/skills/**` and `plugins/mcp-apps/skills/**` (skill
  folder nesting: `plugins/<name>/skills/<skill-name>/SKILL.md`, with an
  optional `references/` folder either shared at the plugin root
  (`power-automate/references/`, `mcp-apps/references/`) or nested per-skill
  when only one skill uses it (`mobile-apps/skills/add-dataverse/references/`)
- `marketplace.json` and `.claude-plugin/marketplace.json` at the repo root
  (identical content: `{name, owner, metadata, plugins: [{name, source}]}`)

**Shape mirrored:**

```
plugins/dv-architect/
├── .claude-plugin/
│   └── plugin.json          (name, version, description, author, keywords)
├── README.md                (this file: provenance, what changed, layout evidence)
└── skills/
    └── dv-architect/
        ├── SKILL.md          (Claude skill format: YAML frontmatter + body)
        └── references/       (six domain reference files, shared within this one skill)
```

Every real plugin in `microsoft/power-platform-skills` puts its SKILL.md
files under `skills/<skill-name>/`, never at the plugin root directly; the
manifest lives in `.claude-plugin/plugin.json` at the plugin root; a shared
`references/` folder sits either at the plugin root (multi-skill plugins) or
nested under one skill (single-consumer references). Since `dv-architect` is
one skill, its `references/` folder is nested under
`skills/dv-architect/references/` rather than promoted to the plugin root;
either placement is attested in the real repo, and nesting keeps the
provenance of "these six files belong to this one skill" visible in the path
itself.

**The honest tension, stated per the frozen ruling:** Claude's skill format
requires `SKILL.md` with YAML frontmatter, and that does not change to fit a
marketplace convention. There is no actual conflict here, though: the real
`microsoft/power-platform-skills` plugins already ARE Claude-format
`SKILL.md` files, just nested one level deeper (`skills/<name>/SKILL.md`)
than a naive `plugins/<name>/SKILL.md` guess would assume. The marketplace
convention governs the FOLDER layout around the skill (`.claude-plugin/`,
`skills/<name>/`, `references/`); it never asked the skill file itself to be
anything other than Claude format. No root-level `marketplace.json` is added
in this repo: DVerse ships one plugin, not a marketplace of plugins, and
`ROADMAP.md`'s deferred option (contributing upstream to
`microsoft/power-platform-skills` later) is the point at which a marketplace
entry would matter, not before.

## What this skill does not claim

It does not claim gate coverage this repo has not built. G5 (plugin
registration conformance, correlating registration YAML against plugin C#
source) is reserved in the gate numbering but not implemented;
`skill/SKILL.md` says so explicitly rather than listing it as enforced. Any
future gate added to `harness/DVerse.Harness.Cli/GateRegistry.cs` should get
a corresponding line in `skill/SKILL.md`'s enforcement table, not the other
way around: the gate is the ground truth, the skill describes it.

It also does not claim mechanical coverage for the security model, the
integration patterns, or most of the plugin-development reference beyond the
registration rungs: those sections are labeled spec-only (or cite a lesson,
never a gate that does not exist) throughout.
