# Skills

Wave 6.1 shipped the first one: `plugins/dv-architect`, the evolved
`d365-architect` skill ported from the archived seed repo
`tbalt88/DVerseClaudeSkills`. See `plugins/dv-architect/README.md` for
provenance and what changed in the port.

Laid out to Microsoft's plugins/<name>/ marketplace convention on purpose. That
keeps the deferred option open: contributing upstream to
microsoft/power-platform-skills later, from a position of demonstrated work
rather than to establish it.

The skill was built AFTER the gates, deliberately. The gates prove which rules
are mechanically checkable; the skill then states rules that have already been
shown enforceable, rather than asserting rules that turn out not to be. That
is why `plugins/dv-architect/skill/SKILL.md` cross-references a gate ID for
every rule it claims is mechanically checked, and says so explicitly where a
rule (G5) is reserved but not yet built.
