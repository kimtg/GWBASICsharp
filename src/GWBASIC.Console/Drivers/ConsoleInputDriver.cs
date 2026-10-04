using System.Text;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;

namespace GWBASIC.ConsoleApp.Drivers;

public class ConsoleInputDriver : IInputDriver
{
    private readonly IScreenDriver _screen;
    private BasicEnvironment? _environment;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public ConsoleInputDriver(IScreenDriver screen)
    {
        _screen = screen;
    }

    public void AttachEnvironment(BasicEnvironment env)
    {
        _environment = env;
    }

    public bool KeyAvailable => !Console.IsInputRedirected && Console.KeyAvailable;

    public string? ReadInkey()
    {
        if (Console.IsInputRedirected || !Console.KeyAvailable) return null;
        var keyInfo = Console.ReadKey(true);
        if (keyInfo.KeyChar != '\0')
            return keyInfo.KeyChar.ToString();

        // Extended keys (2-byte sequence in GW-BASIC: chr$(0) + code)
        return "\0" + ((char)keyInfo.Key);
    }

    public string ReadLine()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        var sb = new StringBuilder();
        int cursor = 0;
        bool insertMode = true;
        int startCol = Math.Clamp(_screen.CursorCol - 1, 0, _screen.Width - 1);
        int startRow = Math.Clamp(_screen.CursorRow - 1, 0, _screen.Height - 1);
        try
        {
            Console.SetCursorPosition(startCol, startRow);
        }
        catch { }

        while (true)
        {
            var keyInfo = Console.ReadKey(true);

            // Handle Function Keys F1-F10
            if (keyInfo.Key is >= ConsoleKey.F1 and <= ConsoleKey.F10 && _environment != null)
            {
                int fIndex = (keyInfo.Key - ConsoleKey.F1) + 1;
                string macro = _environment.GetFunctionKey(fIndex);

                foreach (char ch in macro)
                {
                    if (ch == '\r')
                    {
                        _screen.WriteLine();
                        string finalLine = sb.ToString();
                        AddToHistory(finalLine);
                        return finalLine;
                    }
                    sb.Insert(cursor++, ch);
                }
                RedrawLine(sb, cursor, startCol, startRow);
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                _screen.WriteLine();
                string line = sb.ToString();
                AddToHistory(line);
                return line;
            }

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    cursor--;
                    sb.Remove(cursor, 1);
                    RedrawLine(sb, cursor, startCol, startRow);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Delete)
            {
                if (cursor < sb.Length)
                {
                    sb.Remove(cursor, 1);
                    RedrawLine(sb, cursor, startCol, startRow);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0)
                {
                    cursor--;
                    UpdateCursorPos(startCol, startRow, cursor);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (cursor < sb.Length)
                {
                    cursor++;
                    UpdateCursorPos(startCol, startRow, cursor);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Home)
            {
                cursor = 0;
                UpdateCursorPos(startCol, startRow, cursor);
                continue;
            }

            if (keyInfo.Key == ConsoleKey.End)
            {
                cursor = sb.Length;
                UpdateCursorPos(startCol, startRow, cursor);
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Insert)
            {
                insertMode = !insertMode;
                continue;
            }

            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (_history.Count > 0 && _historyIndex < _history.Count - 1)
                {
                    _historyIndex++;
                    sb.Clear();
                    sb.Append(_history[_history.Count - 1 - _historyIndex]);
                    cursor = sb.Length;
                    RedrawLine(sb, cursor, startCol, startRow);
                }
                continue;
            }

            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    sb.Clear();
                    sb.Append(_history[_history.Count - 1 - _historyIndex]);
                    cursor = sb.Length;
                    RedrawLine(sb, cursor, startCol, startRow);
                }
                else if (_historyIndex == 0)
                {
                    _historyIndex = -1;
                    sb.Clear();
                    cursor = 0;
                    RedrawLine(sb, cursor, startCol, startRow);
                }
                continue;
            }

            // Regular printable characters
            if (!char.IsControl(keyInfo.KeyChar))
            {
                if (insertMode || cursor >= sb.Length)
                {
                    sb.Insert(cursor, keyInfo.KeyChar);
                }
                else
                {
                    sb[cursor] = keyInfo.KeyChar;
                }
                cursor++;
                RedrawLine(sb, cursor, startCol, startRow);
            }
        }
    }

    private void AddToHistory(string line)
    {
        _historyIndex = -1;
        if (!string.IsNullOrWhiteSpace(line))
        {
            if (_history.Count == 0 || _history[^1] != line)
                _history.Add(line);
        }
    }

    private static void RedrawLine(StringBuilder sb, int cursor, int startCol, int startRow)
    {
        try
        {
            Console.SetCursorPosition(startCol, startRow);
            Console.Write(sb.ToString() + " ");
            UpdateCursorPos(startCol, startRow, cursor);
        }
        catch { }
    }

    private static void UpdateCursorPos(int startCol, int startRow, int cursor)
    {
        try
        {
            int total = startCol + cursor;
            int r = startRow + (total / 80);
            int c = total % 80;
            Console.SetCursorPosition(c, r);
        }
        catch { }
    }
}
