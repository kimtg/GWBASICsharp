using System.Text;

namespace GWBASIC.Core.Drivers;

public class PhysicalFileHandle : IFileHandle
{
    public FileModeType Mode { get; }
    public int RecordLength { get; }
    public long Length => _stream.Length;
    public long Position => _stream.Position;
    public bool IsEof => _stream.Position >= _stream.Length;

    private readonly FileStream _stream;
    private readonly StreamReader? _reader;
    private readonly StreamWriter? _writer;

    public PhysicalFileHandle(string path, FileModeType mode, int recordLength)
    {
        Mode = mode;
        RecordLength = recordLength;

        FileAccess access = mode switch
        {
            FileModeType.Input => FileAccess.Read,
            FileModeType.Output => FileAccess.Write,
            FileModeType.Append => FileAccess.Write,
            FileModeType.Random => FileAccess.ReadWrite,
            _ => FileAccess.ReadWrite
        };

        FileMode fMode = mode switch
        {
            FileModeType.Input => FileMode.Open,
            FileModeType.Output => FileMode.Create,
            FileModeType.Append => FileMode.Append,
            FileModeType.Random => FileMode.OpenOrCreate,
            _ => FileMode.OpenOrCreate
        };

        _stream = new FileStream(path, fMode, access, FileShare.ReadWrite);
        if (mode == FileModeType.Input)
        {
            _reader = new StreamReader(_stream, Encoding.Latin1, false, 1024, leaveOpen: true);
        }
        else if (mode is FileModeType.Output or FileModeType.Append)
        {
            _writer = new StreamWriter(_stream, Encoding.Latin1, 1024, leaveOpen: true) { AutoFlush = true };
        }
    }

    public void Write(string text)
    {
        if (_writer != null)
        {
            _writer.Write(text);
        }
        else
        {
            byte[] bytes = Encoding.Latin1.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }
    }

    public void WriteLine(string text)
    {
        if (_writer != null)
        {
            _writer.WriteLine(text);
        }
        else
        {
            Write(text + "\r\n");
        }
    }

    public string ReadLine()
    {
        if (_reader != null)
        {
            return _reader.ReadLine() ?? "";
        }

        var sb = new StringBuilder();
        int b;
        while ((b = _stream.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') sb.Append((char)b);
        }
        return sb.ToString();
    }

    public int ReadChar() => _stream.ReadByte();

    public void Put(int recordNumber, byte[] recordBuffer)
    {
        long target = (long)(recordNumber - 1) * RecordLength;
        _stream.Seek(target, SeekOrigin.Begin);
        _stream.Write(recordBuffer, 0, Math.Min(RecordLength, recordBuffer.Length));
        _stream.Flush();
    }

    public byte[] Get(int recordNumber)
    {
        long target = (long)(recordNumber - 1) * RecordLength;
        _stream.Seek(target, SeekOrigin.Begin);
        byte[] buf = new byte[RecordLength];
        if (_stream.Position < _stream.Length)
        {
            _stream.ReadAtLeast(buf, Math.Min(RecordLength, (int)(_stream.Length - _stream.Position)), false);
        }
        return buf;
    }

    public void Close()
    {
        _writer?.Flush();
        _stream.Close();
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream.Dispose();
    }
}

public class PhysicalFileSystemDriver : IFileSystemDriver
{
    public string CurrentDirectory
    {
        get => Directory.GetCurrentDirectory();
        set => Directory.SetCurrentDirectory(value);
    }

    public bool FileExists(string filename) => File.Exists(ResolvePath(filename));

    public IFileHandle OpenFile(string filename, FileModeType mode, int recordLength = 128)
    {
        string path = ResolvePath(filename);
        return new PhysicalFileHandle(path, mode, recordLength);
    }

    public void DeleteFile(string filename)
    {
        string path = ResolvePath(filename);
        if (File.Exists(path)) File.Delete(path);
    }

    public void RenameFile(string oldFilename, string newFilename)
    {
        string pOld = ResolvePath(oldFilename);
        string pNew = ResolvePath(newFilename);
        if (File.Exists(pOld)) File.Move(pOld, pNew);
    }

    public string[] ListFiles(string pattern = "*.*")
    {
        try
        {
            return Directory.GetFiles(CurrentDirectory, pattern).Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string ReadAllText(string filename) => File.ReadAllText(ResolvePath(filename), Encoding.Latin1);

    public void WriteAllText(string filename, string content) => File.WriteAllText(ResolvePath(filename), content, Encoding.Latin1);

    private string ResolvePath(string filename)
    {
        if (Path.IsPathRooted(filename)) return filename;
        return Path.Combine(CurrentDirectory, filename);
    }
}
