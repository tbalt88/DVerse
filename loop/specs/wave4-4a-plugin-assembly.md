# Slice spec: wave 4.4a, the plugin assembly (code and tests only)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-4-4a`, branch `slice/4.4a`. Registration (SdkMessageProcessingSteps in the solution) is NOT this slice: its YAML shape is unobserved and lands in 4.4b via the platform-mirror method. This slice is the imperative half only.

## Frozen rulings

1. Project `demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.csproj`, net462, SDK-style, referencing `Microsoft.CrmSdk.CoreAssemblies` 9.0.2.49. Tests in `demo-solution/plugins/DVerse.Plugins.Tests/` (net462, xunit + Moq), SEPARATE sibling directory, NOT nested inside the plugin project folder. The archived seed repo (public, https://github.com/tbalt88/DVerseClaudeSkills) made exactly that nesting mistake and its plugin csproj globbed the test sources; docs in this repo record it. Clone the seed shallow to a temp dir for pattern reference (its plugin classes and test style are the house pattern); do not vendor its files.
2. One plugin: `MatterNumberValidator`, registered-for (conceptually) Create and Update of dv_matter. Behavior: dv_matternumber, when present, must match `^M-\d{4,}$`; violation throws InvalidPluginExecutionException naming the value and the expected format. Read the target entity from InputParameters; no Web API calls, no external I/O, per the plug-in best practices the seed's skill documents.
3. Strong-name signing OFF (no key.snk anywhere; the seed's gitignored-snk-with-SignAssembly-true defect is also on the record here).
4. Tests: happy path, violation, absent attribute, non-target entity no-op. xUnit + Moq on IServiceProvider/IPluginExecutionContext, seed-style.
5. G6 must DISCOVER and gate this: from the worktree run the CLI over demo-solution and paste G6's verdict; expect Refuse-free with your project counted. Full suite stays green (baseline 145; the G6 fixture tests build real projects, yours adds build time; zero skips).
6. No em dashes anywhere you write.

## Owned files
- demo-solution/plugins/** (new)

Forbidden: everything else, including all harness code and demo-solution outside plugins/.

## Done means
Committed "Slice 4.4a:" with DDomingo author flags. Report: files, verbatim `dotnet test` of your test project, verbatim CLI gate run showing G6's verdict, suite output, commit hash, assumptions.
