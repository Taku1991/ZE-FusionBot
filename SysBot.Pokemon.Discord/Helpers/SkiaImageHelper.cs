using Discord;
using PKHeX.Core;
using SkiaSharp;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Cross-platform image helper using SkiaSharp.
/// Replaces System.Drawing / GDI+ for Linux compatibility.
/// </summary>
public static class SkiaImageHelper
{
    private static readonly HttpClient Http = new();

    // Pictocode → National Pokédex number (same set as EmbedHelper)
    private static readonly Dictionary<Pictocodes, int> PictocodeSpeciesId = new()
    {
        [Pictocodes.Pikachu]    = 25,
        [Pictocodes.Eevee]      = 133,
        [Pictocodes.Bulbasaur]  = 1,
        [Pictocodes.Charmander] = 4,
        [Pictocodes.Squirtle]   = 7,
        [Pictocodes.Pidgey]     = 16,
        [Pictocodes.Caterpie]   = 10,
        [Pictocodes.Rattata]    = 19,
        [Pictocodes.Jigglypuff] = 39,
        [Pictocodes.Diglett]    = 50,
    };

    private static string PictocodeUrl(Pictocodes code)
    {
        int id = PictocodeSpeciesId.TryGetValue(code, out int n) ? n : 25;
        return $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
    }

    private static string GetImageFolderPath()
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Downloads a PNG from a URL and decodes it. Caller must dispose.</summary>
    public static async Task<SKBitmap?> LoadFromUrlAsync(string url)
    {
        try
        {
            byte[] bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            var bmp = SKBitmap.Decode(bytes);
            if (bmp == null)
                Console.WriteLine($"SkiaImageHelper: Failed to decode image from {url}");
            return bmp;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SkiaImageHelper: Error loading {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves an SKBitmap to a PNG file in the Images/ folder.
    /// Returns the full path.
    /// </summary>
    public static string SaveToFile(SKBitmap bitmap)
    {
        string path = Path.Combine(GetImageFolderPath(), $"img_{Guid.NewGuid():N}.png");
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        encoded.SaveTo(fs);
        return path;
    }

    /// <summary>
    /// Calculates the dominant (most saturated+visible) non-transparent color from an SKBitmap.
    /// </summary>
    public static (int R, int G, int B) GetDominantColor(SKBitmap bmp)
    {
        try
        {
            var counts = new Dictionary<uint, int>(1024);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    SKColor c = bmp.GetPixel(x, y);
                    if (c.Alpha < 128) continue;

                    float brightness = (c.Red + c.Green + c.Blue) / (3f * 255f);
                    if (brightness > 0.9f) continue;

                    // HSV-style saturation proxy
                    int maxC = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
                    int minC = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                    int sat = maxC == 0 ? 0 : (int)((maxC - minC) / (float)maxC * 100);
                    int bri = (int)(brightness * 100);
                    int weight = sat + bri;

                    // Quantize to reduce colour space
                    uint key = ((uint)(c.Red / 10 * 10) << 16)
                             | ((uint)(c.Green / 10 * 10) << 8)
                             | (uint)(c.Blue / 10 * 10);

                    if (!counts.TryAdd(key, weight))
                        counts[key] += weight;
                }
            }

            if (counts.Count == 0)
                return (255, 255, 255);

            uint dominant = 0;
            int max = 0;
            foreach (var kv in counts)
            {
                if (kv.Value > max) { max = kv.Value; dominant = kv.Key; }
            }
            return ((int)(dominant >> 16 & 0xFF), (int)(dominant >> 8 & 0xFF), (int)(dominant & 0xFF));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SkiaImageHelper.GetDominantColor: {ex.Message}");
            return (255, 255, 255);
        }
    }

    /// <summary>
    /// Returns a new SKBitmap with <paramref name="base"/> drawn first, then the ball
    /// sprite (fetched from <paramref name="ballUrl"/>) composited at the bottom-right.
    /// Always returns a valid bitmap even if the ball fails to load. Caller must dispose.
    /// </summary>
    public static async Task<SKBitmap> OverlayBallAsync(SKBitmap baseBmp, string ballUrl)
    {
        using var ballBmp = await LoadFromUrlAsync(ballUrl).ConfigureAwait(false);

        var result = new SKBitmap(baseBmp.Width, baseBmp.Height);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(baseBmp, 0, 0);

        if (ballBmp != null)
        {
            int bx = baseBmp.Width - ballBmp.Width;
            int by = baseBmp.Height - ballBmp.Height;
            canvas.DrawBitmap(ballBmp, bx, by);
        }

        return result;
    }

    /// <summary>
    /// Composites the species sprite centred inside the egg sprite.
    /// Returns a 128×128 SKBitmap. Caller must dispose.
    /// Throws on download failure.
    /// </summary>
    public static async Task<SKBitmap> CompositeEggWithSpeciesAsync(string eggUrl, string speciesUrl)
    {
        using var eggBmp = await LoadFromUrlAsync(eggUrl).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not load egg image: {eggUrl}");
        using var speciesBmp = await LoadFromUrlAsync(speciesUrl).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not load species image: {speciesUrl}");

        // Scale species to fit inside egg
        float scale = Math.Min((float)eggBmp.Width / speciesBmp.Width,
                               (float)eggBmp.Height / speciesBmp.Height);
        int sw = (int)(speciesBmp.Width  * scale);
        int sh = (int)(speciesBmp.Height * scale);
        int sx = (eggBmp.Width  - sw) / 2;
        int sy = (eggBmp.Height - sh) / 2;

        var step1 = new SKBitmap(eggBmp.Width, eggBmp.Height);
        using (var canvas = new SKCanvas(step1))
        {
            canvas.DrawBitmap(eggBmp, 0, 0);
            canvas.DrawBitmap(speciesBmp, new SKRect(sx, sy, sx + sw, sy + sh));
        }

        // Fit result into 128×128 canvas
        const int finalSize = 128;
        float finalScale = Math.Min((float)finalSize / step1.Width,
                                    (float)finalSize / step1.Height);
        int fw = (int)(step1.Width  * finalScale);
        int fh = (int)(step1.Height * finalScale);
        int fx = (finalSize - fw) / 2;
        int fy = (finalSize - fh) / 2;

        var final = new SKBitmap(finalSize, finalSize);
        using (var canvas = new SKCanvas(final))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(step1, new SKRect(fx, fy, fx + fw, fy + fh));
        }
        step1.Dispose();
        return final;
    }

    /// <summary>
    /// Creates a composite sprite image for an LGPE pictocode (3 Pokémon side-by-side)
    /// using PokeAPI sprites. Returns the local file path and a ready Discord Embed.
    /// The caller should schedule file deletion after sending.
    /// </summary>
    public static async Task<(string FilePath, Embed Embed)> CreateLGCodeSpriteAsync(List<Pictocodes> lgcode)
    {
        const int slotW = 80, slotH = 80, padding = 4;
        int count = lgcode.Count;
        int totalW = count * slotW + (count - 1) * padding;

        var bitmaps = new SKBitmap?[count];
        for (int i = 0; i < count; i++)
            bitmaps[i] = await LoadFromUrlAsync(PictocodeUrl(lgcode[i])).ConfigureAwait(false);

        var composite = new SKBitmap(totalW, slotH);
        using (var canvas = new SKCanvas(composite))
        {
            canvas.Clear(SKColors.Transparent);
            for (int i = 0; i < count; i++)
            {
                if (bitmaps[i] == null) continue;
                int x = i * (slotW + padding);
                canvas.DrawBitmap(bitmaps[i], new SKRect(x, 0, x + slotW, slotH));
            }
        }
        foreach (var b in bitmaps) b?.Dispose();

        string filePath = SaveToFile(composite);
        composite.Dispose();

        string title = count >= 3
            ? $"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}"
            : string.Join(", ", lgcode);
        string fileName = Path.GetFileName(filePath);
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithImageUrl($"attachment://{fileName}")
            .Build();

        return (filePath, embed);
    }

    /// <summary>
    /// Creates a combined sprite image for a batch trade using PokeAPI / Showdown sprite URLs.
    /// Returns the local file path. The caller should schedule file deletion after sending.
    /// </summary>
    public static async Task<string> CreateBatchSpriteAsync<T>(List<T> pokemonList)
        where T : PKM, new()
    {
        const int spriteW = 91, spriteH = 75, spacing = 3;
        int totalW = pokemonList.Count * spriteW + (pokemonList.Count - 1) * spacing;

        var bitmaps = new SKBitmap?[pokemonList.Count];
        for (int i = 0; i < pokemonList.Count; i++)
        {
            var pk = pokemonList[i];
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            string url = TradeExtensions<T>.PokeImg(pk, canGmax, pk.IsEgg, null);
            bitmaps[i] = await LoadFromUrlAsync(url).ConfigureAwait(false);
        }

        var composite = new SKBitmap(totalW, spriteH);
        using (var canvas = new SKCanvas(composite))
        {
            canvas.Clear(SKColors.Transparent);
            for (int i = 0; i < bitmaps.Length; i++)
            {
                if (bitmaps[i] == null) continue;
                int x = i * (spriteW + spacing);
                canvas.DrawBitmap(bitmaps[i], new SKRect(x, 0, x + spriteW, spriteH));
            }
        }
        foreach (var b in bitmaps) b?.Dispose();

        string path = Path.Combine(GetImageFolderPath(), $"batch_{DateTime.UtcNow.Ticks}.png");
        using var encoded = composite.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        encoded.SaveTo(fs);
        composite.Dispose();
        return path;
    }
}
