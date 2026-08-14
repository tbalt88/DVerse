using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Cli;

/// <summary>
/// The catalogue of gates the CLI runs.
/// <para>
/// Explicit rather than reflection-discovered, deliberately. A governance tool
/// must be able to state exactly which rules it enforced, and a reflection scan
/// silently changes that set whenever an assembly changes. An explicit list also
/// means a gate cannot be dropped by accident: removing one is a visible diff in
/// this file, not an invisible consequence of a rename.
/// </para>
/// </summary>
public static class GateRegistry
{
    /// <summary>Every gate, in stable id order.</summary>
    public static IReadOnlyList<IGate> All { get; } =
    [
        new WellFormednessGate(),               // G1
        new PublisherPrefixGate(),              // G2
        new DependencyIntegrityGate(),          // G3
        new DocumentLocationCardinalityGate(),  // G4
        new BuildAndTestsGate(),                // G6
        new PowerAppsCheckerGate(),             // G7
        new RootComponentSourceGate(),          // G8
        new SolutionComponentPathGate(),        // G9
        new YamlLayoutGate(),                   // G10
        new CanvasYamlGate(),                   // G11
        new StructuralDiffGate(baselineRoot: null) // G12: no baseline by default, honest SKIP (ruling 1);
                                                    // the CLI substitutes a baseline-carrying instance
                                                    // for this one when --baseline is supplied.
    ];

    /// <summary>Gates that need no tenant. These run everywhere, including fork pull requests.</summary>
    public static IReadOnlyList<IGate> Offline { get; } =
        All.Where(g => !g.RequiresTenant).ToList();

    /// <summary>Gates requiring a live Dataverse connection.</summary>
    public static IReadOnlyList<IGate> Online { get; } =
        All.Where(g => g.RequiresTenant).ToList();

    public static IReadOnlyList<IGate> Select(bool includeOnline) =>
        includeOnline ? All : Offline;
}
