using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;

namespace GWBASIC.ConsoleApp.Drivers;

public class ConsoleScreenDriver : IScreenDriver, IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    private static bool _vtSupported = false;
    private static bool _inAlternateBuffer = false;
    private static int _restored = 0;

    public static bool EnableVirtualTerminal()
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
            return false;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                {
                    if (GetConsoleMode(handle, out uint mode))
                    {
                        if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0 ||
                            SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING))
                        {
                            _vtSupported = true;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        _vtSupported = true;
        return true;
    }

    public static void EnterAlternateBuffer()
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
            return;

        _restored = 0;
        EnableVirtualTerminal();

        if (_vtSupported)
        {
            try
            {
                Console.Write("\x1b[?1049h");
                _inAlternateBuffer = true;
            }
            catch { }
        }
    }

    public static void ExitAlternateBuffer()
    {
        if (Interlocked.Exchange(ref _restored, 1) != 0)
            return;

        if (Console.IsOutputRedirected || Console.IsInputRedirected)
            return;

        try
        {
            Console.ResetColor();
            Console.CursorVisible = true;

            if (_inAlternateBuffer && _vtSupported)
            {
                _inAlternateBuffer = false;
                Console.Write("\x1b[?1049l");
            }
            else
            {
                Console.Clear();
            }
        }
        catch { }
    }

    public int Width { get; private set; } = 80;
    public int Height { get; private set; } = 25;
    public int Mode { get; private set; } = 0;
    public int ForegroundColor { get; private set; } = 7;
    public int BackgroundColor { get; private set; } = 0;

    private int _cursorRow = 1;
    private int _cursorCol = 1;
    public int CursorRow => _cursorRow;
    public int CursorCol => _cursorCol;
    public bool CursorVisible { get; private set; } = true;

    private bool _keyRowVisible = true;
    public bool KeyRowVisible
    {
        get => _keyRowVisible;
        set
        {
            _keyRowVisible = value;
            if (_keyRowVisible)
            {
                RenderKeyRow();
            }
            else
            {
                ClearKeyRow();
            }
        }
    }

    private string[] _keyLabels = new string[10];
    private readonly char[,] _charBuffer;
    private readonly bool _isRedirected;

    // Graphics canvas (640x400 internal resolution)
    public readonly object GraphicLock = new();
    public Bitmap GraphicBitmap { get; }
    public Graphics GraphicContext { get; }

    private readonly Font _fontMode1;
    private readonly Font _fontMode2;
    private readonly StringFormat _stringFormat;

    private Thread? _guiThread;
    private GraphicsWindow? _graphicsWindow;
    private readonly ManualResetEventSlim _windowReadyEvent = new(false);
    private ConsoleInputDriver? _inputDriver;
    private int _screen1Palette = 1; // 0 = green/red/brown, 1 = cyan/magenta/white

    public ConsoleScreenDriver()
    {
        _isRedirected = Console.IsOutputRedirected;
        if (!_isRedirected)
        {
            try
            {
                if (Console.WindowWidth >= 80 && Console.WindowHeight >= 25)
                {
                    Width = 80;
                    Height = 25;
                }
                Console.CursorVisible = true;
            }
            catch { }
        }

        _charBuffer = new char[Height, Width];
        for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                _charBuffer[r, c] = ' ';

        GraphicBitmap = new Bitmap(640, 400, PixelFormat.Format32bppArgb);
        GraphicContext = Graphics.FromImage(GraphicBitmap);
        GraphicContext.InterpolationMode = InterpolationMode.NearestNeighbor;
        GraphicContext.PixelOffsetMode = PixelOffsetMode.Half;

        _fontMode1 = new Font("Consolas", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        _fontMode2 = new Font("Consolas", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        _stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        Cls();
    }

    public void AttachInputDriver(ConsoleInputDriver inputDriver)
    {
        _inputDriver = inputDriver;
    }

    public void SetMode(int mode)
    {
        Mode = mode;
        if (mode == 1)
        {
            SetWidth(40);
            ShowGraphicsWindow(1);
        }
        else if (mode == 2)
        {
            SetWidth(80);
            ShowGraphicsWindow(2);
        }
        else
        {
            SetWidth(80);
            HideGraphicsWindow();
        }
    }

    public void SetWidth(int width)
    {
        Width = width == 40 ? 40 : 80;
        Cls();
    }

    public void SetColors(int foreground, int background, int border = 0)
    {
        ForegroundColor = foreground % 16;
        BackgroundColor = background % 16;
        if (Mode == 1 && background is 0 or 1)
        {
            _screen1Palette = background; // In CGA SCREEN 1, 2nd arg to COLOR sets palette
        }

        if (!_isRedirected)
        {
            try
            {
                Console.ForegroundColor = CgaPalette.GetColor(ForegroundColor).ConsoleColor;
                Console.BackgroundColor = CgaPalette.GetColor(BackgroundColor).ConsoleColor;
            }
            catch { }
        }
    }

    public void Locate(int row, int col, bool? cursorVisible = null)
    {
        _cursorRow = Math.Clamp(row, 1, Height);
        _cursorCol = Math.Clamp(col, 1, Width);

        if (!_isRedirected)
        {
            try
            {
                Console.SetCursorPosition(_cursorCol - 1, _cursorRow - 1);
                if (cursorVisible.HasValue)
                {
                    CursorVisible = cursorVisible.Value;
                    Console.CursorVisible = CursorVisible;
                }
            }
            catch { }
        }
    }

    public void Cls(int? mode = null)
    {
        lock (GraphicLock)
        {
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                    _charBuffer[r, c] = ' ';

            _cursorRow = 1;
            _cursorCol = 1;

            var bgCol = CgaPalette.GetColor(BackgroundColor);
            GraphicContext.Clear(Color.FromArgb(bgCol.R, bgCol.G, bgCol.B));
        }

        if (KeyRowVisible && Mode == 0)
        {
            RenderKeyRow();
        }

        if (!_isRedirected)
        {
            try
            {
                Console.Clear();
                if (KeyRowVisible && Mode == 0)
                {
                    RenderKeyRow();
                }
                Console.SetCursorPosition(0, 0);
            }
            catch { }
        }
    }

    public void Write(string text)
    {
        if (!_isRedirected)
        {
            try
            {
                int expectedLeft = Math.Clamp(_cursorCol - 1, 0, Width - 1);
                int expectedTop = Math.Clamp(_cursorRow - 1, 0, Height - 1);
                if (Console.CursorLeft != expectedLeft || Console.CursorTop != expectedTop)
                {
                    Console.SetCursorPosition(expectedLeft, expectedTop);
                }
            }
            catch { }
        }

        foreach (char ch in text)
        {
            if (ch == '\r')
            {
                _cursorCol = 1;
                if (!_isRedirected)
                {
                    try { Console.CursorLeft = 0; } catch { }
                }
                continue;
            }
            if (ch == '\n')
            {
                WriteLine();
                continue;
            }

            int r = Math.Clamp(_cursorRow - 1, 0, Height - 1);
            int c = Math.Clamp(_cursorCol - 1, 0, Width - 1);
            _charBuffer[r, c] = ch;

            // Render character in graphics window if in graphics mode
            if (Mode != 0)
            {
                lock (GraphicLock)
                {
                    int cellW = Mode == 1 ? 16 : 8;
                    int cellH = 16;
                    var rect = new RectangleF(c * cellW, r * cellH, cellW, cellH);

                    var bgCol = CgaPalette.GetColor(BackgroundColor);
                    using var bgBrush = new SolidBrush(Color.FromArgb(bgCol.R, bgCol.G, bgCol.B));
                    GraphicContext.FillRectangle(bgBrush, rect);

                    if (ch != ' ')
                    {
                        var fgCol = GetScreenColor(ForegroundColor);
                        using var fgBrush = new SolidBrush(Color.FromArgb(fgCol.R, fgCol.G, fgCol.B));
                        var font = Mode == 1 ? _fontMode1 : _fontMode2;
                        GraphicContext.DrawString(ch.ToString(), font, fgBrush, rect, _stringFormat);
                    }
                }
            }

            Console.Write(ch);
            _cursorCol++;
            if (_cursorCol > Width)
            {
                WriteLine();
            }
        }
    }

    public void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            Write(text);
        }
        Console.WriteLine();
        _cursorCol = 1;

        int maxRow = (KeyRowVisible && Mode == 0) ? Height - 2 : Height - 1;
        if (_cursorRow >= maxRow + 1)
        {
            ScrollUp();
            _cursorRow = maxRow + 1;
        }
        else
        {
            _cursorRow++;
        }

        if (!_isRedirected)
        {
            try
            {
                Console.SetCursorPosition(0, Math.Clamp(_cursorRow - 1, 0, Height - 1));
            }
            catch { }
        }
    }

    public void ScrollUp()
    {
        int maxRow = (KeyRowVisible && Mode == 0) ? Height - 2 : Height - 1;
        for (int r = 0; r < maxRow; r++)
        {
            for (int c = 0; c < Width; c++)
            {
                _charBuffer[r, c] = _charBuffer[r + 1, c];
            }
        }
        for (int c = 0; c < Width; c++)
        {
            _charBuffer[maxRow, c] = ' ';
        }

        if (Mode != 0)
        {
            lock (GraphicLock)
            {
                int cellH = 16;
                using var temp = GraphicBitmap.Clone(new Rectangle(0, cellH, 640, 400 - cellH), GraphicBitmap.PixelFormat);
                GraphicContext.DrawImage(temp, 0, 0);
                var bgCol = CgaPalette.GetColor(BackgroundColor);
                using var bgBrush = new SolidBrush(Color.FromArgb(bgCol.R, bgCol.G, bgCol.B));
                GraphicContext.FillRectangle(bgBrush, 0, 400 - cellH, 640, cellH);
            }
        }

        if (!_isRedirected)
        {
            try
            {
                Console.MoveBufferArea(0, 1, Width, maxRow, 0, 0);
                Console.SetCursorPosition(0, maxRow);
                Console.Write(new string(' ', Width));
                Console.SetCursorPosition(0, maxRow);
                if (KeyRowVisible && Mode == 0)
                {
                    RenderKeyRow();
                }
            }
            catch { }
        }
    }

    public void SetKeyLabels(string[] labels)
    {
        _keyLabels = labels;
        if (KeyRowVisible && Mode == 0)
        {
            RenderKeyRow();
        }
    }

    public void RenderKeyRow()
    {
        int keyCount = Width == 40 ? 5 : 10;
        int slotLabelLen = (Width / keyCount) - 1;

        // Update character buffer
        int col = 0;
        for (int i = 0; i < keyCount; i++)
        {
            string label = i < _keyLabels.Length && _keyLabels[i] != null ? _keyLabels[i] : "";
            string num = ((i + 1) % 10).ToString();
            string rest = label.Length > 1 ? label[1..] : "";
            string labelText = rest.Length > slotLabelLen ? rest[..slotLabelLen] : rest.PadRight(slotLabelLen);
            string chunk = num + labelText;
            for (int k = 0; k < chunk.Length && col < Width; k++)
            {
                _charBuffer[Height - 1, col++] = chunk[k];
            }
        }
        while (col < Width)
        {
            _charBuffer[Height - 1, col++] = ' ';
        }

        if (_isRedirected || Mode != 0) return;
        try
        {
            int oldLeft = Console.CursorLeft;
            int oldTop = Console.CursorTop;
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;

            int termWidth = Width;
            try
            {
                if (Console.WindowWidth > termWidth)
                    termWidth = Console.WindowWidth;
            }
            catch { }

            Console.SetCursorPosition(0, Height - 1);

            int drawnCols = 0;
            var normalFg = ConsoleColor.White;
            var normalBg = ConsoleColor.Black;
            var inverseFg = ConsoleColor.Black;
            var inverseBg = ConsoleColor.White;

            for (int i = 0; i < keyCount; i++)
            {
                string label = i < _keyLabels.Length && _keyLabels[i] != null ? _keyLabels[i] : "";
                string num = ((i + 1) % 10).ToString();
                string rest = label.Length > 1 ? label[1..] : "";
                string keyText = rest;
                if (keyText.Length > slotLabelLen)
                    keyText = keyText[..slotLabelLen];

                int padLen = slotLabelLen - keyText.Length;

                // Function key number: normal monochrome
                Console.ForegroundColor = normalFg;
                Console.BackgroundColor = normalBg;
                Console.Write(num);

                // Keys string only: inverse monochrome
                if (keyText.Length > 0)
                {
                    Console.ForegroundColor = inverseFg;
                    Console.BackgroundColor = inverseBg;
                    Console.Write(keyText);
                }

                // Padding spaces: normal monochrome
                if (padLen > 0)
                {
                    Console.ForegroundColor = normalFg;
                    Console.BackgroundColor = normalBg;
                    Console.Write(new string(' ', padLen));
                }

                drawnCols += 1 + slotLabelLen;
            }

            // Clear any remaining columns on row 25 up to the full terminal width in normal monochrome
            if (termWidth > drawnCols)
            {
                Console.ForegroundColor = normalFg;
                Console.BackgroundColor = normalBg;
                Console.Write(new string(' ', termWidth - drawnCols));
            }

            Console.ForegroundColor = oldFg;
            Console.BackgroundColor = oldBg;
            Console.SetCursorPosition(Math.Clamp(oldLeft, 0, Width - 1), Math.Clamp(oldTop, 0, Height - 1));
        }
        catch { }
    }

    public void ClearKeyRow()
    {
        for (int c = 0; c < Width; c++)
        {
            _charBuffer[Height - 1, c] = ' ';
        }

        if (_isRedirected || Mode != 0) return;
        try
        {
            int oldLeft = Console.CursorLeft;
            int oldTop = Console.CursorTop;
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;

            int termWidth = Width;
            try
            {
                if (Console.WindowWidth > termWidth)
                    termWidth = Console.WindowWidth;
            }
            catch { }

            Console.SetCursorPosition(0, Height - 1);
            Console.ForegroundColor = CgaPalette.GetColor(ForegroundColor).ConsoleColor;
            Console.BackgroundColor = CgaPalette.GetColor(BackgroundColor).ConsoleColor;

            if (_vtSupported)
            {
                Console.Write("\x1b[2K"); // Erase entire 25th row
            }

            Console.Write(new string(' ', termWidth));

            Console.ForegroundColor = oldFg;
            Console.BackgroundColor = oldBg;
            Console.SetCursorPosition(Math.Clamp(oldLeft, 0, Width - 1), Math.Clamp(oldTop, 0, Height - 1));
        }
        catch { }
    }

    private CgaPalette.ColorEntry GetScreenColor(int colorIndex)
    {
        if (Mode == 1)
        {
            if (colorIndex == 0) return CgaPalette.GetColor(BackgroundColor);
            if (_screen1Palette == 0)
            {
                if (colorIndex == 1) return CgaPalette.GetColor(2);  // Green
                if (colorIndex == 2) return CgaPalette.GetColor(4);  // Red
                if (colorIndex == 3) return CgaPalette.GetColor(6);  // Brown
            }
            else
            {
                if (colorIndex == 1) return CgaPalette.GetColor(11); // Light Cyan
                if (colorIndex == 2) return CgaPalette.GetColor(13); // Light Magenta
                if (colorIndex == 3) return CgaPalette.GetColor(15); // Bright White
            }
        }
        else if (Mode == 2)
        {
            if (colorIndex == 0) return CgaPalette.GetColor(BackgroundColor);
            if (colorIndex == 1) return CgaPalette.GetColor(ForegroundColor != 0 ? ForegroundColor : 15);
        }
        return CgaPalette.GetColor(colorIndex);
    }

    public void PSet(int x, int y, int color)
    {
        lock (GraphicLock)
        {
            var colEntry = GetScreenColor(color);
            var col = Color.FromArgb(colEntry.R, colEntry.G, colEntry.B);
            int scaleX = Mode == 1 ? 2 : 1;
            int scaleY = 2;

            int px = Math.Clamp(x * scaleX, 0, 639);
            int py = Math.Clamp(y * scaleY, 0, 399);

            using var brush = new SolidBrush(col);
            GraphicContext.FillRectangle(brush, px, py, scaleX, scaleY);
        }
    }

    public void PReset(int x, int y) => PSet(x, y, BackgroundColor);

    public int Point(int x, int y)
    {
        lock (GraphicLock)
        {
            int scaleX = Mode == 1 ? 2 : 1;
            int scaleY = 2;
            int px = Math.Clamp(x * scaleX, 0, 639);
            int py = Math.Clamp(y * scaleY, 0, 399);

            var col = GraphicBitmap.GetPixel(px, py);
            if (Mode == 1)
            {
                for (int c = 0; c < 4; c++)
                {
                    var entry = GetScreenColor(c);
                    if (entry.R == col.R && entry.G == col.G && entry.B == col.B)
                        return c;
                }
            }
            else if (Mode == 2)
            {
                for (int c = 0; c < 2; c++)
                {
                    var entry = GetScreenColor(c);
                    if (entry.R == col.R && entry.G == col.G && entry.B == col.B)
                        return c;
                }
            }

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
        lock (GraphicLock)
        {
            var colEntry = GetScreenColor(color);
            var col = Color.FromArgb(colEntry.R, colEntry.G, colEntry.B);
            int scaleX = Mode == 1 ? 2 : 1;
            int scaleY = 2;

            if (box)
            {
                int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
                int sx = minX * scaleX;
                int sy = minY * scaleY;
                int w = (maxX - minX + 1) * scaleX;
                int h = (maxY - minY + 1) * scaleY;

                using var brush = new SolidBrush(col);
                if (boxFill)
                {
                    GraphicContext.FillRectangle(brush, sx, sy, w, h);
                }
                else
                {
                    // Top and bottom edges
                    GraphicContext.FillRectangle(brush, sx, sy, w, scaleY);
                    GraphicContext.FillRectangle(brush, sx, sy + h - scaleY, w, scaleY);
                    // Left and right edges
                    GraphicContext.FillRectangle(brush, sx, sy, scaleX, h);
                    GraphicContext.FillRectangle(brush, sx + w - scaleX, sy, scaleX, h);
                }
                return;
            }

            // Normal line using Bresenham algorithm
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sxStep = x1 < x2 ? 1 : -1;
            int syStep = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            int cx = x1, cy = y1;

            while (true)
            {
                PSet(cx, cy, color);
                if (cx == x2 && cy == y2) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    cx += sxStep;
                }
                if (e2 < dx)
                {
                    err += dx;
                    cy += syStep;
                }
            }
        }
    }

    public void Circle(int xc, int yc, int radius, int color, double startAngle = 0, double endAngle = 2 * Math.PI, double aspect = 1.0)
    {
        lock (GraphicLock)
        {
            int steps = Math.Max(72, radius * 8);
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
        lock (GraphicLock)
        {
            var targetCol = GetScreenColor(paintColor);
            var boundCol = GetScreenColor(boundaryColor);
            var targetColor = Color.FromArgb(targetCol.R, targetCol.G, targetCol.B);
            var boundColor = Color.FromArgb(boundCol.R, boundCol.G, boundCol.B);

            int scaleX = Mode == 1 ? 2 : 1;
            int scaleY = 2;
            int sx = startX * scaleX;
            int sy = startY * scaleY;

            if (sx < 0 || sx >= 640 || sy < 0 || sy >= 400) return;

            var rect = new Rectangle(0, 0, 640, 400);
            var bmpData = GraphicBitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[640 * 400];
                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

                int targetArgb = targetColor.ToArgb();
                int boundArgb = boundColor.ToArgb();
                int startIdx = sy * 640 + sx;

                if (startIdx >= 0 && startIdx < pixels.Length)
                {
                    int curArgb = pixels[startIdx];
                    if (curArgb != boundArgb && curArgb != targetArgb)
                    {
                        var stack = new Stack<int>();
                        stack.Push(startIdx);
                        pixels[startIdx] = targetArgb;

                        while (stack.Count > 0)
                        {
                            int idx = stack.Pop();
                            int px = idx % 640;
                            int py = idx / 640;

                            if (px + 1 < 640)
                            {
                                int nIdx = idx + 1;
                                if (pixels[nIdx] != boundArgb && pixels[nIdx] != targetArgb)
                                {
                                    pixels[nIdx] = targetArgb;
                                    stack.Push(nIdx);
                                }
                            }
                            if (px - 1 >= 0)
                            {
                                int nIdx = idx - 1;
                                if (pixels[nIdx] != boundArgb && pixels[nIdx] != targetArgb)
                                {
                                    pixels[nIdx] = targetArgb;
                                    stack.Push(nIdx);
                                }
                            }
                            if (py + 1 < 400)
                            {
                                int nIdx = idx + 640;
                                if (pixels[nIdx] != boundArgb && pixels[nIdx] != targetArgb)
                                {
                                    pixels[nIdx] = targetArgb;
                                    stack.Push(nIdx);
                                }
                            }
                            if (py - 1 >= 0)
                            {
                                int nIdx = idx - 640;
                                if (pixels[nIdx] != boundArgb && pixels[nIdx] != targetArgb)
                                {
                                    pixels[nIdx] = targetArgb;
                                    stack.Push(nIdx);
                                }
                            }
                        }
                        Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                    }
                }
            }
            finally
            {
                GraphicBitmap.UnlockBits(bmpData);
            }
        }
    }

    public string ReadLine(int row)
    {
        int r = row - 1;
        if (r < 0 || r >= Height) return "";
        var sb = new System.Text.StringBuilder();
        for (int c = 0; c < Width; c++)
        {
            sb.Append(_charBuffer[r, c]);
        }
        return sb.ToString().TrimEnd();
    }

    private void ShowGraphicsWindow(int mode)
    {
        if (_graphicsWindow == null || _graphicsWindow.IsDisposed)
        {
            _windowReadyEvent.Reset();
            _guiThread = new Thread(() =>
            {
                _graphicsWindow = new GraphicsWindow(this, _inputDriver);
                _ = _graphicsWindow.Handle; // Force window handle creation
                _windowReadyEvent.Set();
                Application.Run(_graphicsWindow);
            })
            {
                IsBackground = true,
                Name = "GW-BASIC Graphics Thread"
            };
            _guiThread.SetApartmentState(ApartmentState.STA);
            _guiThread.Start();
            _windowReadyEvent.Wait();
        }

        if (_graphicsWindow != null && !_graphicsWindow.IsDisposed && _graphicsWindow.IsHandleCreated)
        {
            _graphicsWindow.BeginInvoke(new Action(() =>
            {
                _graphicsWindow.UpdateMode(mode);
                _graphicsWindow.Show();
                _graphicsWindow.BringToFront();
            }));
        }
    }

    private void HideGraphicsWindow()
    {
        if (_graphicsWindow != null && !_graphicsWindow.IsDisposed && _graphicsWindow.IsHandleCreated && _graphicsWindow.Visible)
        {
            _graphicsWindow.BeginInvoke(new Action(() =>
            {
                _graphicsWindow.Hide();
            }));
        }
    }

    public void OnGraphicsWindowClosedByUser()
    {
        SetMode(0);
    }

    public void Dispose()
    {
        if (_graphicsWindow != null && !_graphicsWindow.IsDisposed)
        {
            try
            {
                _graphicsWindow.BeginInvoke(new Action(() =>
                {
                    _graphicsWindow.Dispose();
                }));
            }
            catch { }
        }

        GraphicContext.Dispose();
        GraphicBitmap.Dispose();
        _fontMode1.Dispose();
        _fontMode2.Dispose();
        _stringFormat.Dispose();
        _windowReadyEvent.Dispose();
    }
}
