# Slice spec: wave 4.6t, fix the form binding and bring the app into source

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-4-6t`, branch `slice/4.6t`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING. Lessons 2, 3, 4, 8 are this slice's daily bread.

## The defect, observed in the running app

The Matter App renders the dv_matter main form with ONLY the Owner field. Our three custom columns (dv_name, dv_matternumber, dv_openedon) do not appear. Every lower rung was green: pack, import, all gates. Only driving the UI exposed it (lesson 8). The page title reads "Matter: Information", suggesting the app may be bound to a platform-auto-generated form rather than the one we authored, OR our form's rows were silently dropped between YAML and the org.

## Mission, in order

1. DIAGNOSE with evidence, not guesses: `pac solution clone --name DVerseCore --outputDirectory <temp>` (a read operation; the dverse-ci pac profile on this machine is yours to use for READ operations only: clone, list, org fetch. Imports, deletes, and any write to the org are FORBIDDEN and belong to the seat). The clone contains what the org actually holds: every form on dv_matter (ours, the auto-generated one, or both), and the app module the seat created in the portal (Matter App, appid 0e33cdd1-8597-f111-b8dc-70a8a59a66f9). Diff the org's form(s) against our `FormXml/main/dv_matter_main.yml`. State which hypothesis was true.
2. FIX our form source so the form the app actually renders carries all four fields (dv_name, dv_matternumber, dv_openedon, ownerid). If the org has two main forms, converge to ONE authored in source: ours, corrected with whatever structural elements the platform's own form has and ours lacks (mirror the platform's form XML; it is the ground truth for what renders). If form ids must match the one the app binds to, adopt the platform form's id into our source and say so.
3. TRANSCRIBE the app definition into the repo: the AppModule (and its sitemap) from the clone into `demo-solution/appmodules/` (or the folder name pac's reader actually consumes; decompile SolutionPackagerLib's AppModule processor for the folder and file shapes, precedent in every gate's WHY comments). Add the needed `solutioncomponents.yml` path entries; lesson 2 says subfolders need their own entries, PROVE presence in the packed customizations.xml.
4. VERIFY: pack exit 0 with the app module AND the corrected form present in the packed zip (paste the relevant blocks); unpack round-trip; all offline gates exit 0; full suite green (baseline 156, zero skips). The seat imports and drives the UI at grading; you do not.

## Owned files
- demo-solution/appmodules/** (new; exact folder name per your decompilation finding)
- demo-solution/entities/dv_matter/FormXml/**
- demo-solution/solutions/DVerseCore/solutioncomponents.yml and rootcomponents.yml (entries only)

Forbidden: everything else. Org writes forbidden absolutely. The harness, workflows, docs, other demo-solution paths are read-only.

## Done means
Committed "Slice 4.6t:" with DDomingo author flags. Report: the diagnosis with evidence (which hypothesis, proven how), files, decompilation citation for the appmodule shapes, verbatim pack/gate/suite outputs, packed-zip evidence blocks, commit hash, assumptions.
