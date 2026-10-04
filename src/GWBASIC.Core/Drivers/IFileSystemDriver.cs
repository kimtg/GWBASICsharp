namespace GWBASIC.Core.Drivers;

public enum FileModeType
{
    Input,
    Output,
    Append,
    Random
}

public interface IFileHandle : IDisposable
{
    FileModeType Mode { get; }
    int RecordLength { get; }
    long Length { get; }
    long Position { get; }
    bool IsEof { get; }

    void Write(string text);
    void WriteLine(string text);
    string ReadLine();
    int ReadChar();
    void Put(int recordNumber, byte[] recordBuffer);
    byte[] Get(int recordNumber);
    void Close();
}

/// <summary>
/// Driver for file operations, DOS-compatible path management, and directory listings.
/// </summary>
public interface IFileSystemDriver
{
    string CurrentDirectory { get; set; }
    bool FileExists(string filename);
    IFileHandle OpenFile(string filename, FileModeType mode, int recordLength = 128);
    void DeleteFile(string filename);
    void RenameFile(string oldFilename, string newFilename);
    string[] ListFiles(string pattern = "*.*");
    string ReadAllText(string filename);
    void WriteAllText(string filename, string content);
}
