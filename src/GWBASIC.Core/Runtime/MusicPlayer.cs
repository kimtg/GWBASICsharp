namespace GWBASIC.Core.Runtime;

public record MusicNote(int FrequencyHz, int DurationMs, int RestMs);

public record MusicPlayResult(List<MusicNote> Notes, bool IsBackground);

public static class MusicPlayer
{
    private static readonly Dictionary<string, int> SemitonesFromC = new(StringComparer.OrdinalIgnoreCase)
    {
        { "C", 0 }, { "C#", 1 }, { "C+", 1 }, { "D-", 1 },
        { "D", 2 }, { "D#", 3 }, { "D+", 3 }, { "E-", 3 },
        { "E", 4 },
        { "F", 5 }, { "F#", 6 }, { "F+", 6 }, { "G-", 6 },
        { "G", 7 }, { "G#", 8 }, { "G+", 8 }, { "A-", 8 },
        { "A", 9 }, { "A#", 10 }, { "A+", 10 }, { "B-", 10 },
        { "B", 11 }
    };

    public static List<MusicNote> Parse(string commands, BasicEnvironment? env = null) =>
        ParseWithMode(commands, env).Notes;

    public static MusicPlayResult ParseWithMode(
        string commands,
        BasicEnvironment? env = null,
        int initialOctave = 4,
        int initialDefaultLength = 4,
        int initialTempo = 120,
        double initialNoteFraction = 7.0 / 8.0,
        bool initialBackground = false)
    {
        var notes = new List<MusicNote>();
        int octave = initialOctave;
        int defaultLength = initialDefaultLength; // quarter note
        int tempo = initialTempo; // 120 quarter notes per minute
        double noteFraction = initialNoteFraction; // MN default
        bool isBackground = initialBackground;

        int pos = 0;
        while (pos < commands.Length)
        {
            char c = char.ToUpperInvariant(commands[pos++]);
            if (char.IsWhiteSpace(c) || c == ';') continue;

            if (c == 'O')
            {
                int oct = ReadValue(commands, ref pos, env);
                octave = Math.Clamp(oct, 0, 6);
            }
            else if (c == '>')
            {
                if (octave < 6) octave++;
            }
            else if (c == '<')
            {
                if (octave > 0) octave--;
            }
            else if (c == 'L')
            {
                int len = ReadValue(commands, ref pos, env);
                if (len is >= 1 and <= 64) defaultLength = len;
            }
            else if (c == 'T')
            {
                int t = ReadValue(commands, ref pos, env);
                if (t is >= 32 and <= 255) tempo = t;
            }
            else if (c == 'M')
            {
                if (pos < commands.Length)
                {
                    char style = char.ToUpperInvariant(commands[pos++]);
                    if (style == 'N') noteFraction = 7.0 / 8.0;
                    else if (style == 'L') noteFraction = 1.0;
                    else if (style == 'S') noteFraction = 3.0 / 4.0;
                    else if (style == 'B') isBackground = true;
                    else if (style == 'F') isBackground = false;
                }
            }
            else if (c == 'P')
            {
                int len = ReadValue(commands, ref pos, env);
                if (len < 1 || len > 64) len = defaultLength;
                int dur = CalculateDuration(len, tempo, commands, ref pos);
                notes.Add(new MusicNote(0, 0, dur));
            }
            else if (c == 'N')
            {
                int noteNum = ReadValue(commands, ref pos, env);
                if (noteNum == 0)
                {
                    int dur = CalculateDuration(defaultLength, tempo, commands, ref pos);
                    notes.Add(new MusicNote(0, 0, dur));
                }
                else if (noteNum is >= 1 and <= 84)
                {
                    int semitone = noteNum - 1; // 0 is C0
                    int freq = (int)Math.Round(16.3516 * Math.Pow(2.0, semitone / 12.0));
                    int totalDur = CalculateDuration(defaultLength, tempo, commands, ref pos);
                    int soundDur = (int)Math.Round(totalDur * noteFraction);
                    int restDur = totalDur - soundDur;
                    notes.Add(new MusicNote(freq, soundDur, restDur));
                }
            }
            else if (c == 'X')
            {
                int startVar = pos;
                while (pos < commands.Length && commands[pos] != ';') pos++;
                string varName = commands[startVar..pos].Trim();
                if (pos < commands.Length && commands[pos] == ';') pos++;

                if (env != null && !string.IsNullOrEmpty(varName))
                {
                    string subTune = env.GetVariable(varName).AsString;
                    var subResult = ParseWithMode(subTune, env, octave, defaultLength, tempo, noteFraction, isBackground);
                    notes.AddRange(subResult.Notes);
                    isBackground = subResult.IsBackground;
                }
            }
            else if (c is >= 'A' and <= 'G')
            {
                string noteName = c.ToString();
                if (pos < commands.Length && commands[pos] is '#' or '+' or '-')
                {
                    noteName += commands[pos++];
                }

                int noteLen = ReadValue(commands, ref pos, env);
                if (noteLen < 1 || noteLen > 64) noteLen = defaultLength;

                int totalDur = CalculateDuration(noteLen, tempo, commands, ref pos);
                int soundDur = (int)Math.Round(totalDur * noteFraction);
                int restDur = totalDur - soundDur;

                if (SemitonesFromC.TryGetValue(noteName, out int semi))
                {
                    // C0 is ~16.35 Hz
                    int totalSemitones = octave * 12 + semi;
                    int freq = (int)Math.Round(16.3516 * Math.Pow(2.0, totalSemitones / 12.0));
                    notes.Add(new MusicNote(freq, soundDur, restDur));
                }
            }
        }

        return new MusicPlayResult(notes, isBackground);
    }

    private static int ReadValue(string str, ref int pos, BasicEnvironment? env)
    {
        while (pos < str.Length && char.IsWhiteSpace(str[pos])) pos++;
        if (pos < str.Length && str[pos] == '=')
        {
            pos++;
            int vStart = pos;
            while (pos < str.Length && str[pos] != ';' && !char.IsWhiteSpace(str[pos])) pos++;
            string varName = str[vStart..pos].Trim();
            if (pos < str.Length && str[pos] == ';') pos++;
            return env?.GetVariable(varName).AsInteger ?? 0;
        }

        int val = ReadInt(str, ref pos);
        if (pos < str.Length && str[pos] == ';') pos++;
        return val;
    }

    private static int ReadInt(string str, ref int pos)
    {
        int start = pos;
        while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
        if (pos > start && int.TryParse(str[start..pos], out int val)) return val;
        return 0;
    }

    private static int CalculateDuration(int noteLength, int tempo, string commands, ref int pos)
    {
        // Whole note duration in ms = (60000 / tempo) * 4
        double wholeNoteMs = (240000.0 / tempo);
        double dur = wholeNoteMs / noteLength;

        // Count dots
        double currentFraction = dur;
        while (pos < commands.Length && commands[pos] == '.')
        {
            pos++;
            currentFraction /= 2.0;
            dur += currentFraction;
        }

        return (int)Math.Round(dur);
    }
}
