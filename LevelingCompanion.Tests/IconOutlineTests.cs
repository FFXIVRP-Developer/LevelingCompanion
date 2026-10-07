namespace LevelingCompanion.Tests;

/// <summary>2026-10-07: "add a black border to all the transparent server bar icons we created".</summary>
public sealed class IconOutlineTests
{
    /// <summary>A 5×5 picture, transparent but one gold pixel in the middle.</summary>
    private static byte[] Dot()
    {
        var px = new byte[5 * 5 * 4];
        var i = (2 * 5 + 2) * 4;
        (px[i], px[i + 1], px[i + 2], px[i + 3]) = (110, 190, 220, 255);
        return px;
    }

    private static (byte B, byte G, byte R, byte A) At(byte[] px, int x, int y) { var i = (y * 5 + x) * 4; return (px[i], px[i + 1], px[i + 2], px[i + 3]); }

    [Fact]
    public void GivenAShape_ExpectABlackRingAroundIt()
    {
        var px = Dot();
        IconOutline.Add(px, 5, 5, radius: 1);
        Assert.Equal((0, 0, 0, 255), At(px, 2, 1));
        Assert.Equal((0, 0, 0, 255), At(px, 3, 2));
    }

    [Fact]
    public void GivenAShape_ExpectItKeptAsItWas() => Assert.Equal((110, 190, 220, 255), At(Outlined(), 2, 2));

    [Fact]
    public void GivenFarFromTheShape_ExpectStillTransparent() => Assert.Equal(0, At(Outlined(), 0, 0).A);

    private static byte[] Outlined()
    {
        var px = Dot();
        IconOutline.Add(px, 5, 5, radius: 1);
        return px;
    }

    [Fact]
    public void GivenTheIconsSize_ExpectAboutAScreenPixel() =>
        // Drawn about a quarter of its size in the server bar: 80 px wide, a 3 px ring.
        Assert.Equal(3, IconOutline.RadiusFor(80));
}
