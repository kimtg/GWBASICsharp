using System.Text.RegularExpressions;
using GWBASIC.Core.Common;
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
        if (increment <= 0 || newStart <= 0)
            throw new BasicException(BasicErrorCode.IllegalFunctionCall);

        var oldLines = _lines.OrderBy(kv => kv.Key).ToList();
        var renumMap = new Dictionary<int, int>();

        int currentNew = newStart;
        foreach (var kv in oldLines)
        {
            if (kv.Key >= oldStart)
            {
                renumMap[kv.Key] = currentNew;
                currentNew += increment;
            }
            else
            {
                renumMap[kv.Key] = kv.Key;
            }
        }

        // Patch references in code using regex patterns
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
        // Replace line references after GOTO, GOSUB, THEN, ELSE, RUN, RESTORE
        string pattern = @"\b(GOTO|GOSUB|THEN|ELSE|RUN|RESTORE)\s+(\d+)\b";
        string patched = Regex.Replace(sourceText, pattern, m =>
        {
            string keyword = m.Groups[1].Value;
            int oldLine = int.Parse(m.Groups[2].Value);
            int newLine = map.TryGetValue(oldLine, out int nl) ? nl : oldLine;
            return $"{keyword} {newLine}";
        }, RegexOptions.IgnoreCase);

        // Replace ON ... GOTO / GOSUB lists
        string onPattern = @"\bON\b.+?\b(GOTO|GOSUB)\s+([0-9,\s]+)";
        patched = Regex.Replace(patched, onPattern, m =>
        {
            string full = m.Value;
            int idx = full.IndexOf(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            string prefix = full[..(idx + m.Groups[1].Length)];
            string nums = full[(idx + m.Groups[1].Length)..];

            var parts = nums.Split(',').Select(p =>
            {
                if (int.TryParse(p.Trim(), out int ol) && map.TryGetValue(ol, out int nl))
                    return $" {nl}";
                return p;
            });
            return prefix + string.Join(",", parts);
        }, RegexOptions.IgnoreCase);

        return patched;
    }
}
