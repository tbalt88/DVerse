namespace Fixture.Fail;

/// <summary>
/// G6 fixture: one deliberately failing test. Exists so BuildAndTestsGate's
/// integration tests can prove a Refuse against a real "dotnet test" run,
/// not just a synthetic string.
/// </summary>
public sealed class FailingTest
{
    [Fact]
    public void This_test_deliberately_fails()
    {
        Assert.Fail("deliberate failure for G6 fixture coverage");
    }
}
