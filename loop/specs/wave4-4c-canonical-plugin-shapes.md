# Slice spec: wave 4.4c, transcribe canonical plugin shapes; G8 accepts id-only root components

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-4-4c`, branch `slice/4.4c`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING.

## Context

4.4b authored plugin registration from decompile plus schema knowledge; import rejected it through four successive rungs (see commit f203540's message). The seat then registered the plugin via the org Web API and cloned back the platform's CANONICAL export. The running app now enforces the plugin (BAD-1 blocked, M-2026 accepted). Your job: make the repo's YAML source match the canonical shapes exactly, so the declarative import path round-trips, and fix the G8 gate defect the canonical export exposed.

## Canonical ground truth (verbatim from `pac solution clone --name DVerseCore`, post-registration; this is the authority, not docs, not the 4.4b YAML)

PluginAssembly (src/PluginAssemblies/DVersePlugins-5AF6F42C-9A97-F111-B8DE-70A8A59A66F9/DVersePlugins.dll.data.xml):

```xml
<PluginAssembly FullName="DVerse.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9f506971365378cc" PluginAssemblyId="5af6f42c-9a97-f111-b8de-70a8a59a66f9" CustomizationLevel="1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <IsolationMode>2</IsolationMode>
  <SourceType>0</SourceType>
  <IntroducedVersion>1.0</IntroducedVersion>
  <FileName>/PluginAssemblies/DVersePlugins-5AF6F42C-9A97-F111-B8DE-70A8A59A66F9/DVersePlugins.dll</FileName>
  <PluginTypes>
    <PluginType AssemblyQualifiedName="DVerse.Plugins.MatterNumberValidator, DVerse.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9f506971365378cc" PluginTypeId="0f998a3b-9a97-f111-b8de-70a8a59a66f9" Name="DVerse.Plugins.MatterNumberValidator">
      <FriendlyName>DVerse.Plugins.MatterNumberValidator</FriendlyName>
    </PluginType>
  </PluginTypes>
</PluginAssembly>
```

SdkMessageProcessingStep (src/SdkMessageProcessingSteps/{10998a3b-9a97-f111-b8de-70a8a59a66f9}.xml):

```xml
<SdkMessageProcessingStep Name="MatterNumberValidator: Create of dv_matter" SdkMessageProcessingStepId="{10998a3b-9a97-f111-b8de-70a8a59a66f9}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <SdkMessageId>9ebdbb1b-ea3e-db11-86a7-000a3a5473e8</SdkMessageId>
  <PluginTypeName>DVerse.Plugins.MatterNumberValidator, DVerse.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9f506971365378cc</PluginTypeName>
  <PluginTypeId>0f998a3b-9a97-f111-b8de-70a8a59a66f9</PluginTypeId>
  <PrimaryEntity>dv_matter</PrimaryEntity>
  <AsyncAutoDelete>0</AsyncAutoDelete>
  <Description>Validates dv_matternumber on Create of dv_matter (MatterNumberValidator, PreOperation, synchronous).</Description>
  <FilteringAttributes></FilteringAttributes>
  <InvocationSource>1</InvocationSource>
  <Mode>0</Mode>
  <Rank>1</Rank>
  <EventHandlerTypeCode>4602</EventHandlerTypeCode>
  <Stage>20</Stage>
  <IsCustomizable>1</IsCustomizable>
  <IsHidden>0</IsHidden>
  <SupportedDeployment>0</SupportedDeployment>
  <IntroducedVersion>1.0</IntroducedVersion>
  <SdkMessageProcessingStepImages />
</SdkMessageProcessingStep>
```

RootComponents (src/Other/Solution.xml):

```xml
<RootComponent type="91" id="{5af6f42c-9a97-f111-b8de-70a8a59a66f9}" schemaName="DVerse.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9f506971365378cc" behavior="0" />
<RootComponent type="92" id="{10998a3b-9a97-f111-b8de-70a8a59a66f9}" behavior="0" />
```

Note the step's root component has NO schemaName at all, ids are brace-wrapped, and the step has NO SdkMessageFilterId (PrimaryEntity carries the entity binding by logical name, which is org-portable; the org-specific filter id the 4.4b report called its highest risk simply does not appear in the canonical export).

## Frozen rulings

1. TRANSCRIBE the canonical XML into the existing YAML source files (`demo-solution/pluginassemblies/DVerse.Plugins.yml`, `demo-solution/sdkmessageprocessingsteps/dv_matter_Create_MatterNumberValidator.yml`), replacing the 4.4b-guessed element sets entirely. Adopt the platform ids verbatim (assembly 5af6f42c-9a97-f111-b8de-70a8a59a66f9, type 0f998a3b-9a97-f111-b8de-70a8a59a66f9, step 10998a3b-9a97-f111-b8de-70a8a59a66f9, braces where the canonical XML has braces) so the next import UPDATES the registered components instead of duplicating them. Keep OUR FileName part path (/pluginassemblies/DVerse.Plugins.dll) since our pack layout carries the DLL there; everything else follows the canonical shape. Update each file's WHY header: the authority is now the platform's own export (platform-mirror), cite the clone; keep the decompile citations for what pac reads; record the four import rungs from commit f203540 as the reason doc-derived shapes were replaced.
2. rootcomponents.yml: mirror the canonical entries. Type 91 keeps id (braced) + schemaName (the FullName). Type 92 becomes id-only, NO schemaName, which today G8 refuses. That is a GATE DEFECT: the platform's own export is a shape our gate rejects.
3. FIX G8 (harness/DVerse.Harness/Gates/RootComponentSourceGate.cs, now in your owned files): a RootComponent entry with a non-empty '@id' and no '@schemaName' is legitimate (GUID-identified component types); G8 must accept it, skipping source verification for it with an explicit Evidence line (same honest-skip pattern it already uses for unmapped types). An entry with NEITHER id NOR schemaName still refuses. Ship a red fixture proving the refuse case still refuses, and a green fixture (or extend an existing one) with an id-only entry. Mutation-check per lesson 6: revert your gate change, confirm the new green fixture test goes red, restore.
4. VERIFY: pack exit 0 with both components in the packed customizations.xml matching the canonical shapes (paste blocks); unpack round-trip; all offline gates exit 0 (G8 now passing over the id-only entry); full suite green, zero skips, baseline 156 plus whatever tests you add.
5. The seat imports at grading; you never write to the org. pac READ allowed via dverse-ci profile.
6. No em dashes anywhere you write.

## Owned files
- demo-solution/pluginassemblies/DVerse.Plugins.yml
- demo-solution/sdkmessageprocessingsteps/**
- demo-solution/solutions/DVerseCore/rootcomponents.yml
- harness/DVerse.Harness/Gates/RootComponentSourceGate.cs
- harness/DVerse.Harness.Tests/** (G8 tests and fixtures only)
- harness/fixtures/** (G8 fixtures only)

Forbidden: everything else, including the plugin csproj/snk/source, solutioncomponents.yml, all other gates, workflows, docs. Org writes forbidden absolutely.

## Done means
Committed "Slice 4.4c:" with DDomingo author flags. Report: files changed, the G8 change with its mutation-check evidence, verbatim pack/gate/suite outputs, packed-zip evidence blocks for both components, commit hash, assumptions.
