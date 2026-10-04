using System.Text;
using GWBASIC.Core.Common;

namespace GWBASIC.Core.Drivers;

public class VirtualScreenDriver : IScreenDriver
{
    public int Width { get; private set; } = 80;
    public int Height { get; private set; } = 25;
    public int Mode { get; private set; } = 0;
    public int ForegroundColor { get; private set; } = 7;
    public int BackgroundColor { get; private set; } = 0;
    public int CursorRow { get; private set; } = 1; // 1-based
    public int CursorCol { get; private set; } = 1; // 1-based
    public bool CursorVisible { get; private set; } = true;
    public bool KeyRowVisible { get; set; } = true;

    private readonly char[,] _screenBuffer;
    private readonly byte[,] _pixelBuffer; // For Screen 1 (320x200), Screen 2 (640x200)
    private readonly StringBuilder _outputLog = new();
    private string[] _keyLabels = new string[10];

    public VirtualScreenDriver(int width = 80, int height = 25)
    {
        Width = width;
        Height = height;
        _screenBuffer = new char[Height, Width];
        _pixelBuffer = new byte[640, 400];
        Cls();
    }

    public string GetOutputLog() => _outputLog.ToString();
    public void ClearOutputLog() => _outputLog.Clear();

    public void SetMode(int mode)
    {
        Mode = mode;
        if (mode == 1) { Width = 40; Height = 25; }
        else if (mode == 2) { Width = 80; Height = 25; }
        else { Width = 80; Height = 25; }
        Cls();
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
    }

    public void Locate(int row, int col, bool? cursorVisible = null)
    {
        CursorRow = Math.Clamp(row, 1, Height);
        CursorCol = Math.Clamp(col, 1, Width);
        if (cursorVisible.HasValue) CursorVisible = cursorVisible.Value;
    }

    public void Cls(int? mode = null)
    {
        for (int r = 0; r < Height; r++)
        {
            for (int c = 0; c < Width; c++)
            {
                _screenBuffer[r, c] = ' ';
            }
        }
        Array.Clear(_pixelBuffer);
        CursorRow = 1;
        CursorCol = 1;
    }

    public void Write(string text)
    {
        _outputLog.Append(text);
        foreach (char ch in text)
        {
            if (ch == '\r')
            {
                CursorCol = 1;
                continue;
            }
            if (ch == '\n')
            {
                WriteLine();
                continue;
            }

            int r = CursorRow - 1;
            int c = CursorCol - 1;
            if (r >= 0 && r < Height && c >= 0 && c < Width)
            {
                _screenBuffer[r, c] = ch;
            }

            CursorCol++;
            if (CursorCol > Width)
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
        _outputLog.AppendLine();
        CursorCol = 1;
        CursorRow++;
        if (CursorRow > (KeyRowVisible ? Height - 1 : Height))
        {
            ScrollUp();
            CursorRow = KeyRowVisible ? Height - 1 : Height;
        }
    }

    public void ScrollUp()
    {
        int maxRow = KeyRowVisible ? Height - 2 : Height - 1;
        for (int r = 0; r < maxRow; r++)
        {
            for (int c = 0; c < Width; c++)
            {
                _screenBuffer[r, c] = _screenBuffer[r + 1, c];
            }
        }
        for (int c = 0; c < Width; c++)
        {
            _screenBuffer[maxRow, c] = ' ';
        }
    }

    public void SetKeyLabels(string[] labels)
    {
        _keyLabels = labels;
    }

    public void PSet(int x, int y, int color)
    {
        if (x >= 0 && x < 640 && y >= 0 && y < 400)
        {
            _pixelBuffer[x, y] = (byte)(color % 16);
        }
    }

    public void PReset(int x, int y) => PSet(x, y, BackgroundColor);

    public int Point(int x, int y) =>
        (x >= 0 && x < 640 && y >= 0 && y < 400) ? _pixelBuffer[x, y] : 0;

    public void Line(int x1, int y1, int x2, int y2, int color, bool box = false, bool boxFill = false, ushort style = 0xFFFF)
    {
        if (box)
        {
            int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
            int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
            if (boxFill)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        PSet(x, y, color);
                    }
                }
            }
            else
            {
                for (int x = minX; x <= maxX; x++) { PSet(x, minY, color); PSet(x, maxY, color); }
                for (int y = minY; y <= maxY; y++) { PSet(minX, y, color); PSet(maxX, y, color); }
            }
            return;
        }

        // Bresenham's line algorithm
        int dx = Math.Abs(x2 - x1), sx = x1 < x2 ? 1 : -1;
        int dy = -Math.Abs(y2 - y1), sy = y1 < y2 ? 1 : -1;
        int err = dx + dy;
        int curX = x1, curY = y1;
        int bit = 0;

        while (true)
        {
            if (((style >> (15 - (bit % 16))) & 1) != 0)
            {
                PSet(curX, curY, color);
            }
            bit++;
            if (curX == x2 && curY == y2) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; curX += sx; }
            if (e2 <= dx) { err += dx; curY += sy; }
        }
    }

    public void Circle(int xc, int yc, int radius, int color, double startAngle = 0, double endAngle = 2 * Math.PI, double aspect = 1.0)
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

    public void Paint(int startX, int startY, int paintColor, int boundaryColor)
    {
        if (startX < 0 || startX >= 640 || startY < 0 || startY >= 400) return;
        byte targetCol = (byte)paintColor;
        byte boundCol = (byte)boundaryColor;
        if (_pixelBuffer[startX, startY] == boundCol || _pixelBuffer[startX, startY] == targetCol) return;

        var stack = new Stack<(int X, int Y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || x >= 640 || y < 0 || y >= 400) continue;
            byte cur = _pixelBuffer[x, y];
            if (cur == boundCol || cur == targetCol) continue;

            _pixelBuffer[x, y] = targetCol;
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }
    }

    public string ReadLine(int row)
    {
        int r = row - 1;
        if (r < 0 || r >= Height) return "";
        var sb = new StringBuilder();
        for (int c = 0; c < Width; c++)
        {
            sb.Append(_screenBuffer[r, c]);
        }
        return sb.ToString().TrimEnd();
    }
}

public class VirtualAudioDriver : IAudioDriver
{
    public List<(int Freq, int Dur)> PlayedSounds { get; } = new();
    public List<string> PlayedTunes { get; } = new();
    public int BeepCount { get; private set; }

    public void Beep() => BeepCount++;

    public void Sound(int frequencyHz, int durationClockTicks) =>
        PlayedSounds.Add((frequencyHz, durationClockTicks));

    public void Play(string musicCommands) =>
        PlayedTunes.Add(musicCommands);
}

public class VirtualInputDriver : IInputDriver
{
    private readonly Queue<string> _lineQueue = new();
    private readonly Queue<char> _keyQueue = new();

    public void EnqueueLine(string line) => _lineQueue.Enqueue(line);
    public void EnqueueKey(char key) => _keyQueue.Enqueue(key);

    public bool KeyAvailable => _keyQueue.Count > 0;

    public string? ReadInkey() =>
        _keyQueue.Count > 0 ? _keyQueue.Dequeue().ToString() : null;

    public string ReadLine() =>
        _lineQueue.Count > 0 ? _lineQueue.Dequeue() : throw new InvalidOperationException("VirtualInputDriver: No more lines queued for input");
}

public class VirtualFileHandle : IFileHandle
{
    public FileModeType Mode { get; }
    public int RecordLength { get; }
    public long Length => _data.Length;
    public long Position => _position;
    public bool IsEof => _position >= _data.Length;

    private readonly MemoryStream _data = new();
    private long _position = 0;
    private readonly Action<byte[]>? _onClose;

    public VirtualFileHandle(FileModeType mode, int recordLength, Action<byte[]>? onClose = null)
    {
        Mode = mode;
        RecordLength = recordLength;
        _onClose = onClose;
    }

    public void Write(string text)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(text);
        _data.Write(bytes, 0, bytes.Length);
        _position = _data.Position;
    }

    public void WriteLine(string text) => Write(text + "\r\n");

    public string ReadLine()
    {
        _data.Position = _position;
        var sb = new StringBuilder();
        int b;
        while ((b = _data.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') sb.Append((char)b);
        }
        _position = _data.Position;
        return sb.ToString();
    }

    public int ReadChar()
    {
        if (IsEof) return -1;
        _data.Position = _position;
        int b = _data.ReadByte();
        _position = _data.Position;
        return b;
    }

    public void Put(int recordNumber, byte[] recordBuffer)
    {
        long targetPos = (long)(recordNumber - 1) * RecordLength;
        _data.Position = targetPos;
        _data.Write(recordBuffer, 0, Math.Min(RecordLength, recordBuffer.Length));
        _position = _data.Position;
    }

    public byte[] Get(int recordNumber)
    {
        long targetPos = (long)(recordNumber - 1) * RecordLength;
        byte[] buf = new byte[RecordLength];
        if (targetPos < _data.Length)
        {
            _data.Position = targetPos;
            _data.Read(buf, 0, RecordLength);
        }
        _position = targetPos + RecordLength;
        return buf;
    }

    public void Close()
    {
        _onClose?.Invoke(_data.ToArray());
    }
    public void Dispose() => _data.Dispose();
}

public class VirtualFileSystemDriver : IFileSystemDriver
{
    public string CurrentDirectory { get; set; } = "C:\\";
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string filename) => _files.ContainsKey(filename);

    public IFileHandle OpenFile(string filename, FileModeType mode, int recordLength = 128)
    {
        var handle = new VirtualFileHandle(mode, recordLength, bytes =>
        {
            if (mode != FileModeType.Input)
            {
                _files[filename] = Encoding.Latin1.GetString(bytes);
            }
        });
        if ((mode == FileModeType.Input || mode == FileModeType.Random || mode == FileModeType.Append) && _files.TryGetValue(filename, out var existing))
        {
            handle.Write(existing);
            if (mode == FileModeType.Input || mode == FileModeType.Random)
            {
                // Reset position to beginning
                var fi = typeof(VirtualFileHandle).GetField("_position", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                fi?.SetValue(handle, 0L);
            }
        }
        return handle;
    }

    public void DeleteFile(string filename) => _files.Remove(filename);

    public void RenameFile(string oldFilename, string newFilename)
    {
        if (_files.TryGetValue(oldFilename, out var val))
        {
            _files.Remove(oldFilename);
            _files[newFilename] = val;
        }
    }

    public string[] ListFiles(string pattern = "*.*") => _files.Keys.ToArray();

    public string ReadAllText(string filename) =>
        _files.TryGetValue(filename, out var c) ? c : throw new BasicException(BasicErrorCode.FileNotFound);

    public void WriteAllText(string filename, string content) =>
        _files[filename] = content;
}
