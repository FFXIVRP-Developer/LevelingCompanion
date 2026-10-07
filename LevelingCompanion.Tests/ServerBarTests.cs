namespace LevelingCompanion.Tests;

/// <summary>
///     The chocobo in the server info bar shows only where it can be summoned (user, 2026-10-07: "the chocobo icon actually should
///     only show in areas it can be summoned"; it was a sleeping chocobo everywhere else): field areas, or while it is out.
/// </summary>
public sealed class ServerBarTests
{
    [Fact]
    public void GivenAFieldArea_ExpectTheChocobo() => Assert.True(ServerBar.Shows(obtained: true, summoned: false, inField: true));

    [Fact]
    public void GivenItIsOut_ExpectTheChocobo() => Assert.True(ServerBar.Shows(obtained: true, summoned: true, inField: true));

    [Fact]
    public void GivenACityAnInnOrADuty_ExpectNoChocobo() => Assert.False(ServerBar.Shows(obtained: true, summoned: false, inField: false));

    [Fact]
    public void GivenNoCompanionYet_ExpectNothing() => Assert.False(ServerBar.Shows(obtained: false, summoned: false, inField: true));
}
