namespace GWBASIC.Core.Drivers;

/// <summary>
/// Driver for screen output, text manipulation, cursor positioning, and CGA/EGA graphics.
/// </summary>
public interface IScreenDriver
{
    int Width { get; }
    int Height { get; }
    int Mode { get; }
    int ForegroundColor { get; }
    int BackgroundColor { get; }
    int CursorRow { get; } // 1-based
    int CursorCol { get; } // 1-based
    bool CursorVisible { get; }
    bool KeyRowVisible { get; set; }

    void SetMode(int mode);
    void SetWidth(int width);
    void SetColors(int foreground, int background, int border = 0);
    void Locate(int row, int col, bool? cursorVisible = null);
    void Cls(int? mode = null);
    void Write(string text);
    void WriteLine(string text = "");
    void ScrollUp();
    void SetKeyLabels(string[] labels);

    // Graphics primitives (Screens 1, 2, etc.)
    void PSet(int x, int y, int color);
    void PReset(int x, int y);
    int Point(int x, int y);
    void Line(int x1, int y1, int x2, int y2, int color, bool box = false, bool boxFill = false, ushort style = 0xFFFF);
    void Circle(int xc, int yc, int radius, int color, double startAngle = 0, double endAngle = 2 * Math.PI, double aspect = 1.0);
    void Paint(int x, int y, int paintColor, int boundaryColor);

    // Text buffer readback for screen editing / direct mode
    string ReadLine(int row);
}
