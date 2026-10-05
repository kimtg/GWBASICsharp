using GWBASIC.ConsoleApp.Drivers;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;

namespace GWBASIC.ConsoleApp;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.Title = "GW-BASIC";
        try
        {
            Console.OutputEncoding = System.Text.Encoding.Latin1;
            Console.InputEncoding = System.Text.Encoding.Latin1;
        }
        catch { }

        bool isInteractive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        if (isInteractive)
        {
            ConsoleScreenDriver.EnterAlternateBuffer();
        }

        var screen = new ConsoleScreenDriver();
        var audio = new ConsoleAudioDriver();
        var input = new ConsoleInputDriver(screen);
        screen.AttachInputDriver(input);
        var fileSystem = new PhysicalFileSystemDriver();

        var environment = new BasicEnvironment(screen, audio, input, fileSystem);
        input.AttachEnvironment(environment);
        var interpreter = new Interpreter(environment);

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            audio.Stop();
            ConsoleScreenDriver.ExitAlternateBuffer();
            screen.Dispose();
        };

        // Handle Ctrl+C / Ctrl+Break
        Console.CancelKeyPress += (sender, e) =>
        {
            audio.Stop();
            if (interpreter.IsRunning)
            {
                e.Cancel = true;
                environment.IsPaused = true;
                screen.WriteLine($"\r\nBreak in {environment.CurrentLineNumber}");
                screen.WriteLine("Ok");
            }
            else if (environment.IsAutoMode)
            {
                e.Cancel = true;
                environment.ExitAutoMode();
                screen.WriteLine("\r\nOk");
            }
            else
            {
                ConsoleScreenDriver.ExitAlternateBuffer();
            }
        };

        // In interactive mode, initialize screen and display row 25 function keys
        if (isInteractive)
        {
            screen.Cls();
        }

        // If file passed via args, run it
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            string fileToRun = args[0];
            try
            {
                environment.LoadProgram(fileToRun);
                interpreter.Run();
            }
            catch (Exception ex)
            {
                screen.WriteLine($"Error loading file: {ex.Message}");
            }
        }
        else
        {
            // Authentic GW-BASIC 3.23 banner
            screen.WriteLine("GW-BASIC 3.23");
            screen.WriteLine("(C) Copyright Microsoft 1983,1984,1986,1987,1988");
            screen.WriteLine("60300 Bytes free");
            screen.WriteLine("Ok");
        }

        // Interactive REPL loop
        while (true)
        {
            if (Console.IsInputRedirected && args.Length > 0)
            {
                break;
            }

            string line = input.ReadLine();
            if (Console.IsInputRedirected && string.IsNullOrEmpty(line))
            {
                break;
            }

            bool continueSession = interpreter.ExecuteInputLine(line);
            if (!continueSession)
            {
                break;
            }
        }

        audio.Stop();
        ConsoleScreenDriver.ExitAlternateBuffer();
        screen.Dispose();
    }
}
