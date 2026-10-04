using System.Collections.Concurrent;
using GWBASIC.Core.Drivers;

namespace GWBASIC.Desktop.Drivers;

public class GuiInputDriver : IInputDriver
{
    private readonly ConcurrentQueue<char> _keyQueue = new();
    private readonly BlockingCollection<string> _lineQueue = new();
    private readonly AutoResetEvent _keyAvailableEvent = new(false);

    public bool KeyAvailable => !_keyQueue.IsEmpty;

    public void EnqueueChar(char ch)
    {
        _keyQueue.Enqueue(ch);
        _keyAvailableEvent.Set();
    }

    public void EnqueueLine(string line)
    {
        _lineQueue.Add(line);
    }

    public string? ReadInkey()
    {
        if (_keyQueue.TryDequeue(out char ch))
        {
            return ch.ToString();
        }
        return null;
    }

    public string ReadLine() => _lineQueue.Take();
}
