namespace LevelingCompanion.Tests;

/// <summary>
///     2026-10-07: "change the gysahl green icon on the server bar for the exact same icon but without background": the game's item
///     icon with its grey tile cleared from the edges in, and only the greens' own shape kept (stray specks of the tile go).
/// </summary>
public sealed class GreensIconTests
{
    private static readonly byte[] Grey = [66, 69, 66, 255], Green = [27, 169, 173, 255], Dark = [40, 70, 30, 255]; // B, G, R, A

    /// <summary>A picture from rows of letters: . grey tile, g green, d dark green shading.</summary>
    private static (byte[] Px, int W) Picture(params string[] rows) =>
        (rows.SelectMany(row => row.SelectMany(c => c switch { 'g' => Green, 'd' => Dark, _ => Grey })).ToArray(), rows[0].Length);

    private static byte Alpha((byte[] Px, int W) p, int x, int y) => p.Px[(y * p.W + x) * 4 + 3];

    private static (byte[] Px, int W) Cleared(params string[] rows)
    {
        var p = Picture(rows);
        GreensIcon.ClearTile(p.Px, p.W, rows.Length);
        return p;
    }

    [Fact]
    public void GivenTheGreyTile_ExpectItClearedAndTheGreensKept()
    {
        var p = Cleared(".....", ".ggg.", ".gdg.", ".ggg.", ".....");
        Assert.Equal(0, Alpha(p, 0, 0));
        Assert.Equal(0, Alpha(p, 4, 2));
        Assert.Equal(255, Alpha(p, 1, 1));
        Assert.Equal(255, Alpha(p, 2, 2)); // the greens' dark shading: coloured, kept
    }

    [Fact]
    public void GivenAGreyPixelInsideTheGreens_ExpectItKept() =>
        // Out of the tile's reach: part of the picture.
        Assert.Equal(255, Alpha(Cleared(".....", ".ggg.", ".g.g.", ".ggg.", "....."), 2, 2));

    [Fact]
    public void GivenAStraySpeckApartFromTheGreens_ExpectItGone() =>
        // A tinted bit of the tile that the grey test missed: not the greens' shape.
        Assert.Equal(0, Alpha(Cleared("......", ".ggg..", ".ggg..", ".ggg..", "....g.", "......"), 4, 4));
}
