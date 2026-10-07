using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>
///     The server bar's Gysahl Greens (user, 2026-10-07: "the exact same icon but without background"): the game's item icon with
///     its grey tile cleared, the grey pixels reached from the edges made transparent, then only the greens' own shape kept (the
///     largest one), so stray specks of the tile go.
/// </summary>
internal static class GreensIcon
{
    /// <summary>How far apart red, green and blue may be for a pixel to count as the grey tile (the greens are strongly coloured).</summary>
    private const int GreySpread = 28;

    /// <summary>The greens without their tile, made once from the game's icon (high resolution when there is one); null when unreadable.</summary>
    internal static IDalamudTextureWrap? Load(uint item)
    {
        uint icon = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(item)?.Icon ?? 0u;
        if (icon == 0)
            return null;
        string folder = $"ui/icon/{icon / 1000 * 1000:D6}/{icon:D6}";
        if ((Svc.Data.GetFile<TexFile>($"{folder}_hr1.tex") ?? Svc.Data.GetFile<TexFile>($"{folder}.tex")) is not { } tex)
            return null;
        (int w, int h) = (tex.Header.Width, tex.Header.Height);
        byte[] px = tex.ImageData.ToArray(); // B, G, R, A
        ClearTile(px, w, h);
        IconOutline.Add(px, w, h, IconOutline.RadiusFor(w));
        return Svc.Texture.CreateFromRaw(RawImageSpecification.Bgra32(w, h), px);
    }

    /// <summary>The grey tile cleared in place (BGRA), then everything but the largest remaining shape.</summary>
    internal static void ClearTile(byte[] px, int w, int h)
    {
        bool Tile(int p)
        {
            int b = px[p * 4], g = px[p * 4 + 1], r = px[p * 4 + 2];
            return px[p * 4 + 3] < 16 || System.Math.Max(r, System.Math.Max(g, b)) - System.Math.Min(r, System.Math.Min(g, b)) <= GreySpread;
        }
        // From every edge pixel: the connected tile pixels.
        foreach (int p in Fill(w, h, Edges(w, h), Tile))
            px[p * 4 + 3] = 0;
        // Only the largest shape left stays.
        bool[] seen = new bool[w * h];
        List<List<int>> shapes = [];
        for (int p = 0; p < w * h; p++)
            if (!seen[p] && px[p * 4 + 3] != 0)
                shapes.Add(Fill(w, h, [p], q => px[q * 4 + 3] != 0, seen));
        foreach (int p in shapes.OrderByDescending(s => s.Count).Skip(1).SelectMany(s => s))
            px[p * 4 + 3] = 0;
    }

    private static IEnumerable<int> Edges(int w, int h)
    {
        for (int x = 0; x < w; x++) { yield return x; yield return (h - 1) * w + x; }
        for (int y = 0; y < h; y++) { yield return y * w; yield return y * w + w - 1; }
    }

    /// <summary>The pixels connected (4 ways) to <paramref name="from" /> that pass <paramref name="take" />.</summary>
    private static List<int> Fill(int w, int h, IEnumerable<int> from, System.Func<int, bool> take, bool[]? seen = null)
    {
        seen ??= new bool[w * h];
        List<int> taken = [];
        Queue<int> queue = new();
        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x])
                return;
            seen[y * w + x] = true;
            queue.Enqueue(y * w + x);
        }
        foreach (int p in from)
            Visit(p % w, p / w);
        while (queue.TryDequeue(out int p))
        {
            if (!take(p))
                continue;
            taken.Add(p);
            (int x, int y) = (p % w, p / w);
            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
        }
        return taken;
    }
}
