# Demo solution

Empty until wave 4. This is the solution the agent builds UNDER the harness, and
the receipts it produces are the proof that the gates are real.

Layout is the YAML source control format, which is forced rather than chosen:
canvas app .msapp files are supported only in that format, and canvas apps are
in scope per D11.

    solutions/DVerseCore/      solution.yml, solutioncomponents.yml,
                               rootcomponents.yml, missingdependencies.yml
    publishers/dversepublisher/publisher.yml   CustomizationPrefix: dv
    entities/dv_*/             tables, attributes, formxml, savedqueries
    entityrelationships/       including the 1:N to SharePointDocumentLocation
    canvasapps/                .msapp, wave 5

Nothing here is authored by hand as a shortcut. Every artifact is produced by a
Claude model working under the gates, which is the point of the exercise.
