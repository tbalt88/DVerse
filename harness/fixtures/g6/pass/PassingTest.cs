namespace Fixture.Pass;

/// <summary>
/// G6 fixture: one trivial passing test. Exists so BuildAndTestsGate's
/// integration tests can run the real "dotnet test" against a project that
/// is known-green, without depending on any real DVerse plugin project
/// existing yet.
/// </summary>
public sealed class PassingTest
{
    [Fact]
    public void True_is_true()
    {
        Assert.True(true);
    }
}
