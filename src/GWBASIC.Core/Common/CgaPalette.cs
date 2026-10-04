namespace GWBASIC.Core.Common;

/// <summary>
/// Standard 16-color CGA/EGA palette definitions.
/// </summary>
public static class CgaPalette
{
    public record ColorEntry(int Index, string Name, int R, int G, int B, ConsoleColor ConsoleColor);

    public static readonly ColorEntry[] Colors =
    [
        new(0,  "Black",        0x00, 0x00, 0x00, ConsoleColor.Black),
        new(1,  "Blue",         0x00, 0x00, 0xAA, ConsoleColor.DarkBlue),
        new(2,  "Green",        0x00, 0xAA, 0x00, ConsoleColor.DarkGreen),
        new(3,  "Cyan",         0x00, 0xAA, 0xAA, ConsoleColor.DarkCyan),
        new(4,  "Red",          0xAA, 0x00, 0x00, ConsoleColor.DarkRed),
        new(5,  "Magenta",      0xAA, 0x00, 0xAA, ConsoleColor.DarkMagenta),
        new(6,  "Brown",        0xAA, 0x55, 0x00, ConsoleColor.DarkYellow),
        new(7,  "Light Gray",   0xAA, 0xAA, 0xAA, ConsoleColor.Gray),
        new(8,  "Dark Gray",    0x55, 0x55, 0x55, ConsoleColor.DarkGray),
        new(9,  "Light Blue",   0x55, 0x55, 0xFF, ConsoleColor.Blue),
        new(10, "Light Green",  0x55, 0xFF, 0x55, ConsoleColor.Green),
        new(11, "Light Cyan",   0x55, 0xFF, 0xFF, ConsoleColor.Cyan),
        new(12, "Light Red",    0xFF, 0x55, 0x55, ConsoleColor.Red),
        new(13, "Light Magenta",0xFF, 0x55, 0xFF, ConsoleColor.Magenta),
        new(14, "Yellow",       0xFF, 0xFF, 0x55, ConsoleColor.Yellow),
        new(15, "Bright White", 0xFF, 0xFF, 0xFF, ConsoleColor.White)
    ];

    public static ColorEntry GetColor(int index)
    {
        int clamped = (index % 16 + 16) % 16;
        return Colors[clamped];
    }
}
