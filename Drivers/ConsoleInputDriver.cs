using System.Collections.Concurrent;
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
    private readonly ConcurrentQueue<ConsoleKeyInfo> _windowKeyQueue = new();

    public ConsoleInputDriver(IScreenDriver screen)
    {
        _screen = screen;
    }

    public void AttachEnvironment(BasicEnvironment env)
    {
        _environment = env;
    }

    public void EnqueueKey(ConsoleKeyInfo key)
    {
        _windowKeyQueue.Enqueue(key);
    }

    public bool KeyAvailable => !_windowKeyQueue.IsEmpty || (!Console.IsInputRedirected && Console.KeyAvailable);

    public string? ReadInkey()
    {
        if (_windowKeyQueue.TryDequeue(out var queuedKey))
        {
            if (queuedKey.KeyChar != '\0')
                return queuedKey.KeyChar.ToString();
            return "\0" + ((char)queuedKey.Key);
        }

        if (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(true);
            if (keyInfo.KeyChar != '\0')
                return keyInfo.KeyChar.ToString();

            // Extended keys (2-byte sequence in GW-BASIC: chr$(0) + code)
            return "\0" + ((char)keyInfo.Key);
        }

        return null;
    }

    private ConsoleKeyInfo ReadNextKey()
    {
        while (true)
        {
            if (_windowKeyQueue.TryDequeue(out var queuedKey))
                return queuedKey;

            if (!Console.IsInputRedirected && Console.KeyAvailable)
                return Console.ReadKey(true);

            Thread.Sleep(10);
        }
    }

    public string ReadLine()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? "";
        }

        bool oldTreat = false;
        bool canTreat = true;
        try
        {
            oldTreat = Console.TreatControlCAsInput;
            Console.TreatControlCAsInput = true;
        }
        catch
        {
            canTreat = false;
        }

        try
        {
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
                var keyInfo = ReadNextKey();

                // Handle Ctrl+C / Ctrl+Break / Pause
                if (keyInfo.KeyChar == '\x03' || (keyInfo.Key == ConsoleKey.C && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0) || keyInfo.Key == ConsoleKey.Pause)
                {
                    if (_environment != null && _environment.IsAutoMode)
                    {
                        _environment.ExitAutoMode();
                    }
                    _screen.WriteLine();
                    _screen.WriteLine("Ok");
                    return "";
                }

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
    finally
    {
        if (canTreat)
        {
            try { Console.TreatControlCAsInput = oldTreat; } catch { }
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

    private void RedrawLine(StringBuilder sb, int cursor, int startCol, int startRow)
    {
        if (_screen.Mode != 0)
        {
            _screen.Locate(startRow + 1, startCol + 1);
            _screen.Write(sb.ToString() + " ");
            int total = startCol + cursor;
            int r = startRow + (total / _screen.Width);
            int c = total % _screen.Width;
            _screen.Locate(r + 1, c + 1);
        }
        else
        {
            try
            {
                Console.SetCursorPosition(startCol, startRow);
                Console.Write(sb.ToString() + " ");
                UpdateCursorPos(startCol, startRow, cursor);
            }
            catch { }
        }
    }

    private void UpdateCursorPos(int startCol, int startRow, int cursor)
    {
        int total = startCol + cursor;
        int r = startRow + (total / _screen.Width);
        int c = total % _screen.Width;

        if (_screen.Mode != 0)
        {
            _screen.Locate(r + 1, c + 1);
        }
        else
        {
            try
            {
                Console.SetCursorPosition(c, r);
            }
            catch { }
        }
    }
}
