using GWBASIC.Core.Common;
using GWBASIC.Core.Parser;
using GWBASIC.Core.Parser.Statements;

namespace GWBASIC.Core.Runtime;

public class Interpreter
{
    public BasicEnvironment Environment { get; }
    public bool IsRunning { get; private set; }

    public Interpreter(BasicEnvironment environment)
    {
        Environment = environment;
    }

    /// <summary>
    /// Executes a single line from the interactive prompt (direct command or line numbered statement).
    /// </summary>
    /// <returns>True if the session should continue, false if SYSTEM was executed.</returns>
    public bool ExecuteInputLine(string inputLine)
    {
        if (Environment.IsAutoMode)
        {
            if (inputLine == "\x03" || inputLine.Contains('\x03') || inputLine.Equals("BREAK", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitAutoMode();
                Environment.Screen.WriteLine();
                Environment.Screen.WriteLine("Ok");
                return true;
            }

            string trimmed = inputLine.Trim();

            // If empty line entered in AUTO mode:
            if (string.IsNullOrEmpty(trimmed))
            {
                Environment.AutoLineNumber += Environment.AutoIncrement;
                Environment.PrintAutoPrompt();
                return true;
            }

            try
            {
                string lineToExecute;
                if (char.IsAsciiDigit(trimmed[0]))
                {
                    lineToExecute = trimmed;
                }
                else
                {
                    lineToExecute = $"{Environment.AutoLineNumber} {inputLine.TrimEnd()}";
                }

                var (lineNum, statements) = BasicParser.ParseLine(lineToExecute);
                if (lineNum.HasValue)
                {
                    Environment.Program.AddOrUpdateLine(lineNum.Value, lineToExecute, statements);
                    Environment.CurrentLineNumber = lineNum.Value;
                    Environment.RebuildDataItems();
                    Environment.AutoLineNumber = lineNum.Value + Environment.AutoIncrement;
                }
                else
                {
                    Environment.AutoLineNumber += Environment.AutoIncrement;
                }

                Environment.PrintAutoPrompt();
                return true;
            }
            catch (BasicException be)
            {
                Environment.Screen.WriteLine(be.Message);
                Environment.PrintAutoPrompt();
                return true;
            }
            catch (Exception ex)
            {
                Environment.Screen.WriteLine($"Error: {ex.Message}");
                Environment.PrintAutoPrompt();
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(inputLine))
            return true;

        try
        {
            var (lineNum, statements) = BasicParser.ParseLine(inputLine);
            if (lineNum.HasValue)
            {
                // Program line entry/edit/delete
                Environment.Program.AddOrUpdateLine(lineNum.Value, inputLine, statements);
                Environment.CurrentLineNumber = lineNum.Value;
                Environment.RebuildDataItems();
                return true;
            }

            // Direct mode statement execution
            foreach (var stmt in statements)
            {
                var res = stmt.Execute(Environment);
                if (res.Type == ResultType.Exit)
                    return false;

                if (res.Type == ResultType.JumpLine)
                {
                    RunFromLine(res.TargetLine);
                    break;
                }
                if (res.Type == ResultType.JumpStatement)
                {
                    RunFromStatement(res.TargetLine, res.TargetStatementIndex);
                    break;
                }
                if (res.Type == ResultType.Stop)
                {
                    break;
                }
            }

            if (Environment.IsAutoMode)
            {
                Environment.PrintAutoPrompt();
                return true;
            }

            Environment.Screen.WriteLine("Ok");
        }
        catch (BasicException be)
        {
            Environment.Screen.WriteLine(be.Message);
            Environment.Screen.WriteLine("Ok");
        }
        catch (Exception ex)
        {
            Environment.Screen.WriteLine($"Error: {ex.Message}");
            Environment.Screen.WriteLine("Ok");
        }

        return true;
    }

    /// <summary>
    /// Runs the stored program starting from the first line or specified line.
    /// </summary>
    public bool Run(int? startLine = null)
    {
        int? curLine = startLine ?? Environment.Program.GetFirstLineNumber();
        if (!curLine.HasValue)
        {
            Environment.Screen.WriteLine("Ok");
            return true;
        }

        return RunFromLine(curLine.Value);
    }

    private bool RunFromLine(int startLine) => RunFromStatement(startLine, 0);

    private bool RunFromStatement(int startLine, int startStmtIndex)
    {
        IsRunning = true;
        Environment.IsProgramRunning = true;
        try
        {
            int? curLine = startLine;
            int curStmtIndex = startStmtIndex;

            while (curLine.HasValue && IsRunning)
            {
                var line = Environment.Program.GetLine(curLine.Value);
                if (line == null)
                {
                    // Line not found
                    if (Environment.OnErrorLine.HasValue && !Environment.InErrorHandler)
                    {
                        HandleError(new BasicException(BasicErrorCode.UndefinedLineNumber, curLine.Value), curLine.Value, curStmtIndex, ref curLine, ref curStmtIndex);
                        continue;
                    }
                    Environment.Screen.WriteLine(BasicErrorMessages.GetMessage(BasicErrorCode.UndefinedLineNumber) + $" in {curLine.Value}");
                    Environment.Screen.WriteLine("Ok");
                    IsRunning = false;
                    return true;
                }

                Environment.CurrentLineNumber = curLine.Value;

                if (Environment.IsTron && curStmtIndex == 0)
                {
                    Environment.Screen.Write($"[{curLine.Value}]");
                }

                bool jumped = false;
                while (curStmtIndex < line.Statements.Count)
                {
                    Environment.CurrentStatementIndex = curStmtIndex;
                    var stmt = line.Statements[curStmtIndex];

                    try
                    {
                        var res = stmt.Execute(Environment);
                        switch (res.Type)
                        {
                            case ResultType.Continue:
                                curStmtIndex++;
                                break;

                            case ResultType.JumpLine:
                                curLine = res.TargetLine;
                                curStmtIndex = 0;
                                jumped = true;
                                break;

                            case ResultType.JumpStatement:
                                curLine = res.TargetLine;
                                curStmtIndex = res.TargetStatementIndex;
                                jumped = true;
                                break;

                            case ResultType.Stop:
                                Environment.IsPaused = true;
                                Environment.PauseLocation = (curLine.Value, curStmtIndex + 1);
                                IsRunning = false;
                                Environment.Screen.WriteLine("Ok");
                                return true;

                            case ResultType.Exit:
                                IsRunning = false;
                                return false;
                        }
                    }
                    catch (BasicException be)
                    {
                        be.LineNumber ??= curLine.Value;
                        if (Environment.OnErrorLine.HasValue && !Environment.InErrorHandler)
                        {
                            HandleError(be, curLine.Value, curStmtIndex, ref curLine, ref curStmtIndex);
                            jumped = true;
                            break;
                        }
                        string errText = curLine.HasValue ? $"{be.Message} in {curLine.Value}" : be.Message;
                        Environment.Screen.WriteLine(errText);
                        Environment.Screen.WriteLine("Ok");
                        IsRunning = false;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        var be = new BasicException(BasicErrorCode.InternalError, curLine.Value, ex.Message);
                        if (Environment.OnErrorLine.HasValue && !Environment.InErrorHandler)
                        {
                            HandleError(be, curLine.Value, curStmtIndex, ref curLine, ref curStmtIndex);
                            jumped = true;
                            break;
                        }
                        string errText = curLine.HasValue ? $"{be.Message} in {curLine.Value}" : be.Message;
                        Environment.Screen.WriteLine(errText);
                        Environment.Screen.WriteLine("Ok");
                        IsRunning = false;
                        return true;
                    }

                    if (jumped) break;
                }

                if (!jumped && curLine.HasValue)
                {
                    curLine = Environment.Program.GetNextLineNumber(curLine.Value);
                    curStmtIndex = 0;
                }
            }

            IsRunning = false;
            Environment.Screen.WriteLine("Ok");
            return true;
        }
        finally
        {
            IsRunning = false;
            Environment.IsProgramRunning = false;
        }
    }

    private void HandleError(BasicException be, int currentLine, int currentStmt, ref int? curLine, ref int curStmtIndex)
    {
        Environment.LastErrorCode = (int)be.ErrorCode;
        Environment.LastErrorLine = currentLine;
        Environment.ResumeStatement = (currentLine, currentStmt);
        Environment.InErrorHandler = true;

        curLine = Environment.OnErrorLine!.Value;
        curStmtIndex = 0;
    }
}
