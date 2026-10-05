using System.Globalization;
using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Parser;
using GWBASIC.Core.Parser.Expressions;
using GWBASIC.Core.Parser.Statements;

namespace GWBASIC.Core.Runtime;

public class BasicEnvironment
{
    public IScreenDriver Screen { get; }
    public IAudioDriver Audio { get; }
    public IInputDriver Input { get; }
    public IFileSystemDriver FileSystem { get; }
    public BasicProgram Program { get; }

    // Variable and Array storage
    private readonly Dictionary<string, BasicValue> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BasicArray> _arrays = new(StringComparer.OrdinalIgnoreCase);
    private readonly BasicType[] _defaultTypes = new BasicType[26];
    public int OptionBase { get; private set; } = 0;

    // User defined functions DEF FN
    private readonly Dictionary<string, (List<string> Parameters, Expression Body)> _userFunctions = new(StringComparer.OrdinalIgnoreCase);

    // Call stacks
    private readonly Stack<CallFrame> _callStack = new();
    private readonly Stack<ForLoopFrame> _forStack = new();
    private readonly Stack<WhileLoopFrame> _whileStack = new();

    // DATA statements
    private readonly List<(int LineNumber, string Value)> _dataItems = new();
    private int _dataIndex;

    // Error handling
    public int? OnErrorLine { get; set; }
    public bool InErrorHandler { get; set; }
    public int LastErrorCode { get; set; }
    public int LastErrorLine { get; set; }
    public (int Line, int StmtIndex) ResumeStatement { get; set; }

    // Files and FIELD buffers
    private readonly Dictionary<int, IFileHandle> _openFiles = new();
    private readonly Dictionary<int, byte[]> _randomRecordBuffers = new();
    private readonly Dictionary<int, List<(int Width, string VariableName)>> _fieldDefinitions = new();

    // Function keys (1-10)
    private readonly string[] _functionKeys = new string[10];

    // Execution state
    public bool IsProgramRunning { get; set; }
    public bool IsTron { get; set; }
    public int CurrentLineNumber { get; set; }
    public int CurrentStatementIndex { get; set; }
    public bool IsPaused { get; set; }
    public (int Line, int StmtIndex) PauseLocation { get; set; }

    // AUTO mode state
    public bool IsAutoMode { get; private set; }
    public int AutoLineNumber { get; set; }
    public int AutoIncrement { get; set; } = 10;

    public void StartAutoMode(int startLine, int increment)
    {
        IsAutoMode = true;
        AutoLineNumber = startLine;
        AutoIncrement = increment;
    }

    public void ExitAutoMode()
    {
        IsAutoMode = false;
    }

    public void PrintAutoPrompt()
    {
        if (!IsAutoMode) return;

        if (AutoLineNumber > 65529)
        {
            ExitAutoMode();
            Screen.WriteLine("Ok");
            return;
        }

        bool exists = Program.GetLine(AutoLineNumber) != null;
        string marker = exists ? "* " : " ";
        Screen.Write($"{AutoLineNumber}{marker}");
    }

    // Graphics state
    public int LastGraphicX { get; set; }
    public int LastGraphicY { get; set; }

    // Memory emulation for DEF SEG, PEEK, POKE
    public int DefSeg { get; set; }
    private readonly byte[] _memory = new byte[65536];

    // Random number generator
    private Random _random = new(1);
    private double _lastRnd = 0.0;

    public BasicEnvironment(IScreenDriver screen, IAudioDriver audio, IInputDriver input, IFileSystemDriver fileSystem)
    {
        Screen = screen;
        Audio = audio;
        Input = input;
        FileSystem = fileSystem;
        Program = new BasicProgram();

        ResetDefaults();
        InitializeFunctionKeys();
    }

    private void ResetDefaults()
    {
        for (int i = 0; i < 26; i++)
        {
            _defaultTypes[i] = BasicType.Single;
        }
        OptionBase = 0;
        DefSeg = 0;
    }

    private void InitializeFunctionKeys()
    {
        _functionKeys[0] = "LIST ";
        _functionKeys[1] = "RUN\r";
        _functionKeys[2] = "LOAD\"";
        _functionKeys[3] = "SAVE\"";
        _functionKeys[4] = "CONT\r";
        _functionKeys[5] = ",\"LPT1:\"\r";
        _functionKeys[6] = "TRON\r";
        _functionKeys[7] = "TROFF\r";
        _functionKeys[8] = "KEY ";
        _functionKeys[9] = "SCREEN 0,0,0\r";
        UpdateScreenKeyRow();
    }

    public void SetFunctionKey(int keyNum, string text)
    {
        if (keyNum is >= 1 and <= 10)
        {
            _functionKeys[keyNum - 1] = text;
            UpdateScreenKeyRow();
        }
    }

    public string GetFunctionKey(int keyNum) =>
        (keyNum is >= 1 and <= 10) ? _functionKeys[keyNum - 1] : "";

    private void UpdateScreenKeyRow()
    {
        string[] labels = new string[10];
        for (int i = 0; i < 10; i++)
        {
            string t = _functionKeys[i].Replace("\r", "<-");
            int keyNum = (i + 1) % 10;
            labels[i] = $"{keyNum}{t}";
        }
        Screen.SetKeyLabels(labels);
    }

    public void ListKeys()
    {
        for (int i = 0; i < 10; i++)
        {
            Screen.WriteLine($"F{i + 1}  {_functionKeys[i]}");
        }
    }

    #region Variables & Types

    public string CanonicalizeVariableName(string name)
    {
        string upper = name.ToUpperInvariant();
        char last = upper[^1];
        if (last is '%' or '!' or '#' or '$')
            return upper;

        char first = upper[0];
        BasicType defType = (first is >= 'A' and <= 'Z') ? _defaultTypes[first - 'A'] : BasicType.Single;
        char sigil = defType switch
        {
            BasicType.Integer => '%',
            BasicType.Single => '!',
            BasicType.Double => '#',
            BasicType.String => '$',
            _ => '!'
        };
        return upper + sigil;
    }

    public BasicType GetVariableType(string canonicalName) => canonicalName[^1] switch
    {
        '%' => BasicType.Integer,
        '!' => BasicType.Single,
        '#' => BasicType.Double,
        '$' => BasicType.String,
        _ => BasicType.Single
    };

    public void SetDefaultTypeRange(char start, char end, BasicType type)
    {
        int s = char.ToUpperInvariant(start) - 'A';
        int e = char.ToUpperInvariant(end) - 'A';
        if (s < 0 || s > 25 || e < 0 || e > 25 || s > e)
            throw new BasicException(BasicErrorCode.SyntaxError);

        for (int i = s; i <= e; i++)
        {
            _defaultTypes[i] = type;
        }
    }

    public void SetOptionBase(int b)
    {
        if (_arrays.Count > 0)
            throw new BasicException(BasicErrorCode.DuplicateDefinition);
        OptionBase = b;
    }

    public BasicValue GetVariable(string name)
    {
        string key = CanonicalizeVariableName(name);
        if (_variables.TryGetValue(key, out var val))
            return val;

        var type = GetVariableType(key);
        return type == BasicType.String ? BasicValue.EmptyString : BasicValue.Zero;
    }

    public void SetVariable(string name, BasicValue value)
    {
        string key = CanonicalizeVariableName(name);
        var type = GetVariableType(key);
        _variables[key] = value.ConvertTo(type);
    }

    public void SetVariableFromString(string name, string rawString)
    {
        string key = CanonicalizeVariableName(name);
        var type = GetVariableType(key);
        if (type == BasicType.String)
        {
            _variables[key] = BasicValue.FromString(rawString);
        }
        else
        {
            if (double.TryParse(rawString.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                _variables[key] = BasicValue.FromDouble(d).ConvertTo(type);
            }
            else
            {
                _variables[key] = BasicValue.Zero;
            }
        }
    }

    public void ClearVariables()
    {
        _variables.Clear();
        _arrays.Clear();
        _userFunctions.Clear();
        _callStack.Clear();
        _forStack.Clear();
        _whileStack.Clear();
        _dataIndex = 0;
        OnErrorLine = null;
        InErrorHandler = false;
        LastGraphicX = 0;
        LastGraphicY = 0;
        ResetDefaults();
        RebuildDataItems();
    }

    #endregion

    #region Arrays

    public void DimArray(string name, int[] upperBounds)
    {
        string key = CanonicalizeVariableName(name);
        if (_arrays.ContainsKey(key))
            throw new BasicException(BasicErrorCode.DuplicateDefinition);

        var type = GetVariableType(key);
        _arrays[key] = new BasicArray(key, type, upperBounds, OptionBase);
    }

    public BasicValue GetArrayElement(string name, int[] indices)
    {
        string key = CanonicalizeVariableName(name);
        if (!_arrays.TryGetValue(key, out var arr))
        {
            // Auto dimension to 10 for each index
            int[] defaultBounds = new int[indices.Length];
            Array.Fill(defaultBounds, 10);
            DimArray(name, defaultBounds);
            arr = _arrays[key];
        }

        return arr.GetElement(indices);
    }

    public void SetArrayElement(string name, int[] indices, BasicValue val)
    {
        string key = CanonicalizeVariableName(name);
        if (!_arrays.TryGetValue(key, out var arr))
        {
            int[] defaultBounds = new int[indices.Length];
            Array.Fill(defaultBounds, 10);
            DimArray(name, defaultBounds);
            arr = _arrays[key];
        }

        arr.SetElement(indices, val);
    }

    public void SetArrayElementFromString(string name, int[] indices, string raw)
    {
        string key = CanonicalizeVariableName(name);
        var type = GetVariableType(key);
        if (type == BasicType.String)
        {
            SetArrayElement(name, indices, BasicValue.FromString(raw));
        }
        else
        {
            if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                SetArrayElement(name, indices, BasicValue.FromDouble(d));
            }
            else
            {
                SetArrayElement(name, indices, BasicValue.Zero);
            }
        }
    }

    public void EraseArray(string name)
    {
        string key = CanonicalizeVariableName(name);
        _arrays.Remove(key);
    }

    #endregion

    #region User Defined Functions

    public void DefineFn(string name, List<string> parameters, Expression body)
    {
        string key = CanonicalizeVariableName(name);
        _userFunctions[key] = (parameters, body);
    }

    public BasicValue CallFn(string name, List<BasicValue> args)
    {
        string key = CanonicalizeVariableName(name);
        if (!_userFunctions.TryGetValue(key, out var fn))
            throw new BasicException(BasicErrorCode.UndefinedUserFunction);

        if (fn.Parameters.Count != args.Count)
            throw new BasicException(BasicErrorCode.SyntaxError);

        // Save existing variables with parameter names
        var savedValues = new Dictionary<string, BasicValue?>();
        for (int i = 0; i < fn.Parameters.Count; i++)
        {
            string pName = CanonicalizeVariableName(fn.Parameters[i]);
            savedValues[pName] = _variables.TryGetValue(pName, out var v) ? v : null;
            SetVariable(pName, args[i]);
        }

        try
        {
            return fn.Body.Evaluate(this);
        }
        finally
        {
            // Restore saved variables
            foreach (var kv in savedValues)
            {
                if (kv.Value.HasValue) _variables[kv.Key] = kv.Value.Value;
                else _variables.Remove(kv.Key);
            }
        }
    }

    #endregion

    #region DATA / READ / RESTORE

    public void RebuildDataItems()
    {
        _dataItems.Clear();
        foreach (var line in Program.GetLines())
        {
            foreach (var stmt in line.Statements)
            {
                if (stmt is DataStatement ds)
                {
                    foreach (var item in ds.Items)
                    {
                        _dataItems.Add((line.LineNumber, item));
                    }
                }
            }
        }
        _dataIndex = 0;
    }

    public string ReadDataItem()
    {
        if (_dataIndex >= _dataItems.Count)
            throw new BasicException(BasicErrorCode.OutOfData);
        return _dataItems[_dataIndex++].Value;
    }

    public void RestoreData(int? targetLine)
    {
        if (!targetLine.HasValue)
        {
            _dataIndex = 0;
            return;
        }

        int idx = _dataItems.FindIndex(d => d.LineNumber >= targetLine.Value);
        _dataIndex = idx >= 0 ? idx : _dataItems.Count;
    }

    #endregion

    #region Subroutines (GOSUB / RETURN)

    public void PushGosub()
    {
        _callStack.Push(new CallFrame(CurrentLineNumber, CurrentStatementIndex + 1));
    }

    public (int ReturnLine, int StatementIndex) PopGosub()
    {
        if (_callStack.Count == 0)
            throw new BasicException(BasicErrorCode.ReturnWithoutGosub);
        var frame = _callStack.Pop();
        return (frame.ReturnLine, frame.StatementIndex);
    }

    #endregion

    #region Loops (FOR/NEXT and WHILE/WEND)

    public void PushForLoop(string varName, BasicValue endVal, BasicValue stepVal)
    {
        string canonical = CanonicalizeVariableName(varName);
        _forStack.Push(new ForLoopFrame(canonical, endVal, stepVal, CurrentLineNumber, CurrentStatementIndex + 1));
    }

    public StatementResult ExecuteNext(string? varName)
    {
        if (_forStack.Count == 0)
            throw new BasicException(BasicErrorCode.NextWithoutFor);

        ForLoopFrame frame;
        if (varName == null)
        {
            frame = _forStack.Peek();
        }
        else
        {
            string canonical = CanonicalizeVariableName(varName);
            // Search down stack for matching variable
            while (_forStack.Count > 0 && _forStack.Peek().VariableName != canonical)
            {
                _forStack.Pop();
            }
            if (_forStack.Count == 0)
                throw new BasicException(BasicErrorCode.NextWithoutFor);
            frame = _forStack.Peek();
        }

        var currentVal = GetVariable(frame.VariableName);
        var nextVal = BasicValue.Add(currentVal, frame.StepValue);
        SetVariable(frame.VariableName, nextVal);

        double nextD = nextVal.AsDouble;
        double endD = frame.EndValue.AsDouble;
        double stepD = frame.StepValue.AsDouble;

        bool done = stepD >= 0 ? nextD > endD : nextD < endD;
        if (!done)
        {
            return StatementResult.JumpStatement(frame.LoopStartLine, frame.LoopStartStatementIndex);
        }

        _forStack.Pop();
        return StatementResult.Continue;
    }

    public StatementResult SkipToNext(string varName)
    {
        string canonical = CanonicalizeVariableName(varName);
        int? lineNum = CurrentLineNumber;

        while (lineNum.HasValue)
        {
            var line = Program.GetLine(lineNum.Value);
            if (line != null)
            {
                for (int i = (lineNum == CurrentLineNumber ? CurrentStatementIndex + 1 : 0); i < line.Statements.Count; i++)
                {
                    if (line.Statements[i] is NextStatement ns)
                    {
                        if (ns.VariableNames.Count == 0 || ns.VariableNames.Any(v => CanonicalizeVariableName(v) == canonical))
                        {
                            return StatementResult.JumpStatement(lineNum.Value, i + 1);
                        }
                    }
                }
            }
            lineNum = Program.GetNextLineNumber(lineNum.Value);
        }

        throw new BasicException(BasicErrorCode.ForWithoutNext);
    }

    public void PushWhileLoop()
    {
        _whileStack.Push(new WhileLoopFrame(CurrentLineNumber, CurrentStatementIndex));
    }

    public StatementResult ExecuteWend()
    {
        if (_whileStack.Count == 0)
            throw new BasicException(BasicErrorCode.WendWithoutWhile);
        var frame = _whileStack.Pop();
        return StatementResult.JumpStatement(frame.ConditionLine, frame.StatementIndex);
    }

    public StatementResult SkipToWend()
    {
        int depth = 1;
        int? lineNum = CurrentLineNumber;

        while (lineNum.HasValue)
        {
            var line = Program.GetLine(lineNum.Value);
            if (line != null)
            {
                for (int i = (lineNum == CurrentLineNumber ? CurrentStatementIndex + 1 : 0); i < line.Statements.Count; i++)
                {
                    if (line.Statements[i] is WhileStatement) depth++;
                    else if (line.Statements[i] is WendStatement)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return StatementResult.JumpStatement(lineNum.Value, i + 1);
                        }
                    }
                }
            }
            lineNum = Program.GetNextLineNumber(lineNum.Value);
        }

        throw new BasicException(BasicErrorCode.WhileWithoutWend);
    }

    #endregion

    #region Error Handling

    public void SetErrorHandler(int line)
    {
        OnErrorLine = line == 0 ? null : line;
    }

    public StatementResult ExecuteResume(ResumeTarget target, int? targetLine)
    {
        if (!InErrorHandler)
            throw new BasicException(BasicErrorCode.ResumeWithoutError);

        InErrorHandler = false;
        return target switch
        {
            ResumeTarget.Current => StatementResult.JumpStatement(ResumeStatement.Line, ResumeStatement.StmtIndex),
            ResumeTarget.Next => StatementResult.JumpStatement(ResumeStatement.Line, ResumeStatement.StmtIndex + 1),
            ResumeTarget.Line => StatementResult.JumpLine(targetLine!.Value),
            _ => throw new BasicException(BasicErrorCode.SyntaxError)
        };
    }

    #endregion

    #region File I/O & Random Access

    public void OpenFile(int fileNum, string fileName, FileModeType mode, int recordLength)
    {
        if (_openFiles.ContainsKey(fileNum))
            throw new BasicException(BasicErrorCode.FileAlreadyOpen);

        var handle = FileSystem.OpenFile(fileName, mode, recordLength);
        _openFiles[fileNum] = handle;
        if (mode == FileModeType.Random)
        {
            _randomRecordBuffers[fileNum] = new byte[recordLength];
        }
    }

    public void CloseFile(int fileNum)
    {
        if (_openFiles.TryGetValue(fileNum, out var handle))
        {
            handle.Close();
            _openFiles.Remove(fileNum);
            _randomRecordBuffers.Remove(fileNum);
            _fieldDefinitions.Remove(fileNum);
        }
    }

    public void CloseAllFiles()
    {
        foreach (var handle in _openFiles.Values)
        {
            handle.Close();
        }
        _openFiles.Clear();
        _randomRecordBuffers.Clear();
        _fieldDefinitions.Clear();
    }

    public bool IsEof(int fileNum) =>
        _openFiles.TryGetValue(fileNum, out var h) ? h.IsEof : throw new BasicException(BasicErrorCode.BadFileNumber);

    public double GetLof(int fileNum) =>
        _openFiles.TryGetValue(fileNum, out var h) ? h.Length : throw new BasicException(BasicErrorCode.BadFileNumber);

    public double GetLoc(int fileNum) =>
        _openFiles.TryGetValue(fileNum, out var h) ? h.Position : throw new BasicException(BasicErrorCode.BadFileNumber);

    public void DefineField(int fileNum, List<(int Width, string VariableName)> fields)
    {
        if (!_openFiles.ContainsKey(fileNum))
            throw new BasicException(BasicErrorCode.BadFileNumber);
        _fieldDefinitions[fileNum] = fields;
        foreach (var (width, vName) in fields)
        {
            SetVariable(vName, BasicValue.FromString(new string(' ', width)));
        }
    }

    public void LsetVariable(string name, string val)
    {
        string canonical = CanonicalizeVariableName(name);
        int len = 0;
        foreach (var list in _fieldDefinitions.Values)
        {
            var match = list.FirstOrDefault(f => CanonicalizeVariableName(f.VariableName) == canonical);
            if (match.Width > 0)
            {
                len = match.Width;
                break;
            }
        }
        if (len == 0)
        {
            string cur = GetVariable(canonical).AsString;
            len = cur.Length;
            if (len == 0) len = val.Length;
        }

        string res = val.Length >= len ? val[..len] : val.PadRight(len);
        SetVariable(canonical, BasicValue.FromString(res));
    }

    public void RsetVariable(string name, string val)
    {
        string canonical = CanonicalizeVariableName(name);
        int len = 0;
        foreach (var list in _fieldDefinitions.Values)
        {
            var match = list.FirstOrDefault(f => CanonicalizeVariableName(f.VariableName) == canonical);
            if (match.Width > 0)
            {
                len = match.Width;
                break;
            }
        }
        if (len == 0)
        {
            string cur = GetVariable(canonical).AsString;
            len = cur.Length;
            if (len == 0) len = val.Length;
        }

        string res = val.Length >= len ? val[..len] : val.PadLeft(len);
        SetVariable(canonical, BasicValue.FromString(res));
    }

    public void PutRecord(int fileNum, int? recNum)
    {
        if (!_openFiles.TryGetValue(fileNum, out var h) || !_randomRecordBuffers.TryGetValue(fileNum, out var buf))
            throw new BasicException(BasicErrorCode.BadFileNumber);

        // Pack field variables into record buffer
        if (_fieldDefinitions.TryGetValue(fileNum, out var fields))
        {
            int offset = 0;
            foreach (var (width, vName) in fields)
            {
                string s = GetVariable(vName).AsString;
                byte[] bytes = System.Text.Encoding.Latin1.GetBytes(s.PadRight(width));
                Array.Copy(bytes, 0, buf, offset, Math.Min(width, bytes.Length));
                offset += width;
            }
        }

        int rec = recNum ?? (int)(h.Position / h.RecordLength + 1);
        h.Put(rec, buf);
    }

    public void GetRecord(int fileNum, int? recNum)
    {
        if (!_openFiles.TryGetValue(fileNum, out var h) || !_randomRecordBuffers.TryGetValue(fileNum, out var buf))
            throw new BasicException(BasicErrorCode.BadFileNumber);

        int rec = recNum ?? (int)(h.Position / h.RecordLength + 1);
        var bytes = h.Get(rec);
        Array.Copy(bytes, buf, Math.Min(bytes.Length, buf.Length));

        // Unpack into field variables
        if (_fieldDefinitions.TryGetValue(fileNum, out var fields))
        {
            int offset = 0;
            foreach (var (width, vName) in fields)
            {
                byte[] part = new byte[width];
                Array.Copy(buf, offset, part, 0, Math.Min(width, buf.Length - offset));
                string s = System.Text.Encoding.Latin1.GetString(part);
                SetVariable(vName, BasicValue.FromString(s));
                offset += width;
            }
        }
    }

    public void FileInput(int fileNum, List<string> varNames)
    {
        if (!_openFiles.TryGetValue(fileNum, out var h))
            throw new BasicException(BasicErrorCode.BadFileNumber);

        string line = h.ReadLine();
        var parts = line.Split(',');
        for (int i = 0; i < varNames.Count; i++)
        {
            string p = i < parts.Length ? parts[i].Trim().Trim('"') : "";
            SetVariableFromString(varNames[i], p);
        }
    }

    public string FileLineInput(int fileNum)
    {
        if (!_openFiles.TryGetValue(fileNum, out var h))
            throw new BasicException(BasicErrorCode.BadFileNumber);
        return h.ReadLine();
    }

    #endregion

    #region Print & Console Output

    public void PrintOutput(int? fileNum, string text, bool isField)
    {
        if (fileNum.HasValue)
        {
            if (!_openFiles.TryGetValue(fileNum.Value, out var h))
                throw new BasicException(BasicErrorCode.BadFileNumber);
            h.Write(text);
        }
        else
        {
            Screen.Write(text);
        }
    }

    public void PrintNewline(int? fileNum)
    {
        if (fileNum.HasValue)
        {
            if (!_openFiles.TryGetValue(fileNum.Value, out var h))
                throw new BasicException(BasicErrorCode.BadFileNumber);
            h.WriteLine("");
        }
        else
        {
            Screen.WriteLine("");
        }
    }

    public void PrintCommaZone(int? fileNum)
    {
        if (fileNum.HasValue)
        {
            PrintOutput(fileNum, ",", false);
            return;
        }

        // Advance to next 14-char zone
        int col = Screen.CursorCol; // 1-based
        int zone = ((col - 1) / 14) + 1;
        int nextCol = zone * 14 + 1;
        if (nextCol > Screen.Width)
        {
            Screen.WriteLine();
        }
        else
        {
            Screen.Locate(Screen.CursorRow, nextCol);
        }
    }

    public void PrintTab(int? fileNum, int targetCol)
    {
        if (fileNum.HasValue) return;
        if (targetCol < Screen.CursorCol)
        {
            Screen.WriteLine();
        }
        int col = Math.Clamp(targetCol, 1, Screen.Width);
        Screen.Locate(Screen.CursorRow, col);
    }

    #endregion

    #region Memory & RND

    public byte Peek(int address)
    {
        int flat = (DefSeg * 16 + address) & 0xFFFF;
        return _memory[flat];
    }

    public void Poke(int address, byte value)
    {
        int flat = (DefSeg * 16 + address) & 0xFFFF;
        _memory[flat] = value;
    }

    public void Randomize(int seed)
    {
        _random = new Random(seed);
    }

    public BasicValue GetRnd(double x)
    {
        if (x < 0)
        {
            _random = new Random((int)Math.Round(x));
            _lastRnd = _random.NextDouble();
            return BasicValue.FromSingle((float)_lastRnd);
        }
        if (Math.Abs(x) < double.Epsilon)
        {
            return BasicValue.FromSingle((float)_lastRnd);
        }
        _lastRnd = _random.NextDouble();
        return BasicValue.FromSingle((float)_lastRnd);
    }

    #endregion

    #region Program Management Operations

    public void ListProgram(int? start, int? end)
    {
        foreach (var line in Program.GetLines(start, end))
        {
            Screen.WriteLine(line.ToString());
        }
    }

    public void NewProgram()
    {
        Program.Clear();
        ClearVariables();
        ExitAutoMode();
        CurrentLineNumber = 0;
        AutoIncrement = 10;
    }

    public void DeleteProgramLines(int? start, int? end)
    {
        Program.DeleteRange(start, end);
        RebuildDataItems();
    }

    public void RenumProgram(int newStart, int oldStart, int inc)
    {
        Program.Renumber(newStart, oldStart, inc);
        RebuildDataItems();
    }

    public void SaveProgram(string filename, bool ascii)
    {
        string fn = EnsureBasExtension(filename);
        var sb = new System.Text.StringBuilder();
        foreach (var line in Program.GetLines())
        {
            sb.AppendLine(line.ToString());
        }
        FileSystem.WriteAllText(fn, sb.ToString());
    }

    public void LoadProgram(string filename)
    {
        string fn = EnsureBasExtension(filename);
        string content = FileSystem.ReadAllText(fn);
        Program.Clear();
        ClearVariables();

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var (lineNum, statements) = BasicParser.ParseLine(line);
            if (lineNum.HasValue)
            {
                Program.AddOrUpdateLine(lineNum.Value, line, statements);
            }
        }
        RebuildDataItems();
    }

    public void MergeProgram(string filename)
    {
        string fn = EnsureBasExtension(filename);
        string content = FileSystem.ReadAllText(fn);
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var (lineNum, statements) = BasicParser.ParseLine(line);
            if (lineNum.HasValue)
            {
                Program.AddOrUpdateLine(lineNum.Value, line, statements);
            }
        }
        RebuildDataItems();
    }

    public void LoadAndRun(string filename)
    {
        LoadProgram(filename);
    }

    public StatementResult ContinueExecution()
    {
        if (!IsPaused)
            throw new BasicException(BasicErrorCode.CannotContinue);

        IsPaused = false;
        return StatementResult.JumpStatement(PauseLocation.Line, PauseLocation.StmtIndex);
    }

    public void ListFiles(string pattern)
    {
        var files = FileSystem.ListFiles(pattern);
        foreach (var f in files)
        {
            Screen.WriteLine(f);
        }
    }

    private static string EnsureBasExtension(string filename)
    {
        if (!filename.Contains('.'))
            return filename + ".BAS";
        return filename;
    }

    #endregion
}
