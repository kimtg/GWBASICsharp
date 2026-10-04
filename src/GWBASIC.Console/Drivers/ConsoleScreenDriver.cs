using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;

namespace GWBASIC.ConsoleApp.Drivers;

public class ConsoleScreenDriver : IScreenDriver
{
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
    public bool KeyRowVisible { get; set; } = true;

    private string[] _keyLabels = new string[10];
    private readonly char[,] _charBuffer;
    private readonly bool _isRedirected;

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
    }

    public void SetMode(int mode)
    {
        Mode = mode;
        if (mode == 1) SetWidth(40);
        else SetWidth(80);
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
        for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                _charBuffer[r, c] = ' ';

        _cursorRow = 1;
        _cursorCol = 1;

        if (!_isRedirected)
        {
            try
            {
                Console.Clear();
                if (KeyRowVisible)
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

        int maxRow = KeyRowVisible ? Height - 2 : Height - 1;
        if (_cursorRow >= maxRow)
        {
            ScrollUp();
            _cursorRow = maxRow;
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
        int maxRow = KeyRowVisible ? Height - 2 : Height - 1;
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

        if (!_isRedirected)
        {
            try
            {
                Console.MoveBufferArea(0, 1, Width, maxRow, 0, 0);
                Console.SetCursorPosition(0, maxRow);
                Console.Write(new string(' ', Width));
                Console.SetCursorPosition(0, maxRow);
                if (KeyRowVisible)
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
        if (KeyRowVisible && !_isRedirected)
        {
            RenderKeyRow();
        }
    }

    public void RenderKeyRow()
    {
        if (_isRedirected) return;
        try
        {
            int oldLeft = Console.CursorLeft;
            int oldTop = Console.CursorTop;
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;

            Console.SetCursorPosition(0, Height - 1);

            for (int i = 0; i < 10; i++)
            {
                string label = i < _keyLabels.Length && _keyLabels[i] != null ? _keyLabels[i] : "";
                string num = ((i + 1) % 10).ToString();
                string rest = label.Length > 1 ? label[1..] : "";

                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = ConsoleColor.White;
                Console.Write(num);

                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.Write(rest.PadRight(7));
            }

            Console.ForegroundColor = oldFg;
            Console.BackgroundColor = oldBg;
            Console.SetCursorPosition(oldLeft, oldTop);
        }
        catch { }
    }

    public void PSet(int x, int y, int color) { }
    public void PReset(int x, int y) { }
    public int Point(int x, int y) => 0;
    public void Line(int x1, int y1, int x2, int y2, int color, bool box = false, bool boxFill = false, ushort style = 0xFFFF) { }
    public void Circle(int xc, int yc, int radius, int color, double startAngle = 0, double endAngle = 2 * Math.PI, double aspect = 1.0) { }
    public void Paint(int x, int y, int paintColor, int boundaryColor) { }

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
}
