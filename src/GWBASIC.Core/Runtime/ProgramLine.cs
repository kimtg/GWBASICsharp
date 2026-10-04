using GWBASIC.Core.Parser.Statements;

namespace GWBASIC.Core.Runtime;

public class ProgramLine
{
    public int LineNumber { get; set; }
    public string Text { get; set; }
    public List<Statement> Statements { get; set; }

    public ProgramLine(int lineNumber, string text, List<Statement> statements)
    {
        LineNumber = lineNumber;
        Text = text;
        Statements = statements;
    }

    public override string ToString() => $"{LineNumber} {Text}";
}

public readonly record struct CallFrame(int ReturnLine, int StatementIndex);

public class ForLoopFrame
{
    public string VariableName { get; }
    public Common.BasicValue EndValue { get; }
    public Common.BasicValue StepValue { get; }
    public int LoopStartLine { get; }
    public int LoopStartStatementIndex { get; }

    public ForLoopFrame(string varName, Common.BasicValue endVal, Common.BasicValue stepVal, int line, int stmtIdx)
    {
        VariableName = varName;
        EndValue = endVal;
        StepValue = stepVal;
        LoopStartLine = line;
        LoopStartStatementIndex = stmtIdx;
    }
}

public readonly record struct WhileLoopFrame(int ConditionLine, int StatementIndex);
