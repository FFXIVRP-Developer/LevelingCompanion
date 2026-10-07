using System;

namespace LevelingCompanion;

/// <summary>
///     A black outline around a server bar icon's shape (user, 2026-10-07: "add a black border to all the transparent server bar
///     icons we created"): every transparent pixel within <c>radius</c> of the shape becomes opaque black; the shape stays as it is.
/// </summary>
internal static class IconOutline
{
    /// <summary>The ring for a texture this wide: drawn about a quarter of its size in the bar, so about one screen pixel.</summary>
    public static int RadiusFor(int width) => Math.Max(1, width / 24);

    /// <summary>The outline added in place (BGRA).</summary>
    public static void Add(byte[] px, int w, int h, int radius)
    {
        var solid = new bool[w * h];
        for (var p = 0; p < w * h; p++) solid[p] = px[p * 4 + 3] >= 128;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = y * w + x;
                if (solid[p] || !Near(solid, w, h, x, y, radius)) continue;
                (px[p * 4], px[p * 4 + 1], px[p * 4 + 2], px[p * 4 + 3]) = (0, 0, 0, 255);
            }
    }

    private static bool Near(bool[] solid, int w, int h, int x, int y, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h && solid[ny * w + nx]) return true;
            }
        return false;
    }
}
