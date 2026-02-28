using System;
using System.Net.Http;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using SysBot.Pokemon;
using SysBot.Pokemon.Helpers;

namespace SysBot.Pokemon.ConsoleApp;

public static class InitUtil
{
    private static readonly HttpClient SpriteHttp = new();

    public static void InitializeStubs(ProgramMode mode)
    {
        SaveFile sav = mode switch
        {
            ProgramMode.SWSH => new SAV8SWSH(),
            ProgramMode.BDSP => new SAV8BS(),
            ProgramMode.LA  => new SAV8LA(),
            ProgramMode.SV   => new SAV9SV(),
            ProgramMode.LGPE => new SAV7b(),
            ProgramMode.PLZA => new SAV9ZA(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(mode)),
        };

        try
        {
            SpriteUtil.Initialize(sav);
            StreamSettings.CreateSpriteFile = (pk, fn) =>
            {
                var png = pk.Sprite();
                png.Save(fn);
            };
        }
        catch (Exception ex)
        {
            SysBot.Base.LogUtil.LogError($"Sprite-Initialisierung fehlgeschlagen (läuft headless ohne GDI+?): {ex.Message}", "InitUtil");
            SysBot.Base.LogUtil.LogInfo("Sprite-Fallback aktiv: Lade Sprite via URL (kein GDI+ verfügbar).", "InitUtil");

            // Fallback: URL-basierter Download identisch zu den Discord-Embeds
            StreamSettings.CreateSpriteFile = (pk, fn) =>
            {
                try
                {
                    bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
                    string url = TradeExtensions<PK9>.PokeImg(pk, canGmax, false);
                    byte[] bytes = SpriteHttp.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(fn, bytes);
                }
                catch (Exception dlEx)
                {
                    SysBot.Base.LogUtil.LogError($"Sprite-Download fehlgeschlagen: {dlEx.Message}", "InitUtil");
                    System.IO.File.WriteAllBytes(fn, StreamSettings.BlackPixel);
                }
            };
        }
    }
}
