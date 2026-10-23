using UnityEngine;

// Fixed numbers shared by the whole game. One world unit = one game pixel.
public static class GameConfig
{
    public const int ScreenWidth = 384;
    public const int ScreenHeight = 216;
    public const int HalfWidth = ScreenWidth / 2;
    public const int HalfHeight = ScreenHeight / 2;
    public const float TickRate = 60f;
    public const float TickSeconds = 1f / TickRate;

    // Ground sits 28 px above the bottom edge of the screen.
    public const float GroundScreenOffset = 28f;
    public const float CameraY = HalfHeight - GroundScreenOffset;
    public const float StageHalfWidth = 320f;
    public const float WallMargin = 30f;
    public const float MaxFighterSeparation = ScreenWidth - 44f;

    public const int RoundsToWin = 2;
    public const int RoundSeconds = 99;
    public const int FinishHimTicks = 300;

    public static readonly Color P1Color = new Color(0.24f, 0.55f, 1f);
    public static readonly Color P2Color = new Color(1f, 0.24f, 0.24f);

    public static Color Hex(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }
}

// Sorting orders for everything drawn into the low-resolution render texture.
public static class Layers
{
    public const int StageBack = -100;
    public const int Stains = -6;
    public const int Shadows = -4;
    public const int Fighter = 10;
    public const int FighterFront = 12;
    public const int Gore = 20;
    public const int Blood = 25;
    public const int Sparks = 30;
    public const int Hud = 100;
    public const int Banner = 200;
    public const int Screen = 300;
    public const int Overlay = 800;
    public const int Fade = 900;
}
