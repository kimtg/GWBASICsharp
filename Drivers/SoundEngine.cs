using System.Runtime.InteropServices;

namespace GWBASIC.ConsoleApp.Drivers;

/// <summary>
/// Authentic PC Speaker sound engine for GW-BASIC.
/// Generates 8-bit square waves matching vintage IBM PC PIT 8253 audio
/// and plays synchronously or asynchronously via Win32 PlaySound (or background Console.Beep fallback).
/// </summary>
public static class SoundEngine
{
    private static bool _isMuted = false;
    private static readonly object _lock = new();
    private static CancellationTokenSource? _currentCts;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySound(byte[]? pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_SYNC = 0x0000;
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_PURGE = 0x0040;

    public static bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_isMuted) Stop();
        }
    }

    public static void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    /// <summary>
    /// Stops any currently playing audio immediately.
    /// </summary>
    public static void Stop()
    {
        lock (_lock)
        {
            _currentCts?.Cancel();
            _currentCts = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    PlaySound(null, IntPtr.Zero, SND_PURGE);
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    /// <summary>
    /// Plays in-memory WAV data synchronously on the current thread.
    /// </summary>
    public static void PlaySync(byte[]? wavData, int fallbackFrequencyHz = 0, int fallbackDurationMs = 0)
    {
        if (_isMuted) return;

        PlayInternal(wavData, fallbackFrequencyHz, fallbackDurationMs, isAsync: false);
    }

    /// <summary>
    /// Plays in-memory WAV data asynchronously in the background.
    /// </summary>
    public static void PlayAsync(byte[]? wavData, int fallbackFrequencyHz = 0, int fallbackDurationMs = 0)
    {
        if (_isMuted) return;

        Task.Run(() => PlayInternal(wavData, fallbackFrequencyHz, fallbackDurationMs, isAsync: true));
    }

    private static void PlayInternal(byte[]? wavData, int fallbackFrequencyHz, int fallbackDurationMs, bool isAsync)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();
            cts = _currentCts;
        }

        try
        {
            if (cts.Token.IsCancellationRequested || _isMuted) return;

            if (wavData != null && wavData.Length > 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                uint flags = SND_MEMORY | SND_NODEFAULT | (isAsync ? SND_ASYNC : SND_SYNC);
                bool success = false;
                try
                {
                    success = PlaySound(wavData, IntPtr.Zero, flags);
                }
                catch
                {
                    success = false;
                }

                if (success)
                {
                    return;
                }
            }

            // Fallback for non-Windows or if PlaySound failed
            if (fallbackFrequencyHz >= 37 && fallbackFrequencyHz <= 32767 && fallbackDurationMs > 0)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        Console.Beep(fallbackFrequencyHz, Math.Min(fallbackDurationMs, 5000));
                    }
                }
                catch
                {
                    Thread.Sleep(fallbackDurationMs);
                }
            }
            else if (fallbackDurationMs > 0)
            {
                Thread.Sleep(fallbackDurationMs);
            }
        }
        catch
        {
            // Audio errors should never crash the interpreter
        }
    }
}
