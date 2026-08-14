# Slice spec: wave 4.3, the document-location relationship and the refusal pair

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-4-3`, branch `slice/4.3`. THIS IS THE FLAGSHIP SLICE: its deliverable is the pair of ledger entries the whole project exists to produce.

## Platform-authored ground truth (extracted by the seat, 2026-08-13)

The stock `contact` entity's relationship to `SharePointDocumentLocation`, cloned from the live org (also on disk at `C:\Users\dmdom\AppData\Local\Temp\account-ref2\DVerseCore\src\Other\Relationships\Contact.xml`, READ-ONLY):

```xml
<EntityRelationship Name="contact_SharePointDocumentLocations">
  <EntityRelationshipType>OneToMany</EntityRelationshipType>
  <IsCustomizable>1</IsCustomizable>
  <IntroducedVersion>1.0</IntroducedVersion>
  <IsHierarchical>0</IsHierarchical>
  <ReferencingEntityName>SharePointDocumentLocation</ReferencingEntityName>
  <ReferencedEntityName>Contact</ReferencedEntityName>
  <CascadeAssign>Cascade</CascadeAssign>
  <CascadeDelete>Cascade</CascadeDelete>
  <CascadeArchive>NoCascade</CascadeArchive>
  <CascadeReparent>Cascade</CascadeReparent>
  <CascadeShare>Cascade</CascadeShare>
  <CascadeUnshare>Cascade</CascadeUnshare>
  <ReferencingAttributeName>RegardingObjectId</ReferencingAttributeName>
  <RelationshipDescription>
    <Descriptions>
      <Description description="Unique identifier of the object with which the SharePoint document location record is associated." languagecode="1033" />
    </Descriptions>
  </RelationshipDescription>
</EntityRelationship>
```

Wrapped in `<EntityRelationships>` root. NOTE: the clone emits CLASSIC format (`Other/Relationships/<Entity>.xml`); our repo is the YAML source format, whose documented relationship folder is `entityrelationships/`. Whether pack actually consumes that folder, and under which per-file naming, is YOURS TO PROVE via pack round-trip: the relationship must appear inside the packed `customizations.xml` under `<EntityRelationships>`. A silent omission (pack exit 0, relationship absent from the zip) is exactly the failure class G9 and 4.2 documented; if a `solutioncomponents.yml` path entry is needed to make it stick, add it and report.

## Frozen rulings

1. Author `demo-solution/entityrelationships/dv_matter_SharePointDocumentLocations.yml`: a YAML transcription of the reference with Contact replaced by our entity (relationship name `dv_matter_SharePointDocumentLocations`, ReferencedEntityName `dv_Matter`), every other element mirrored verbatim including the cascade block and description.
2. Flip `IsDocumentManagementEnabled` from `'0'` to `'1'` in `demo-solution/entities/dv_matter/Entity.yml` (single line; this is the declarative doc-management enable, per D15).
3. G4 REALIGNMENT PERMITTED AND EXPECTED: G4 currently parses the wave 1 inferred YAML shape. Realign its parser to the shape you actually author (which mirrors platform XML through our established `@attr` YAML conventions), exactly as G9 was realigned. The SEMANTICS ARE FROZEN and must survive: an entity-to-SharePointDocumentLocation relationship must be OneToMany with SharePointDocumentLocation on the REFERENCING (many) side; anything else refuses with the silent-empty-Documents-tab reason. Rewrite the g4 fixtures to the new shape preserving each fixture's semantic (pass stays pass, every refuse-* refuses for its original reason).
4. THE REFUSAL PAIR, your headline deliverable:
   a. Run the full offline gate suite over the correct solution; capture G4's PASS ledger line verbatim.
   b. Copy demo-solution to a temp directory, INVERT the relationship there (swap ReferencingEntityName and ReferencedEntityName), run the same CLI against the copy with a separate ledger; capture G4's REFUSE line verbatim. The temp copy is never committed; the two ledger lines are.
   c. Write `docs/receipts/wave4-3-refusal-pair.md`: the two verbatim JSONL ledger entries side by side, timestamps intact, with two paragraphs of honest framing: same artifact, one relationship direction apart; the platform imports both without complaint and the wrong one fails only as documents silently not appearing; the harness refuses it in milliseconds, offline, with the reason on the record.
5. Verification, verbatim: pack exit 0 WITH the relationship present in packed customizations.xml (paste the packed EntityRelationship block), unpack round-trip, all offline gates exit 0 over demo-solution, full suite green (baseline 145 plus your G4 test adjustments, zero skips). IMPORT IS NOT YOURS; the seat runs it at grading.
6. No em dashes anywhere you write. House conventions throughout.

## Owned files

- demo-solution/entityrelationships/** (new)
- demo-solution/entities/dv_matter/Entity.yml (the single-line flip only)
- demo-solution/solutions/DVerseCore/solutioncomponents.yml (path entries if pack demands)
- harness/DVerse.Harness/Gates/DocumentLocationCardinalityGate.cs (realignment)
- harness/DVerse.Harness.Tests/Gates/DocumentLocationCardinalityGateTests.cs
- harness/fixtures/g4/** (reshape, semantics preserved)
- docs/receipts/wave4-3-refusal-pair.md (new)

Forbidden: everything else, including all other gates, GateRegistry, WaveOneIntegrationTests (G4 is already wired), workflows, the reference clone directories.

## Definition of done

Committed "Slice 4.3:" with DDomingo author flags. Report: files, the realigned G4 rule in one sentence, the packed EntityRelationship block, THE REFUSAL PAIR verbatim, suite and gate outputs verbatim, commit hash, assumptions.
