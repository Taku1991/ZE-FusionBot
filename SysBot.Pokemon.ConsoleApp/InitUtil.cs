using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using SysBot.Pokemon;

namespace SysBot.Pokemon.ConsoleApp;

public static class InitUtil
{
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

        SpriteUtil.Initialize(sav);
        StreamSettings.CreateSpriteFile = (pk, fn) =>
        {
            var png = pk.Sprite();
            png.Save(fn);
        };
    }
}
