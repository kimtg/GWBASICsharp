using System.Text;
using System.Text.RegularExpressions;
using GWBASIC.Core.Common;
using GWBASIC.Core.Lexer;
using GWBASIC.Core.Parser;
using GWBASIC.Core.Parser.Statements;

namespace GWBASIC.Core.Runtime;

public class BasicProgram
{
    private readonly SortedDictionary<int, ProgramLine> _lines = new();

    public int Count => _lines.Count;

    public void AddOrUpdateLine(int lineNumber, string text, List<Statement> statements)
    {
        string trimmed = text.Trim();
        // If line is empty or just the line number itself, delete line
        if (string.IsNullOrEmpty(trimmed) || trimmed == lineNumber.ToString())
        {
            DeleteLine(lineNumber);
            return;
        }

        string stmtText = trimmed;
        if (stmtText.StartsWith(lineNumber.ToString()))
        {
            stmtText = stmtText[lineNumber.ToString().Length..].TrimStart();
        }

        _lines[lineNumber] = new ProgramLine(lineNumber, stmtText, statements);
    }

    public bool DeleteLine(int lineNumber) => _lines.Remove(lineNumber);

    public void DeleteRange(int? start, int? end)
    {
        int s = start ?? 0;
        int e = end ?? int.MaxValue;
        var toRemove = _lines.Keys.Where(k => k >= s && k <= e).ToList();
        foreach (var k in toRemove)
        {
            _lines.Remove(k);
        }
    }

    public void Clear() => _lines.Clear();

    public ProgramLine? GetLine(int lineNumber) => _lines.TryGetValue(lineNumber, out var line) ? line : null;

    public int? GetFirstLineNumber() => _lines.Count > 0 ? _lines.Keys.First() : null;

    public int? GetNextLineNumber(int currentLineNumber)
    {
        foreach (var key in _lines.Keys)
        {
            if (key > currentLineNumber) return key;
        }
        return null;
    }

    public IEnumerable<ProgramLine> GetLines(int? start = null, int? end = null)
    {
        int s = start ?? 0;
        int e = end ?? int.MaxValue;
        foreach (var kvp in _lines)
        {
            if (kvp.Key >= s && kvp.Key <= e)
            {
                yield return kvp.Value;
            }
        }
    }

    public void Renumber(int newStart = 10, int oldStart = 0, int increment = 10)
    {
        if (increment <= 0 || newStart <= 0 || newStart > 65529)
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        var oldLines = _lines.OrderBy(kv => kv.Key).ToList();
        var renumMap = new Dictionary<int, int>();

        int currentNew = newStart;
        foreach (var kv in oldLines)
        {
            if (kv.Key >= oldStart)
            {
                if (currentNew > 65529)
                    throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                renumMap[kv.Key] = currentNew;
                currentNew += increment;
            }
            else
            {
                renumMap[kv.Key] = kv.Key;
            }
        }

        if (renumMap.Values.Distinct().Count() != renumMap.Count)
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        // Patch references in code using token-aware parsing
        var newLines = new SortedDictionary<int, ProgramLine>();
        foreach (var kv in oldLines)
        {
            int newLineNum = renumMap[kv.Key];
            string patchedText = PatchLineReferences(kv.Value.Text, renumMap);
            var (parsedLineNum, statements) = BasicParser.ParseLine($"{newLineNum} {patchedText}");
            newLines[newLineNum] = new ProgramLine(newLineNum, patchedText, statements);
        }

        _lines.Clear();
        foreach (var kv in newLines)
        {
            _lines[kv.Key] = kv.Value;
        }
    }

    private static string PatchLineReferences(string sourceText, Dictionary<int, int> map)
    {
        var lexer = new BasicLexer(sourceText);
        var tokens = lexer.Tokenize();
        var replacements = new List<(int Start, int Length, string NewText)>();

        for (int i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];
            if (tok.Type is TokenType.Goto or TokenType.Gosub or TokenType.Restore or TokenType.Run or TokenType.Resume)
            {
                if (i + 1 < tokens.Count && tokens[i + 1].Type == TokenType.IntegerLiteral)
                {
                    int oldLine = (int)tokens[i + 1].Value!.Value.AsInteger;
                    if (map.TryGetValue(oldLine, out int newLine))
                    {
                        replacements.Add((tokens[i + 1].Position, tokens[i + 1].Text.Length, newLine.ToString()));
                    }
                    i++;
                }
            }
            else if (tok.Type is TokenType.Then or TokenType.Else)
            {
                if (i + 1 < tokens.Count && tokens[i + 1].Type == TokenType.IntegerLiteral)
                {
                    int oldLine = (int)tokens[i + 1].Value!.Value.AsInteger;
                    if (map.TryGetValue(oldLine, out int newLine))
                    {
                        replacements.Add((tokens[i + 1].Position, tokens[i + 1].Text.Length, newLine.ToString()));
                    }
                    i++;
                }
            }
            else if (tok.Type == TokenType.On)
            {
                // Advance until Goto or Gosub
                while (i < tokens.Count && tokens[i].Type is not (TokenType.Goto or TokenType.Gosub or TokenType.EndOfLine or TokenType.Colon))
                {
                    i++;
                }

                if (i < tokens.Count && tokens[i].Type is (TokenType.Goto or TokenType.Gosub))
                {
                    i++; // move past Goto / Gosub
                    while (i < tokens.Count && (tokens[i].Type == TokenType.IntegerLiteral || tokens[i].Type == TokenType.Comma))
                    {
                        if (tokens[i].Type == TokenType.IntegerLiteral)
                        {
                            int oldLine = (int)tokens[i].Value!.Value.AsInteger;
                            if (map.TryGetValue(oldLine, out int newLine))
                            {
                                replacements.Add((tokens[i].Position, tokens[i].Text.Length, newLine.ToString()));
                            }
                        }
                        i++;
                    }
                    i--; // back up so outer loop can continue
                }
            }
        }

        if (replacements.Count == 0)
            return sourceText;

        // Apply replacements from right to left so earlier indices remain valid
        var sb = new StringBuilder(sourceText);
        foreach (var (start, len, newText) in replacements.OrderByDescending(r => r.Start))
        {
            if (start >= 0 && start + len <= sb.Length)
            {
                sb.Remove(start, len);
                sb.Insert(start, newText);
            }
        }

        return sb.ToString();
    }
}
