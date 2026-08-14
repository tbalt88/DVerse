# Wave 4.3: the refusal pair

Slice 4.3's flagship deliverable. Two runs of the same offline CLI over the same
relationship artifact, one relationship direction apart, captured 22 seconds
apart on 2026-08-14.

## Run A: the correct solution (`demo-solution`)

`dotnet run --project harness/DVerse.Harness.Cli -- gate run --solution demo-solution
--repo . --stage integration`, 8 gates, 8 PASS, exit 0.

G4's ledger line, verbatim:

```json
{"GateId":"G4","GateName":"document-location-cardinality","Outcome":"Pass","Artifact":"demo-solution/entityrelationships","Evidence":"Inspected 1 relationship file(s) under entityrelationships/; 1 touch a SharePoint document table and all are one-to-many with the document table on the many side.","At":"2026-08-14T02:10:48.9543132+00:00","Stage":"Integration"}
```

## Run B: the inverted copy (temp directory, never committed)

`demo-solution` copied to a scratch directory outside the repository, with the
one relationship file's `ReferencingEntityName` and `ReferencedEntityName`
swapped and nothing else touched. Same CLI, a separate ledger:
`gate run --solution <temp-copy> --repo <temp-copy-parent> --stage integration`,
7 PASS, 1 REFUSE, exit 1.

G4's ledger line, verbatim:

```json
{"GateId":"G4","GateName":"document-location-cardinality","Outcome":"Refuse","Artifact":"wave4-3-inverted-solution/entityrelationships/dv_matter_SharePointDocumentLocations.yml","Evidence":"Inspected relationship 'dv_matter_SharePointDocumentLocations': type OneToMany, referencing 'dv_Matter', referenced 'SharePointDocumentLocation'.","Reason":"Relationship is inverted: 'SharePointDocumentLocation' is the referenced (one) side and 'dv_Matter' is the referencing (many) side. Dataverse requires the document table on the MANY side (1:N from the custom entity). As authored, the Documents tab will be silently empty.","At":"2026-08-14T02:11:10.0135524+00:00","Stage":"Integration"}
```

## Framing

Both files are the same artifact, one relationship direction apart: same
relationship name, same `EntityRelationshipType` of `OneToMany`, same cascade
block, same description text. The only difference between Run A and Run B is
which entity name sits in `ReferencingEntityName` and which sits in
`ReferencedEntityName`. Both are well formed XML-shaped YAML. Both pack with
`pac solution pack` at exit 0 (confirmed empirically this slice for the
inverted copy too, over and above the CLI runs above). Dataverse's own
documentation, quoted in the gate itself, says a many-to-one relationship to a
SharePoint document table "results in the app not listing the documents that
exist in the SharePoint document library": the platform accepts the inverted
authoring at import, publishes it without a warning, and the only symptom is
that a user opens the Documents tab on a matter record and finds it silently
empty, with no error message pointing back at the relationship that caused it.

The harness gives the opposite experience for the identical mistake. G4 reads
the same file, checks two element values against one frozen rule, and refuses
before any packaging, checking, or import has run at all: Run A to Run B above
is a single `dotnet run` invocation each, sub-second gate evaluation inside
that, entirely offline, no Dataverse tenant involved. The reason is not a log
line the author has to go dig for; it is printed to the console at refusal
time and sits on the ledger permanently, naming the exact symptom ("the
Documents tab will be silently empty") the platform itself would only ever
show as an absence. That is the whole case for the gate in one artifact: what
costs an afternoon of confused clicking on the platform costs milliseconds and
comes with its own explanation here.

## Related finding surfaced this slice

Packing `demo-solution` with the relationship file present but with no
matching `@path` entry in `solutions/DVerseCore/solutioncomponents.yml`
produced `pac solution pack` exit 0 and an empty `<EntityRelationships />` in
the packed `customizations.xml`: the exact silent-omission shape G9 and slice
4.2 documented, this time for the `entityrelationships/` folder rather than an
individual component. See `demo-solution/solutions/DVerseCore/solutioncomponents.yml`
and `demo-solution/entityrelationships/dv_matter_SharePointDocumentLocations.yml`
for the full finding and the fix (an `entityrelationships` path entry). The
packed `EntityRelationship` block once the entry is present:

```xml
<EntityRelationships>
    <EntityRelationship Name="dv_matter_SharePointDocumentLocations">
      <EntityRelationshipType>OneToMany</EntityRelationshipType>
      <IsCustomizable>1</IsCustomizable>
      <IntroducedVersion>1.0</IntroducedVersion>
      <IsHierarchical>0</IsHierarchical>
      <ReferencingEntityName>SharePointDocumentLocation</ReferencingEntityName>
      <ReferencedEntityName>dv_Matter</ReferencedEntityName>
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
  </EntityRelationships>
```

An unpack round trip of this same zip (`pac solution unpack`) reproduced every
element and value above unchanged under `Other/Relationships/dv_Matter.xml`,
confirming the data survives the pack and unpack cycle intact.
