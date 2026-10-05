using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Parser.Expressions;
using GWBASIC.Core.Runtime;

namespace GWBASIC.Core.Parser.Statements;

public enum ResultType
{
    Continue,
    JumpLine,
    JumpStatement,
    Stop,
    Exit
}

public readonly struct StatementResult
{
    public ResultType Type { get; }
    public int TargetLine { get; }
    public int TargetStatementIndex { get; }

    private StatementResult(ResultType type, int targetLine = 0, int targetStatementIndex = 0)
    {
        Type = type;
        TargetLine = targetLine;
        TargetStatementIndex = targetStatementIndex;
    }

    public static readonly StatementResult Continue = new(ResultType.Continue);
    public static readonly StatementResult Stop = new(ResultType.Stop);
    public static readonly StatementResult Exit = new(ResultType.Exit);
    public static StatementResult JumpLine(int line) => new(ResultType.JumpLine, line);
    public static StatementResult JumpStatement(int line, int stmtIdx) => new(ResultType.JumpStatement, line, stmtIdx);
}

public abstract class Statement
{
    public int? LineNumber { get; set; }
    public abstract StatementResult Execute(BasicEnvironment env);
}

public class RemStatement : Statement
{
    public string Comment { get; }
    public RemStatement(string comment) => Comment = comment;
    public override StatementResult Execute(BasicEnvironment env) => StatementResult.Continue;
}

public class LetStatement : Statement
{
    public string VariableName { get; }
    public Expression Value { get; }

    public LetStatement(string variableName, Expression value)
    {
        VariableName = variableName.ToUpperInvariant();
        Value = value;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        var val = Value.Evaluate(env);
        env.SetVariable(VariableName, val);
        return StatementResult.Continue;
    }
}

public class ArraySetStatement : Statement
{
    public string VariableName { get; }
    public List<Expression> Indices { get; }
    public Expression Value { get; }

    public ArraySetStatement(string variableName, List<Expression> indices, Expression value)
    {
        VariableName = variableName.ToUpperInvariant();
        Indices = indices;
        Value = value;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int[] evalIndices = new int[Indices.Count];
        for (int i = 0; i < Indices.Count; i++)
        {
            evalIndices[i] = Indices[i].Evaluate(env).AsInteger;
        }
        var val = Value.Evaluate(env);
        env.SetArrayElement(VariableName, evalIndices, val);
        return StatementResult.Continue;
    }
}

public class MidSetStatement : Statement
{
    public string VariableName { get; }
    public Expression Start { get; }
    public Expression? Length { get; }
    public Expression Replacement { get; }

    public MidSetStatement(string variableName, Expression start, Expression? length, Expression replacement)
    {
        VariableName = variableName.ToUpperInvariant();
        Start = start;
        Length = length;
        Replacement = replacement;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string current = env.GetVariable(VariableName).AsString;
        int s = Start.Evaluate(env).AsInteger;
        if (s <= 0 || s > current.Length)
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        string rep = Replacement.Evaluate(env).AsString;
        int len = Length?.Evaluate(env).AsInteger ?? rep.Length;
        if (len < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        int maxCharsToReplace = Math.Min(len, Math.Min(rep.Length, current.Length - s + 1));
        char[] chars = current.ToCharArray();
        for (int i = 0; i < maxCharsToReplace; i++)
        {
            chars[s - 1 + i] = rep[i];
        }

        env.SetVariable(VariableName, BasicValue.FromString(new string(chars)));
        return StatementResult.Continue;
    }
}

public enum PrintItemType { Expression, Comma, Semicolon, Tab, Spc }

public record PrintItem(PrintItemType Type, Expression? Expr = null);

public class PrintStatement : Statement
{
    public Expression? FileNumber { get; }
    public Expression? UsingFormat { get; }
    public List<PrintItem> Items { get; }

    public PrintStatement(Expression? fileNumber, Expression? usingFormat, List<PrintItem> items)
    {
        FileNumber = fileNumber;
        UsingFormat = usingFormat;
        Items = items;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? fNum = FileNumber != null ? FileNumber.Evaluate(env).AsInteger : null;
        string? formatStr = UsingFormat != null ? UsingFormat.Evaluate(env).AsString : null;

        if (formatStr != null)
        {
            // PRINT USING
            List<BasicValue> values = new();
            foreach (var item in Items)
            {
                if (item.Type == PrintItemType.Expression && item.Expr != null)
                {
                    values.Add(item.Expr.Evaluate(env));
                }
            }
            string formatted = PrintFormatter.FormatUsing(formatStr, values);
            env.PrintOutput(fNum, formatted, false);

            bool lastIsDelimiter = Items.Count > 0 && (Items[^1].Type == PrintItemType.Semicolon || Items[^1].Type == PrintItemType.Comma);
            if (!lastIsDelimiter)
            {
                env.PrintNewline(fNum);
            }
            else if (Items[^1].Type == PrintItemType.Comma)
            {
                env.PrintCommaZone(fNum);
            }
            return StatementResult.Continue;
        }

        // Standard PRINT
        bool lastWasDelimiter = false;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            lastWasDelimiter = false;
            switch (item.Type)
            {
                case PrintItemType.Expression:
                    if (item.Expr != null)
                    {
                        var val = item.Expr.Evaluate(env);
                        env.PrintOutput(fNum, val.ToBasicPrintString(), true);
                    }
                    break;
                case PrintItemType.Comma:
                    env.PrintCommaZone(fNum);
                    lastWasDelimiter = true;
                    break;
                case PrintItemType.Semicolon:
                    lastWasDelimiter = true;
                    break;
                case PrintItemType.Tab:
                    int col = item.Expr!.Evaluate(env).AsInteger;
                    env.PrintTab(fNum, col);
                    break;
                case PrintItemType.Spc:
                    int spaces = item.Expr!.Evaluate(env).AsInteger;
                    if (spaces > 0) env.PrintOutput(fNum, new string(' ', spaces), true);
                    break;
            }
        }

        if (!lastWasDelimiter)
        {
            env.PrintNewline(fNum);
        }

        return StatementResult.Continue;
    }
}

public class WriteStatement : Statement
{
    public Expression? FileNumber { get; }
    public List<Expression> Items { get; }

    public WriteStatement(Expression? fileNumber, List<Expression> items)
    {
        FileNumber = fileNumber;
        Items = items;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? fNum = FileNumber != null ? FileNumber.Evaluate(env).AsInteger : null;
        List<string> outputs = new();
        foreach (var expr in Items)
        {
            var val = expr.Evaluate(env);
            if (val.IsString)
                outputs.Add($"\"{val.AsString}\"");
            else
                outputs.Add(val.FormatBasicNumber());
        }
        string line = string.Join(",", outputs);
        env.PrintOutput(fNum, line + "\r\n", true);
        return StatementResult.Continue;
    }
}

public class InputStatement : Statement
{
    public Expression? FileNumber { get; }
    public bool SuppressNewline { get; }
    public string? Prompt { get; }
    public bool QuestionMarkPrompt { get; }
    public List<string> VariableNames { get; }

    public InputStatement(Expression? fileNumber, bool suppressNewline, string? prompt, bool questionMarkPrompt, List<string> variableNames)
    {
        FileNumber = fileNumber;
        SuppressNewline = suppressNewline;
        Prompt = prompt;
        QuestionMarkPrompt = questionMarkPrompt;
        VariableNames = variableNames.Select(v => v.ToUpperInvariant()).ToList();
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? fNum = FileNumber != null ? FileNumber.Evaluate(env).AsInteger : null;
        if (fNum.HasValue)
        {
            env.FileInput(fNum.Value, VariableNames);
            return StatementResult.Continue;
        }

        string fullPrompt = (Prompt ?? "") + (QuestionMarkPrompt ? "? " : "");
        env.Screen.Write(fullPrompt);

        while (true)
        {
            string line = env.Input.ReadLine();
            var values = ParseInputLine(line);
            if (values.Count < VariableNames.Count)
            {
                env.Screen.WriteLine("?Redo from start");
                env.Screen.Write(fullPrompt);
                continue;
            }

            for (int i = 0; i < VariableNames.Count; i++)
            {
                env.SetVariableFromString(VariableNames[i], values[i]);
            }
            break;
        }

        return StatementResult.Continue;
    }

    private static List<string> ParseInputLine(string line)
    {
        var result = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length) break;

            if (line[i] == '"')
            {
                i++;
                int start = i;
                while (i < line.Length && line[i] != '"') i++;
                result.Add(line[start..i]);
                if (i < line.Length && line[i] == '"') i++;
                while (i < line.Length && line[i] != ',') i++;
                if (i < line.Length && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != ',') i++;
                result.Add(line[start..i].Trim());
                if (i < line.Length && line[i] == ',') i++;
            }
        }
        return result;
    }
}

public class LineInputStatement : Statement
{
    public Expression? FileNumber { get; }
    public bool SuppressNewline { get; }
    public string? Prompt { get; }
    public string VariableName { get; }

    public LineInputStatement(Expression? fileNumber, bool suppressNewline, string? prompt, string variableName)
    {
        FileNumber = fileNumber;
        SuppressNewline = suppressNewline;
        Prompt = prompt;
        VariableName = variableName.ToUpperInvariant();
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? fNum = FileNumber != null ? FileNumber.Evaluate(env).AsInteger : null;
        if (fNum.HasValue)
        {
            string fileLine = env.FileLineInput(fNum.Value);
            env.SetVariable(VariableName, BasicValue.FromString(fileLine));
            return StatementResult.Continue;
        }

        if (!string.IsNullOrEmpty(Prompt))
        {
            env.Screen.Write(Prompt);
        }

        string line = env.Input.ReadLine();
        env.SetVariable(VariableName, BasicValue.FromString(line));
        return StatementResult.Continue;
    }
}

public class IfStatement : Statement
{
    public Expression Condition { get; }
    public List<Statement> ThenStatements { get; }
    public List<Statement>? ElseStatements { get; }

    public IfStatement(Expression condition, List<Statement> thenStatements, List<Statement>? elseStatements)
    {
        Condition = condition;
        ThenStatements = thenStatements;
        ElseStatements = elseStatements;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        bool isTrue = Condition.Evaluate(env).AsBoolean;
        var stmtsToRun = isTrue ? ThenStatements : ElseStatements;
        if (stmtsToRun == null || stmtsToRun.Count == 0)
            return StatementResult.Continue;

        foreach (var s in stmtsToRun)
        {
            var res = s.Execute(env);
            if (res.Type != ResultType.Continue)
                return res;
        }

        return StatementResult.Continue;
    }
}

public class GotoStatement : Statement
{
    public Expression TargetLineExpr { get; }
    public GotoStatement(Expression targetLineExpr) => TargetLineExpr = targetLineExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int line = TargetLineExpr.Evaluate(env).AsInteger;
        return StatementResult.JumpLine(line);
    }
}

public class GosubStatement : Statement
{
    public Expression TargetLineExpr { get; }
    public GosubStatement(Expression targetLineExpr) => TargetLineExpr = targetLineExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int line = TargetLineExpr.Evaluate(env).AsInteger;
        env.PushGosub();
        return StatementResult.JumpLine(line);
    }
}

public class ReturnStatement : Statement
{
    public Expression? TargetLineExpr { get; }
    public ReturnStatement(Expression? targetLineExpr) => TargetLineExpr = targetLineExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        if (TargetLineExpr != null)
        {
            int line = TargetLineExpr.Evaluate(env).AsInteger;
            env.PopGosub();
            return StatementResult.JumpLine(line);
        }

        var (retLine, retStmtIdx) = env.PopGosub();
        return StatementResult.JumpStatement(retLine, retStmtIdx);
    }
}

public class ForStatement : Statement
{
    public string VariableName { get; }
    public Expression StartExpr { get; }
    public Expression EndExpr { get; }
    public Expression? StepExpr { get; }

    public ForStatement(string variableName, Expression startExpr, Expression endExpr, Expression? stepExpr)
    {
        VariableName = variableName.ToUpperInvariant();
        StartExpr = startExpr;
        EndExpr = endExpr;
        StepExpr = stepExpr;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        var startVal = StartExpr.Evaluate(env);
        var endVal = EndExpr.Evaluate(env);
        var stepVal = StepExpr != null ? StepExpr.Evaluate(env) : BasicValue.One;

        env.SetVariable(VariableName, startVal);
        env.PushForLoop(VariableName, endVal, stepVal);

        // Check if initial condition should terminate immediately
        double step = stepVal.AsDouble;
        double start = startVal.AsDouble;
        double end = endVal.AsDouble;
        bool shouldTerminate = step >= 0 ? start > end : start < end;
        if (shouldTerminate)
        {
            // Skip loop body to matching NEXT
            return env.SkipToNext(VariableName);
        }

        return StatementResult.Continue;
    }
}

public class NextStatement : Statement
{
    public List<string> VariableNames { get; }

    public NextStatement(List<string>? variableNames = null)
    {
        VariableNames = variableNames?.Select(v => v.ToUpperInvariant()).ToList() ?? new List<string>();
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        if (VariableNames.Count == 0)
        {
            return env.ExecuteNext(null);
        }

        foreach (var varName in VariableNames)
        {
            var res = env.ExecuteNext(varName);
            if (res.Type != ResultType.Continue)
                return res;
        }

        return StatementResult.Continue;
    }
}

public class WhileStatement : Statement
{
    public Expression Condition { get; }
    public WhileStatement(Expression condition) => Condition = condition;

    public override StatementResult Execute(BasicEnvironment env)
    {
        bool isTrue = Condition.Evaluate(env).AsBoolean;
        if (isTrue)
        {
            env.PushWhileLoop();
            return StatementResult.Continue;
        }

        // Skip to matching WEND
        return env.SkipToWend();
    }
}

public class WendStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env) => env.ExecuteWend();
}

public class OnGotoStatement : Statement
{
    public Expression Selector { get; }
    public List<int> TargetLines { get; }
    public bool IsGosub { get; }

    public OnGotoStatement(Expression selector, List<int> targetLines, bool isGosub)
    {
        Selector = selector;
        TargetLines = targetLines;
        IsGosub = isGosub;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int index = Selector.Evaluate(env).AsInteger;
        if (index < 0 || index > 255)
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        if (index >= 1 && index <= TargetLines.Count)
        {
            int target = TargetLines[index - 1];
            if (IsGosub)
            {
                env.PushGosub();
            }
            return StatementResult.JumpLine(target);
        }

        return StatementResult.Continue;
    }
}

public class OnErrorGotoStatement : Statement
{
    public int TargetLine { get; }
    public OnErrorGotoStatement(int targetLine) => TargetLine = targetLine;

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.SetErrorHandler(TargetLine);
        return StatementResult.Continue;
    }
}

public enum ResumeTarget { Line, Next, Current }

public class ResumeStatement : Statement
{
    public ResumeTarget Target { get; }
    public int? LineNumberTarget { get; }

    public ResumeStatement(ResumeTarget target, int? lineNumberTarget = null)
    {
        Target = target;
        LineNumberTarget = lineNumberTarget;
    }

    public override StatementResult Execute(BasicEnvironment env) => env.ExecuteResume(Target, LineNumberTarget);
}

public class DataStatement : Statement
{
    public List<string> Items { get; }
    public DataStatement(List<string> items) => Items = items;
    public override StatementResult Execute(BasicEnvironment env) => StatementResult.Continue;
}

public class ReadStatement : Statement
{
    public List<(string Name, List<Expression>? Indices)> Targets { get; }

    public ReadStatement(List<(string Name, List<Expression>? Indices)> targets)
    {
        Targets = targets;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        foreach (var (name, indices) in Targets)
        {
            string raw = env.ReadDataItem();
            if (indices != null && indices.Count > 0)
            {
                int[] evalIndices = indices.Select(e => (int)e.Evaluate(env).AsInteger).ToArray();
                env.SetArrayElementFromString(name, evalIndices, raw);
            }
            else
            {
                env.SetVariableFromString(name, raw);
            }
        }
        return StatementResult.Continue;
    }
}

public class RestoreStatement : Statement
{
    public Expression? TargetLineExpr { get; }
    public RestoreStatement(Expression? targetLineExpr) => TargetLineExpr = targetLineExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? targetLine = TargetLineExpr != null ? TargetLineExpr.Evaluate(env).AsInteger : null;
        env.RestoreData(targetLine);
        return StatementResult.Continue;
    }
}

public class DimStatement : Statement
{
    public List<(string Name, List<Expression> Dimensions)> Dimensions { get; }
    public DimStatement(List<(string Name, List<Expression> Dimensions)> dimensions) => Dimensions = dimensions;

    public override StatementResult Execute(BasicEnvironment env)
    {
        foreach (var (name, dims) in Dimensions)
        {
            int[] upperBounds = dims.Select(d => (int)d.Evaluate(env).AsInteger).ToArray();
            env.DimArray(name, upperBounds);
        }
        return StatementResult.Continue;
    }
}

public class OptionBaseStatement : Statement
{
    public int Base { get; }
    public OptionBaseStatement(int b) => Base = b;

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.SetOptionBase(Base);
        return StatementResult.Continue;
    }
}

public class DefTypeStatement : Statement
{
    public BasicType Type { get; }
    public List<(char Start, char End)> Ranges { get; }

    public DefTypeStatement(BasicType type, List<(char Start, char End)> ranges)
    {
        Type = type;
        Ranges = ranges;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        foreach (var (start, end) in Ranges)
        {
            env.SetDefaultTypeRange(char.ToUpperInvariant(start), char.ToUpperInvariant(end), Type);
        }
        return StatementResult.Continue;
    }
}

public class DefFnStatement : Statement
{
    public string Name { get; }
    public List<string> Parameters { get; }
    public Expression Body { get; }

    public DefFnStatement(string name, List<string> parameters, Expression body)
    {
        Name = name.ToUpperInvariant();
        Parameters = parameters.Select(p => p.ToUpperInvariant()).ToList();
        Body = body;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.DefineFn(Name, Parameters, Body);
        return StatementResult.Continue;
    }
}

public class SwapStatement : Statement
{
    public (string Name, List<Expression>? Indices) Target1 { get; }
    public (string Name, List<Expression>? Indices) Target2 { get; }

    public SwapStatement((string Name, List<Expression>? Indices) t1, (string Name, List<Expression>? Indices) t2)
    {
        Target1 = (t1.Name.ToUpperInvariant(), t1.Indices);
        Target2 = (t2.Name.ToUpperInvariant(), t2.Indices);
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        BasicValue v1;
        int[]? idx1 = null;
        if (Target1.Indices != null && Target1.Indices.Count > 0)
        {
            idx1 = Target1.Indices.Select(i => (int)i.Evaluate(env).AsInteger).ToArray();
            v1 = env.GetArrayElement(Target1.Name, idx1);
        }
        else
        {
            v1 = env.GetVariable(Target1.Name);
        }

        BasicValue v2;
        int[]? idx2 = null;
        if (Target2.Indices != null && Target2.Indices.Count > 0)
        {
            idx2 = Target2.Indices.Select(i => (int)i.Evaluate(env).AsInteger).ToArray();
            v2 = env.GetArrayElement(Target2.Name, idx2);
        }
        else
        {
            v2 = env.GetVariable(Target2.Name);
        }

        if (v1.Type != v2.Type)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        if (idx1 != null) env.SetArrayElement(Target1.Name, idx1, v2);
        else env.SetVariable(Target1.Name, v2);

        if (idx2 != null) env.SetArrayElement(Target2.Name, idx2, v1);
        else env.SetVariable(Target2.Name, v1);

        return StatementResult.Continue;
    }
}

public class EraseStatement : Statement
{
    public List<string> ArrayNames { get; }
    public EraseStatement(List<string> arrayNames) => ArrayNames = arrayNames.Select(a => a.ToUpperInvariant()).ToList();

    public override StatementResult Execute(BasicEnvironment env)
    {
        foreach (var arr in ArrayNames)
        {
            env.EraseArray(arr);
        }
        return StatementResult.Continue;
    }
}

public class ClearStatement : Statement
{
    public Expression? MemorySize { get; }
    public ClearStatement(Expression? memSize) => MemorySize = memSize;

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.ClearVariables();
        return StatementResult.Continue;
    }
}

public class RandomizeStatement : Statement
{
    public Expression? SeedExpr { get; }
    public RandomizeStatement(Expression? seedExpr) => SeedExpr = seedExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int seed;
        if (SeedExpr != null)
        {
            seed = SeedExpr.Evaluate(env).AsInteger;
        }
        else
        {
            env.Screen.Write("Random number seed (-32768 to 32767)? ");
            string line = env.Input.ReadLine();
            seed = short.TryParse(line, out short s) ? s : 0;
        }
        env.Randomize(seed);
        return StatementResult.Continue;
    }
}

public class ClsStatement : Statement
{
    public Expression? ModeExpr { get; }
    public ClsStatement(Expression? modeExpr) => ModeExpr = modeExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int? mode = ModeExpr != null ? ModeExpr.Evaluate(env).AsInteger : null;
        env.Screen.Cls(mode);
        return StatementResult.Continue;
    }
}

public class LocateStatement : Statement
{
    public Expression? Row { get; }
    public Expression? Col { get; }
    public Expression? Cursor { get; }

    public LocateStatement(Expression? row, Expression? col, Expression? cursor)
    {
        Row = row;
        Col = col;
        Cursor = cursor;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int r = Row != null ? Row.Evaluate(env).AsInteger : env.Screen.CursorRow;
        int c = Col != null ? Col.Evaluate(env).AsInteger : env.Screen.CursorCol;
        bool? cur = Cursor != null ? Cursor.Evaluate(env).AsBoolean : null;
        env.Screen.Locate(r, c, cur);
        return StatementResult.Continue;
    }
}

public class ColorStatement : Statement
{
    public Expression? Foreground { get; }
    public Expression? Background { get; }
    public Expression? Border { get; }

    public ColorStatement(Expression? fg, Expression? bg, Expression? border)
    {
        Foreground = fg;
        Background = bg;
        Border = border;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int fg = Foreground != null ? Foreground.Evaluate(env).AsInteger : env.Screen.ForegroundColor;
        int bg = Background != null ? Background.Evaluate(env).AsInteger : env.Screen.BackgroundColor;
        int bdr = Border != null ? Border.Evaluate(env).AsInteger : 0;
        env.Screen.SetColors(fg, bg, bdr);
        return StatementResult.Continue;
    }
}

public class ScreenStatement : Statement
{
    public Expression Mode { get; }
    public Expression? ColorBurst { get; }
    public Expression? ActivePage { get; }
    public Expression? VisualPage { get; }

    public ScreenStatement(Expression mode, Expression? colorBurst, Expression? activePage, Expression? visualPage)
    {
        Mode = mode;
        ColorBurst = colorBurst;
        ActivePage = activePage;
        VisualPage = visualPage;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int m = Mode.Evaluate(env).AsInteger;
        env.Screen.SetMode(m);
        return StatementResult.Continue;
    }
}

public class WidthStatement : Statement
{
    public Expression Columns { get; }
    public WidthStatement(Expression cols) => Columns = cols;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int w = Columns.Evaluate(env).AsInteger;
        env.Screen.SetWidth(w);
        return StatementResult.Continue;
    }
}

public class PsetStatement : Statement
{
    public Expression X { get; }
    public Expression Y { get; }
    public Expression? Color { get; }
    public bool IsPreset { get; }

    public PsetStatement(Expression x, Expression y, Expression? color, bool isPreset)
    {
        X = x;
        Y = y;
        Color = color;
        IsPreset = isPreset;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int px = X.Evaluate(env).AsInteger;
        int py = Y.Evaluate(env).AsInteger;
        if (IsPreset)
        {
            if (Color != null)
                env.Screen.PSet(px, py, Color.Evaluate(env).AsInteger);
            else
                env.Screen.PReset(px, py);
        }
        else
        {
            int col = Color != null ? Color.Evaluate(env).AsInteger : env.Screen.ForegroundColor;
            env.Screen.PSet(px, py, col);
        }
        env.LastGraphicX = px;
        env.LastGraphicY = py;
        return StatementResult.Continue;
    }
}

public class LineGraphicsStatement : Statement
{
    public Expression? X1 { get; }
    public Expression? Y1 { get; }
    public Expression X2 { get; }
    public Expression Y2 { get; }
    public Expression? Color { get; }
    public bool Box { get; }
    public bool BoxFill { get; }
    public Expression? Style { get; }

    public LineGraphicsStatement(Expression? x1, Expression? y1, Expression x2, Expression y2, Expression? color, bool box, bool boxFill, Expression? style)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        Color = color;
        Box = box;
        BoxFill = boxFill;
        Style = style;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int x1 = X1 != null ? X1.Evaluate(env).AsInteger : env.LastGraphicX;
        int y1 = Y1 != null ? Y1.Evaluate(env).AsInteger : env.LastGraphicY;
        int x2 = X2.Evaluate(env).AsInteger;
        int y2 = Y2.Evaluate(env).AsInteger;
        int col = Color != null ? Color.Evaluate(env).AsInteger : env.Screen.ForegroundColor;
        ushort style = Style != null ? (ushort)Style.Evaluate(env).AsInteger : (ushort)0xFFFF;

        env.Screen.Line(x1, y1, x2, y2, col, Box, BoxFill, style);
        env.LastGraphicX = x2;
        env.LastGraphicY = y2;
        return StatementResult.Continue;
    }
}

public class CircleStatement : Statement
{
    public Expression X { get; }
    public Expression Y { get; }
    public Expression Radius { get; }
    public Expression? Color { get; }
    public Expression? Start { get; }
    public Expression? End { get; }
    public Expression? Aspect { get; }

    public CircleStatement(Expression x, Expression y, Expression radius, Expression? color, Expression? start, Expression? end, Expression? aspect)
    {
        X = x;
        Y = y;
        Radius = radius;
        Color = color;
        Start = start;
        End = end;
        Aspect = aspect;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int xc = X.Evaluate(env).AsInteger;
        int yc = Y.Evaluate(env).AsInteger;
        int r = Radius.Evaluate(env).AsInteger;
        int col = Color != null ? Color.Evaluate(env).AsInteger : env.Screen.ForegroundColor;
        double s = Start != null ? Start.Evaluate(env).AsDouble : 0.0;
        double e = End != null ? End.Evaluate(env).AsDouble : 2.0 * Math.PI;
        double asp = Aspect != null ? Aspect.Evaluate(env).AsDouble : (env.Screen.Mode == 1 ? 5.0 / 6.0 : 5.0 / 12.0);

        env.Screen.Circle(xc, yc, r, col, s, e, asp);
        env.LastGraphicX = xc;
        env.LastGraphicY = yc;
        return StatementResult.Continue;
    }
}

public class PaintStatement : Statement
{
    public Expression X { get; }
    public Expression Y { get; }
    public Expression? PaintColor { get; }
    public Expression? BoundaryColor { get; }

    public PaintStatement(Expression x, Expression y, Expression? paintColor, Expression? boundaryColor)
    {
        X = x;
        Y = y;
        PaintColor = paintColor;
        BoundaryColor = boundaryColor;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int x = X.Evaluate(env).AsInteger;
        int y = Y.Evaluate(env).AsInteger;
        int pCol = PaintColor != null ? PaintColor.Evaluate(env).AsInteger : env.Screen.ForegroundColor;
        int bCol = BoundaryColor != null ? BoundaryColor.Evaluate(env).AsInteger : pCol;

        env.Screen.Paint(x, y, pCol, bCol);
        return StatementResult.Continue;
    }
}

public class DrawStatement : Statement
{
    public Expression CommandExpr { get; }
    public DrawStatement(Expression cmdExpr) => CommandExpr = cmdExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        string cmd = CommandExpr.Evaluate(env).AsString;
        DrawEngine.Execute(cmd, env);
        return StatementResult.Continue;
    }
}

public class SoundStatement : Statement
{
    public Expression Frequency { get; }
    public Expression Duration { get; }

    public SoundStatement(Expression freq, Expression dur)
    {
        Frequency = freq;
        Duration = dur;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int freq = Frequency.Evaluate(env).AsInteger;
        int dur = Duration.Evaluate(env).AsInteger;
        env.Audio.Sound(freq, dur);
        return StatementResult.Continue;
    }
}

public class BeepStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env)
    {
        env.Audio.Beep();
        return StatementResult.Continue;
    }
}

public class PlayStatement : Statement
{
    public Expression CommandExpr { get; }
    public PlayStatement(Expression cmdExpr) => CommandExpr = cmdExpr;

    public override StatementResult Execute(BasicEnvironment env)
    {
        string cmd = CommandExpr.Evaluate(env).AsString;
        env.Audio.Play(cmd);
        return StatementResult.Continue;
    }
}

public class PokeStatement : Statement
{
    public Expression Address { get; }
    public Expression Value { get; }

    public PokeStatement(Expression addr, Expression val)
    {
        Address = addr;
        Value = val;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int addr = Address.Evaluate(env).AsInteger;
        int val = Value.Evaluate(env).AsInteger;
        env.Poke(addr, (byte)val);
        return StatementResult.Continue;
    }
}

public class DefSegStatement : Statement
{
    public Expression? Segment { get; }
    public DefSegStatement(Expression? seg) => Segment = seg;

    public override StatementResult Execute(BasicEnvironment env)
    {
        int seg = Segment != null ? Segment.Evaluate(env).AsInteger : 0;
        env.DefSeg = seg;
        return StatementResult.Continue;
    }
}

public class OpenStatement : Statement
{
    public Expression FileName { get; }
    public FileModeType Mode { get; }
    public Expression FileNumber { get; }
    public Expression? RecordLength { get; }

    public OpenStatement(Expression fileName, FileModeType mode, Expression fileNumber, Expression? recordLength)
    {
        FileName = fileName;
        Mode = mode;
        FileNumber = fileNumber;
        RecordLength = recordLength;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string name = FileName.Evaluate(env).AsString;
        int num = FileNumber.Evaluate(env).AsInteger;
        int reclen = RecordLength != null ? RecordLength.Evaluate(env).AsInteger : 128;
        env.OpenFile(num, name, Mode, reclen);
        return StatementResult.Continue;
    }
}

public class CloseStatement : Statement
{
    public List<Expression>? FileNumbers { get; }
    public CloseStatement(List<Expression>? fileNumbers) => FileNumbers = fileNumbers;

    public override StatementResult Execute(BasicEnvironment env)
    {
        if (FileNumbers == null || FileNumbers.Count == 0)
        {
            env.CloseAllFiles();
        }
        else
        {
            foreach (var fn in FileNumbers)
            {
                int num = fn.Evaluate(env).AsInteger;
                env.CloseFile(num);
            }
        }
        return StatementResult.Continue;
    }
}

public class FieldStatement : Statement
{
    public Expression FileNumber { get; }
    public List<(Expression Width, string VariableName)> Fields { get; }

    public FieldStatement(Expression fileNumber, List<(Expression Width, string VariableName)> fields)
    {
        FileNumber = fileNumber;
        Fields = fields;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int num = FileNumber.Evaluate(env).AsInteger;
        var evaluatedFields = Fields.Select(f => ((int)f.Width.Evaluate(env).AsInteger, f.VariableName.ToUpperInvariant())).ToList();
        env.DefineField(num, evaluatedFields);
        return StatementResult.Continue;
    }
}

public class LsetStatement : Statement
{
    public string VariableName { get; }
    public Expression Value { get; }

    public LsetStatement(string varName, Expression value)
    {
        VariableName = varName.ToUpperInvariant();
        Value = value;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string val = Value.Evaluate(env).AsString;
        env.LsetVariable(VariableName, val);
        return StatementResult.Continue;
    }
}

public class RsetStatement : Statement
{
    public string VariableName { get; }
    public Expression Value { get; }

    public RsetStatement(string varName, Expression value)
    {
        VariableName = varName.ToUpperInvariant();
        Value = value;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string val = Value.Evaluate(env).AsString;
        env.RsetVariable(VariableName, val);
        return StatementResult.Continue;
    }
}

public class PutStatement : Statement
{
    public Expression FileNumber { get; }
    public Expression? RecordNumber { get; }

    public PutStatement(Expression fileNum, Expression? recNum)
    {
        FileNumber = fileNum;
        RecordNumber = recNum;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int num = FileNumber.Evaluate(env).AsInteger;
        int? rec = RecordNumber != null ? RecordNumber.Evaluate(env).AsInteger : null;
        env.PutRecord(num, rec);
        return StatementResult.Continue;
    }
}

public class GetStatement : Statement
{
    public Expression FileNumber { get; }
    public Expression? RecordNumber { get; }

    public GetStatement(Expression fileNum, Expression? recNum)
    {
        FileNumber = fileNum;
        RecordNumber = recNum;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        int num = FileNumber.Evaluate(env).AsInteger;
        int? rec = RecordNumber != null ? RecordNumber.Evaluate(env).AsInteger : null;
        env.GetRecord(num, rec);
        return StatementResult.Continue;
    }
}

public class TronStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env)
    {
        env.IsTron = true;
        return StatementResult.Continue;
    }
}

public class TroffStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env)
    {
        env.IsTron = false;
        return StatementResult.Continue;
    }
}

public class StopStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env)
    {
        env.Screen.WriteLine($"Break in {LineNumber ?? 0}");
        return StatementResult.Stop;
    }
}

public class EndStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env) => StatementResult.Stop;
}

public class SystemStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env) => StatementResult.Exit;
}

public class ListStatement : Statement
{
    public int? StartLine { get; }
    public int? EndLine { get; }
    public ListStatement(int? start, int? end) { StartLine = start; EndLine = end; }

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.ListProgram(StartLine, EndLine);
        return StatementResult.Continue;
    }
}

public class LlistStatement : ListStatement
{
    public LlistStatement(int? start, int? end) : base(start, end) { }
}

public class NewStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env)
    {
        env.NewProgram();
        return StatementResult.Continue;
    }
}

public class RunStatement : Statement
{
    public Expression? TargetExpr { get; }
    public RunStatement(Expression? target) => TargetExpr = target;

    public override StatementResult Execute(BasicEnvironment env)
    {
        if (TargetExpr != null)
        {
            var val = TargetExpr.Evaluate(env);
            if (val.IsString)
            {
                env.LoadAndRun(val.AsString);
                return StatementResult.JumpLine(env.Program.GetFirstLineNumber() ?? 0);
            }
            return StatementResult.JumpLine(val.AsInteger);
        }

        env.ClearVariables();
        int? firstLine = env.Program.GetFirstLineNumber();
        return firstLine.HasValue ? StatementResult.JumpLine(firstLine.Value) : StatementResult.Continue;
    }
}

public class ContStatement : Statement
{
    public override StatementResult Execute(BasicEnvironment env) => env.ContinueExecution();
}

public class RenumStatement : Statement
{
    public int? NewStart { get; }
    public int? OldStart { get; }
    public int? Increment { get; }

    public RenumStatement(int? newStart, int? oldStart, int? inc)
    {
        NewStart = newStart;
        OldStart = oldStart;
        Increment = inc;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.RenumProgram(NewStart ?? 10, OldStart ?? 0, Increment ?? 10);
        return StatementResult.Continue;
    }
}

public class AutoStatement : Statement
{
    public int? StartLine { get; }
    public int? Increment { get; }
    public bool UseCurrentLine { get; }

    public AutoStatement(int? startLine, int? increment, bool useCurrentLine = false)
    {
        StartLine = startLine;
        Increment = increment;
        UseCurrentLine = useCurrentLine;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        if (env.IsProgramRunning)
        {
            throw new BasicException(BasicErrorCode.IllegalDirect);
        }

        int start = StartLine ?? 10;
        if (UseCurrentLine)
        {
            start = env.CurrentLineNumber > 0 ? env.CurrentLineNumber : 10;
        }

        if (start < 0 || start > 65529)
        {
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);
        }

        int inc = Increment ?? env.AutoIncrement;
        if (Increment.HasValue)
        {
            if (Increment.Value <= 0 || Increment.Value > 65529)
            {
                throw new BasicException(BasicErrorCode.IllegalFunctionCall);
            }
            env.AutoIncrement = Increment.Value;
        }

        env.StartAutoMode(start, inc);
        return StatementResult.Continue;
    }
}

public class DeleteStatement : Statement
{
    public int? StartLine { get; }
    public int? EndLine { get; }
    public DeleteStatement(int? start, int? end) { StartLine = start; EndLine = end; }

    public override StatementResult Execute(BasicEnvironment env)
    {
        env.DeleteProgramLines(StartLine, EndLine);
        return StatementResult.Continue;
    }
}

public class LoadStatement : Statement
{
    public Expression FileName { get; }
    public bool RunAfterLoad { get; }

    public LoadStatement(Expression fileName, bool runAfterLoad)
    {
        FileName = fileName;
        RunAfterLoad = runAfterLoad;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string file = FileName.Evaluate(env).AsString;
        env.LoadProgram(file);
        if (RunAfterLoad)
        {
            return env.Program.GetFirstLineNumber().HasValue
                ? StatementResult.JumpLine(env.Program.GetFirstLineNumber()!.Value)
                : StatementResult.Continue;
        }
        return StatementResult.Continue;
    }
}

public class SaveStatement : Statement
{
    public Expression FileName { get; }
    public bool AsciiMode { get; }

    public SaveStatement(Expression fileName, bool asciiMode)
    {
        FileName = fileName;
        AsciiMode = asciiMode;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string file = FileName.Evaluate(env).AsString;
        env.SaveProgram(file, AsciiMode);
        return StatementResult.Continue;
    }
}

public class MergeStatement : Statement
{
    public Expression FileName { get; }
    public MergeStatement(Expression fileName) => FileName = fileName;

    public override StatementResult Execute(BasicEnvironment env)
    {
        string file = FileName.Evaluate(env).AsString;
        env.MergeProgram(file);
        return StatementResult.Continue;
    }
}

public class FilesStatement : Statement
{
    public Expression? Pattern { get; }
    public FilesStatement(Expression? pattern) => Pattern = pattern;

    public override StatementResult Execute(BasicEnvironment env)
    {
        string pat = Pattern != null ? Pattern.Evaluate(env).AsString : "*.*";
        env.ListFiles(pat);
        return StatementResult.Continue;
    }
}

public class KillStatement : Statement
{
    public Expression FileName { get; }
    public KillStatement(Expression fileName) => FileName = fileName;

    public override StatementResult Execute(BasicEnvironment env)
    {
        string file = FileName.Evaluate(env).AsString;
        env.FileSystem.DeleteFile(file);
        return StatementResult.Continue;
    }
}

public class NameStatement : Statement
{
    public Expression OldName { get; }
    public Expression NewName { get; }

    public NameStatement(Expression oldName, Expression newName)
    {
        OldName = oldName;
        NewName = newName;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        string oldN = OldName.Evaluate(env).AsString;
        string newN = NewName.Evaluate(env).AsString;
        env.FileSystem.RenameFile(oldN, newN);
        return StatementResult.Continue;
    }
}

public enum KeyCommandType { Set, On, Off, List }

public class KeyStatement : Statement
{
    public KeyCommandType CommandType { get; }
    public int? KeyNumber { get; }
    public Expression? KeyString { get; }

    public KeyStatement(KeyCommandType cmd, int? num = null, Expression? str = null)
    {
        CommandType = cmd;
        KeyNumber = num;
        KeyString = str;
    }

    public override StatementResult Execute(BasicEnvironment env)
    {
        switch (CommandType)
        {
            case KeyCommandType.On:
                env.Screen.KeyRowVisible = true;
                break;
            case KeyCommandType.Off:
                env.Screen.KeyRowVisible = false;
                break;
            case KeyCommandType.List:
                env.ListKeys();
                break;
            case KeyCommandType.Set:
                if (KeyNumber.HasValue && KeyString != null)
                {
                    string text = KeyString.Evaluate(env).AsString;
                    env.SetFunctionKey(KeyNumber.Value, text);
                }
                break;
        }
        return StatementResult.Continue;
    }
}
