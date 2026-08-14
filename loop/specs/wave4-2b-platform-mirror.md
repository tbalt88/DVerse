# Slice spec: wave 4.2b, mirror the platform-authored entity shape

Persisted before spawn per L1 entry gate. Executor: Sonnet, worktree `.worktrees/slice-4-2b`, branch `slice/4.2b`.

## Context

Slice 4.2 authored `dv_matter` from decompiled reader shapes; pack accepted it, the IMPORTER refused it through a chain of errors (primary name, entity labels, primary key, then the SyncError relationship step, which points at missing ownership metadata). The seat then had the PLATFORM author a reference: a `Probe` table created via the maker portal in the org copy of DVerseCore, cloned back with `pac solution clone`. The reference is on this machine, READ-ONLY, at:

`C:\Users\dmdom\AppData\Local\Temp\probe-clone\DVerseCore\src\Entities\dv_Probe\`

It contains the COMPLETE platform-authored truth: `Entity.xml` (41 KB, the full entity element inventory including OwnershipTypeMask and every capability flag), the exact attribute XML shapes, three FormXml forms (main, card, quick), seven SavedQueries, and RibbonDiff.xml. This is the ground truth the importer actually accepts, because the platform itself wrote it.

## Frozen rulings

1. Rebuild `demo-solution/entities/dv_matter/**` to MIRROR the reference faithfully: same entity element set and default values, same attribute element shapes, renamed for our table (dv_probe to dv_matter, dv_Probe to dv_Matter in PhysicalName casing, labels Matter/Matters, description preserved from current Entity.yml). Preserve our three business attributes with shapes mirrored from the reference: `dv_name` mirrors the reference `dv_name` exactly (adjusting MaxLength to 100, RequiredLevel required, our display name), `dv_matternumber` mirrors the nvarchar shape, `dv_openedon` takes the datetime shape from the reference's `createdon`-class attributes but with `IsCustomField` 1 and our labels (Format DateOnly). The primary key attribute mirrors the reference's `dv_probeid` attribute exactly, renamed.
2. YAML form: keep the established `@attr` and element conventions the packer round-trips (the existing files show them). The reference is XML; you are transcribing to our YAML format, and `pac solution pack` then `pac solution unpack` round-trip is your proof of fidelity: unpacked XML should match the reference's structure for every element you carried.
3. Forms and views: transcribe the reference's MAIN form (unmanaged variant) into `FormXml/main/` with our attribute names substituted for the probe's, replacing the invented form from 4.2. Transcribe at least ONE SavedQuery (the active-records view) into `SavedQueries/`. Card and quick forms may be omitted for this slice; note the omission.
4. Do NOT include `_managed.xml` variants. Do not modify solutions manifests except to add the SavedQueries path entry if pack requires one (report if so; the FormXml lesson from 4.2 says subfolder paths need their own entries in solutioncomponents.yml, so expect the same for SavedQueries and FormXml paths you add).
5. GUIDs: forms and saved queries carry formid/savedqueryid GUIDs. Generate fresh GUIDs for ours (do not reuse the probe's). Keep them stable in the files.
6. Verification, outputs verbatim: pack exit 0; unpack round-trip preserving your elements; all offline gates exit 0 (`dotnet run --project harness/DVerse.Harness.Cli -- gate run --solution demo-solution --repo . --ledger <temp>`); full suite green (baseline 145, `dotnet test harness/DVerse.Harness.Tests/DVerse.Harness.Tests.csproj --nologo -v minimal`). IMPORT IS NOT YOURS; the seat runs it at grading.
7. No em dashes anywhere you write.

## Owned files

- demo-solution/entities/dv_matter/** (rebuild)
- demo-solution/solutions/DVerseCore/solutioncomponents.yml (path entries only, if pack demands them)

Forbidden: everything else. The reference clone directory is read-only.

## Definition of done

Committed "Slice 4.2b:" with DDomingo author flags. Report: files, element-fidelity summary (what you carried, what you deliberately omitted), verbatim pack/unpack/gate/suite outputs, commit hash, assumptions.
