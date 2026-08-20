# Plugin Development Reference

> Inherited from the seed `ce-plugin-dev.md` (event pipeline, IPlugin/PluginBase
> shape, context properties, BP-1 through BP-12): substantively unchanged, this
> is standard Dataverse SDK behavior, not something this repo's harness checks.
> Everything under "DVerse additions" below is new: rules this repo has
> actually proven, live, against a running org (wave 4.4), each cited to the
> gate or burned lesson that proves it.

---

## Event Pipeline — Four Stages

| Stage | Code | In Transaction | Use For | Never Use For |
|---|---|---|---|---|
| PreValidation | 10 | Before tx opens | Cancel operation; runs before security checks | Reading related records not yet in context |
| PreOperation | 20 | Yes | Modify Target values before save; set defaults | Cancelling (rollback + perf hit); external calls |
| MainOperation | 30 | Internal only | Custom API / virtual table providers only | Direct plugin registration |
| PostOperation | 40 | Yes (sync) / No (async) | Modify response properties; trigger side effects | Updating same entity (triggers new Update event) |

An exception thrown at any synchronous stage rolls back the whole transaction.
Cancel in PreValidation, not PreOperation. Async PostOperation runs outside the
transaction via the async service; required for `SystemUser` Create event
updates (`UserSettings` does not exist yet when sync PostOp fires).

## IPlugin — Reference Pattern

Target `net462` (the DVerse plugin project's real TFM,
`demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.csproj`; the official spec
also names `netstandard2.0` as buildable, but this repo standardizes on
`net462` since that is what the live plugin project uses and what G6 actually
builds and tests). Stateless: no instance fields storing services or context
data.

```csharp
public class OnCreateOrder : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var context = (IPluginExecutionContext)serviceProvider
            .GetService(typeof(IPluginExecutionContext));
        var tracingService = (ITracingService)serviceProvider
            .GetService(typeof(ITracingService));
        var factory = (IOrganizationServiceFactory)serviceProvider
            .GetService(typeof(IOrganizationServiceFactory));
        var orgService = factory.CreateOrganizationService(context.UserId);

        if (context.MessageName != "Create" || context.PrimaryEntityName != "dv_matter")
            return;

        var target = (Entity)context.InputParameters["Target"];

        try
        {
            tracingService.Trace("OnCreateOrder: start, Id={0}", target.Id);
            // business logic
        }
        catch (Exception ex)
        {
            tracingService.Trace("OnCreateOrder: unhandled error: {0}", ex);
            throw new InvalidPluginExecutionException(
                "OnCreateOrder unexpected error: " + ex.Message, ex);
        }
    }
}
```

## Key Context Properties

```csharp
context.MessageName          // "Create", "Update", "Delete", "Associate", etc.
context.PrimaryEntityName    // logical entity name, e.g. "dv_matter"
context.PrimaryEntityId      // GUID of target record
context.Stage                // 10=PreVal, 20=PreOp, 40=PostOp
context.Mode                 // 0=Sync, 1=Async
context.Depth                // call depth; guard against infinite loops with Depth > 1
context.InputParameters      // "Target" (Entity), "EntityMoniker" for Delete
context.PreEntityImages      // snapshot before operation (register in PRT)
context.PostEntityImages     // snapshot after operation (register in PRT)
```

## Best Practices (spec-only, no gate covers plugin C# internals yet)

```
BP-1  Stateless: no instance fields storing services or context data
BP-2  InvalidPluginExecutionException for ALL user-facing errors
BP-3  ITracingService: trace at entry, exit, and all branch decisions
BP-4  No ExecuteMultiple/ExecuteTransaction inside plugins
BP-5  No parallel/multi-thread execution inside plugins
BP-6  No duplicate step registration (causes multiple firings)
BP-7  Set FilteringAttributes on Update steps (avoid fire on every Update)
BP-8  Set Timeout on external calls; external HTTP has no default timeout
BP-9  Set KeepAlive=false on HttpWebRequest to external hosts
BP-10 Single assembly per solution
BP-11 Avoid Retrieve/RetrieveMultiple synchronous steps for perf
BP-12 Merge separate plugins into one assembly for perf/maintainability
```

---

## DVerse additions (gate- and lesson-backed, proven live wave 4.4)

**Scaffold + build layout [L9].** The plugin project and its test project are
SIBLINGS, never nested: `demo-solution/plugins/DVerse.Plugins/` and
`demo-solution/plugins/DVerse.Plugins.Tests/` side by side. G6 discovers and
builds every `*.csproj` under the solution root independently; a test project
nested inside the plugin project's own folder is a build defect this repo hit
once (the seed's own scaffold pattern) and now avoids by construction. G6
[G6] classifies each discovered project via `IsTestProject` and routes it to
`dotnet test` or `dotnet build` accordingly; zero skipped tests is required
for a Pass on the test-classified project.

**Strong-name the assembly, and COMMIT the key [L9, L15].** Sandbox isolation
mode requires the assembly to carry a public key token, so it must be signed.
`DVerse.Plugins.snk` is committed at
`demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.snk`, not gitignored.
This directly corrects a stale claim carried from the seed (both its ALM and
bootstrap references listed `*.snk` as a mandatory `.gitignore` entry): the
defect that actually bit this repo, empirically, was gitignoring the key, not
signing itself. Signing was always required; hiding the key from source
control is what broke Sandbox registration.

**PluginAssembly RootComponent schemaName is the assembly's FULL NAME, not a
bare name [L15].** `'@schemaName'` (or, per the platform's own canonical
export, `'@FullName'` on the `PluginAssembly` element itself) must read
`DVerse.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=...`, the
exact string `AssemblyName.FullName` returns for the signed, built DLL, not
`DVerse.Plugins` alone. Import rejects the bare form.

**The DLL's FileName part URI needs a LEADING SLASH [L15].** The packed
`PluginAssembly` element's `FileName` child must read
`/pluginassemblies/DVerse.Plugins.dll`, not
`pluginassemblies/DVerse.Plugins.dll`. Import rejects the form without it.

**A new component type's first registration goes through platform-mirror,
never docs or a guessed shape [L4, L15].** Register the component once via
the org Web API (a seat-only tenant write), then `pac solution clone` and
transcribe the platform's own canonical XML back into YAML source. Four
distinct import rejections were absorbed one at a time, in sequence, before
this became standing procedure (`loop/specs/wave4-4c-canonical-plugin-shapes.md`
is the transcription this repo now treats as authoritative for the
`PluginAssembly` / `PluginType` element and attribute set). Each import
rejection is invisible until the previous one clears; do not guess the next
one, mirror it.

**`FilteringAttributes` and step registration have no mechanical gate today.**
G5 (plugin registration conformance, correlating registration YAML against
plugin C# source) is reserved in the numbering but not built; treat every
statement in this section about step shape as spec-only unless it names a
different gate.

**`pac solution import` leaves every plugin step DISABLED without
`--activate-plugins` [L17].** Import exits 0, publish reports clean, the step
exists with the correct ids, `statecode` is 1 (disabled), and nothing else in
the ladder below runtime notices. Every import of a solution carrying steps
in this repo uses `--activate-plugins`, and every import is followed by a
post-import runtime probe: a negative-input request against the live org that
must be refused by the plugin, not just accepted by the platform. See the
"Verification ladder" section of `SKILL.md` for why this rung cannot be
skipped even after three green gate rungs below it.
