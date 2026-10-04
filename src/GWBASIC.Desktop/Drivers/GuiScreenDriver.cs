using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;

namespace GWBASIC.Desktop.Drivers;

public class GuiScreenDriver : IScreenDriver
{
    public int Width { get; private set; } = 80;
    public int Height { get; private set; } = 25;
    public int Mode { get; private set; } = 0;
    public int ForegroundColor { get; private set; } = 7;
    public int BackgroundColor { get; private set; } = 0;
    public int CursorRow { get; private set; } = 1;
    public int CursorCol { get; private set; } = 1;
    public bool CursorVisible { get; private set; } = true;
    public bool KeyRowVisible { get; set; } = true;

    // Buffer for text mode
    public readonly char[,] CharBuffer;
    public readonly byte[,] FgColorBuffer;
    public readonly byte[,] BgColorBuffer;

    // Buffer for graphics mode (640x400 internal canvas)
    public readonly Bitmap GraphicBitmap;
    public readonly Graphics GraphicContext;
    private readonly object _lock = new();

    public string[] KeyLabels = new string[10];

    public GuiScreenDriver()
    {
        CharBuffer = new char[25, 80];
        FgColorBuffer = new byte[25, 80];
        BgColorBuffer = new byte[25, 80];

        GraphicBitmap = new Bitmap(640, 400);
        GraphicContext = Graphics.FromImage(GraphicBitmap);
        GraphicContext.InterpolationMode = InterpolationMode.NearestNeighbor;
        GraphicContext.PixelOffsetMode = PixelOffsetMode.Half;

        Cls();
    }

    public void SetMode(int mode)
    {
        lock (_lock)
        {
            Mode = mode;
            if (mode == 1) SetWidth(40);
            else SetWidth(80);
            Cls();
        }
    }

    public void SetWidth(int width)
    {
        lock (_lock)
        {
            Width = width == 40 ? 40 : 80;
            Cls();
        }
    }

    public void SetColors(int foreground, int background, int border = 0)
    {
        ForegroundColor = foreground % 16;
        BackgroundColor = background % 16;
    }

    public void Locate(int row, int col, bool? cursorVisible = null)
    {
        lock (_lock)
        {
            CursorRow = Math.Clamp(row, 1, Height);
            CursorCol = Math.Clamp(col, 1, Width);
            if (cursorVisible.HasValue) CursorVisible = cursorVisible.Value;
        }
    }

    public void Cls(int? mode = null)
    {
        lock (_lock)
        {
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    CharBuffer[r, c] = ' ';
                    FgColorBuffer[r, c] = (byte)ForegroundColor;
                    BgColorBuffer[r, c] = (byte)BackgroundColor;
                }
            }

            var bgEntry = CgaPalette.GetColor(BackgroundColor);
            GraphicContext.Clear(Color.FromArgb(bgEntry.R, bgEntry.G, bgEntry.B));
            CursorRow = 1;
            CursorCol = 1;
        }
    }

    public void Write(string text)
    {
        lock (_lock)
        {
            foreach (char ch in text)
            {
                if (ch == '\r')
                {
                    CursorCol = 1;
                    continue;
                }
                if (ch == '\n')
                {
                    WriteLineInternal();
                    continue;
                }

                int r = CursorRow - 1;
                int c = CursorCol - 1;
                if (r >= 0 && r < Height && c >= 0 && c < Width)
                {
                    CharBuffer[r, c] = ch;
                    FgColorBuffer[r, c] = (byte)ForegroundColor;
                    BgColorBuffer[r, c] = (byte)BackgroundColor;
                }

                CursorCol++;
                if (CursorCol > Width)
                {
                    WriteLineInternal();
                }
            }
        }
    }

    public void WriteLine(string text = "")
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Write(text);
            }
            WriteLineInternal();
        }
    }

    private void WriteLineInternal()
    {
        CursorCol = 1;
        CursorRow++;
        int maxRow = KeyRowVisible ? Height - 1 : Height;
        if (CursorRow > maxRow)
        {
            ScrollUp();
            CursorRow = maxRow;
        }
    }

    public void ScrollUp()
    {
        int maxRow = KeyRowVisible ? Height - 2 : Height - 1;
        for (int r = 0; r < maxRow; r++)
        {
            for (int c = 0; c < Width; c++)
            {
                CharBuffer[r, c] = CharBuffer[r + 1, c];
                FgColorBuffer[r, c] = FgColorBuffer[r + 1, c];
                BgColorBuffer[r, c] = BgColorBuffer[r + 1, c];
            }
        }
        for (int c = 0; c < Width; c++)
        {
            CharBuffer[maxRow, c] = ' ';
            FgColorBuffer[maxRow, c] = (byte)ForegroundColor;
            BgColorBuffer[maxRow, c] = (byte)BackgroundColor;
        }
    }

    public void SetKeyLabels(string[] labels)
    {
        KeyLabels = labels;
    }

    public void PSet(int x, int y, int color)
    {
        lock (_lock)
        {
            var colEntry = CgaPalette.GetColor(color);
            var col = Color.FromArgb(colEntry.R, colEntry.G, colEntry.B);
            int scaleY = (Mode == 1 || Mode == 2) ? 2 : 1;
            int px = Math.Clamp(x, 0, 639);
            int py = Math.Clamp(y * scaleY, 0, 399);

            GraphicBitmap.SetPixel(px, py, col);
            if (scaleY > 1 && py + 1 < 400)
            {
                GraphicBitmap.SetPixel(px, py + 1, col);
            }
        }
    }

    public void PReset(int x, int y) => PSet(x, y, BackgroundColor);

    public int Point(int x, int y)
    {
        lock (_lock)
        {
            int scaleY = (Mode == 1 || Mode == 2) ? 2 : 1;
            int px = Math.Clamp(x, 0, 639);
            int py = Math.Clamp(y * scaleY, 0, 399);
            var col = GraphicBitmap.GetPixel(px, py);

            for (int i = 0; i < 16; i++)
            {
                var entry = CgaPalette.GetColor(i);
                if (entry.R == col.R && entry.G == col.G && entry.B == col.B)
                    return i;
            }
            return 0;
        }
    }

    public void Line(int x1, int y1, int x2, int y2, int color, bool box = false, bool boxFill = false, ushort style = 0xFFFF)
    {
        lock (_lock)
        {
            var colEntry = CgaPalette.GetColor(color);
            var col = Color.FromArgb(colEntry.R, colEntry.G, colEntry.B);
            int scaleY = (Mode == 1 || Mode == 2) ? 2 : 1;
            int sy1 = y1 * scaleY;
            int sy2 = y2 * scaleY;

            if (box)
            {
                int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                int minY = Math.Min(sy1, sy2), maxY = Math.Max(sy1, sy2);
                if (boxFill)
                {
                    using var brush = new SolidBrush(col);
                    GraphicContext.FillRectangle(brush, minX, minY, maxX - minX + 1, maxY - minY + scaleY);
                }
                else
                {
                    using var pen = new Pen(col, 1);
                    GraphicContext.DrawRectangle(pen, minX, minY, maxX - minX, maxY - minY);
                }
                return;
            }

            using var p = new Pen(col, 1);
            GraphicContext.DrawLine(p, x1, sy1, x2, sy2);
        }
    }

    public void Circle(int xc, int yc, int radius, int color, double startAngle = 0, double endAngle = 2 * Math.PI, double aspect = 1.0)
    {
        lock (_lock)
        {
            int steps = Math.Max(36, radius * 4);
            double angleStep = (2 * Math.PI) / steps;
            for (int i = 0; i <= steps; i++)
            {
                double a = i * angleStep;
                if (a >= startAngle && a <= endAngle)
                {
                    int px = xc + (int)Math.Round(radius * Math.Cos(a));
                    int py = yc - (int)Math.Round(radius * Math.Sin(a) * aspect);
                    PSet(px, py, color);
                }
            }
        }
    }

    public void Paint(int startX, int startY, int paintColor, int boundaryColor)
    {
        lock (_lock)
        {
            var targetCol = CgaPalette.GetColor(paintColor);
            var boundCol = CgaPalette.GetColor(boundaryColor);
            var targetColor = Color.FromArgb(targetCol.R, targetCol.G, targetCol.B);
            var boundColor = Color.FromArgb(boundCol.R, boundCol.G, boundCol.B);

            if (startX < 0 || startX >= 640 || startY < 0 || startY >= 400) return;
            var cur = GraphicBitmap.GetPixel(startX, startY);
            if (cur.ToArgb() == boundColor.ToArgb() || cur.ToArgb() == targetColor.ToArgb()) return;

            var stack = new Stack<Point>();
            stack.Push(new Point(startX, startY));

            while (stack.Count > 0)
            {
                var pt = stack.Pop();
                if (pt.X < 0 || pt.X >= 640 || pt.Y < 0 || pt.Y >= 400) continue;
                var pixel = GraphicBitmap.GetPixel(pt.X, pt.Y);
                if (pixel.ToArgb() == boundColor.ToArgb() || pixel.ToArgb() == targetColor.ToArgb()) continue;

                GraphicBitmap.SetPixel(pt.X, pt.Y, targetColor);
                stack.Push(new Point(pt.X + 1, pt.Y));
                stack.Push(new Point(pt.X - 1, pt.Y));
                stack.Push(new Point(pt.X, pt.Y + 1));
                stack.Push(new Point(pt.X, pt.Y - 1));
            }
        }
    }

    public string ReadLine(int row)
    {
        lock (_lock)
        {
            int r = row - 1;
            if (r < 0 || r >= Height) return "";
            var sb = new StringBuilder();
            for (int c = 0; c < Width; c++)
            {
                sb.Append(CharBuffer[r, c]);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
