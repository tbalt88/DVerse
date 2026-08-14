using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// Shared fixture loading for the Diff test suite. Mirrors the fixture-root
/// walk every Gates test already uses (see ArtifactPathBaseTests.cs), aimed
/// at harness/fixtures/diff instead of harness/fixtures/gN, and adds a
/// literal-string parse helper for the smaller unit tests that do not need
/// a fixture file at all.
/// </summary>
internal static class DiffFixtures
{
    /// <summary>Parses a fixture file under harness/fixtures/diff/&lt;relativePath&gt; and returns its root node.</summary>
    public static YamlNode LoadRoot(string relativePath)
    {
        var path = Path.Combine(FixturesRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Diff fixture not found: {path}");

        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);
        return stream.Documents[0].RootNode;
    }

    /// <summary>Parses a literal YAML string and returns its root node, for tests that do not need a fixture file.</summary>
    public static YamlNode ParseRoot(string yaml)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);
        return stream.Documents[0].RootNode;
    }

    private static string FixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException($"Could not locate a 'fixtures' folder above {AppContext.BaseDirectory}.");

        var diffRoot = Path.Combine(dir.FullName, "fixtures", "diff");

        if (!Directory.Exists(diffRoot))
            throw new DirectoryNotFoundException($"Diff fixtures folder not found: {diffRoot}");

        return diffRoot;
    }
}
