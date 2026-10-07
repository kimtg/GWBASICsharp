namespace GWBASIC.Core.Runtime;

public static class DrawEngine
{
    public static void Execute(string commands, BasicEnvironment env)
    {
        Execute(commands, env, env.Screen.ForegroundColor, 1.0, 0, 0);
    }

    private static (int Color, double Scale, int Angle, int TurnAngle) Execute(
        string commands,
        BasicEnvironment env,
        int initialColor,
        double initialScale,
        int initialAngle,
        int initialTurnAngle)
    {
        int x = env.LastGraphicX;
        int y = env.LastGraphicY;
        int color = initialColor;
        double scale = initialScale; // S4 is default scale = 1.0 (scale = n / 4)
        int angle = initialAngle; // 0=0, 1=90, 2=180, 3=270
        int turnAngle = initialTurnAngle; // TA angle in degrees

        int pos = 0;
        while (pos < commands.Length)
        {
            char c = char.ToUpperInvariant(commands[pos++]);
            if (char.IsWhiteSpace(c) || c == ';') continue;

            bool blind = false;
            bool noUpdate = false;

            if (c == 'B')
            {
                blind = true;
                if (pos < commands.Length) c = char.ToUpperInvariant(commands[pos++]);
            }
            else if (c == 'N')
            {
                noUpdate = true;
                if (pos < commands.Length) c = char.ToUpperInvariant(commands[pos++]);
            }

            int prevX = x;
            int prevY = y;

            if (c == 'C')
            {
                color = ReadValue(commands, ref pos, env);
                continue;
            }
            if (c == 'S')
            {
                int sVal = ReadValue(commands, ref pos, env);
                scale = sVal > 0 ? sVal / 4.0 : 1.0;
                continue;
            }
            if (c == 'A')
            {
                angle = ReadValue(commands, ref pos, env) % 4;
                continue;
            }
            if (c == 'T')
            {
                if (pos < commands.Length && char.ToUpperInvariant(commands[pos]) == 'A')
                {
                    pos++; // skip 'A'
                    turnAngle = ReadSignedValue(commands, ref pos, env);
                }
                continue;
            }
            if (c == 'P')
            {
                int pCol = ReadValue(commands, ref pos, env);
                int bCol = pCol;
                while (pos < commands.Length && char.IsWhiteSpace(commands[pos])) pos++;
                if (pos < commands.Length && commands[pos] == ',')
                {
                    pos++;
                    bCol = ReadValue(commands, ref pos, env);
                }
                if (pos < commands.Length && commands[pos] == ';') pos++;
                env.Screen.Paint(x, y, pCol, bCol);
                continue;
            }
            if (c == 'X')
            {
                int startVar = pos;
                while (pos < commands.Length && commands[pos] != ';') pos++;
                string varName = commands[startVar..pos].Trim();
                if (pos < commands.Length && commands[pos] == ';') pos++;

                if (!string.IsNullOrEmpty(varName))
                {
                    env.LastGraphicX = x;
                    env.LastGraphicY = y;
                    string subCmd = env.GetVariable(varName).AsString;
                    var state = Execute(subCmd, env, color, scale, angle, turnAngle);
                    color = state.Color;
                    scale = state.Scale;
                    angle = state.Angle;
                    turnAngle = state.TurnAngle;
                    x = env.LastGraphicX;
                    y = env.LastGraphicY;
                }
                continue;
            }

            if (c is 'U' or 'D' or 'L' or 'R' or 'E' or 'F' or 'G' or 'H')
            {
                int distance = ReadValue(commands, ref pos, env);
                if (distance == 0) distance = 1;
                double dist = distance * scale;

                int dx = 0, dy = 0;
                switch (c)
                {
                    case 'U': dy = -(int)Math.Round(dist); break;
                    case 'D': dy = (int)Math.Round(dist); break;
                    case 'L': dx = -(int)Math.Round(dist); break;
                    case 'R': dx = (int)Math.Round(dist); break;
                    case 'E': dx = (int)Math.Round(dist); dy = -(int)Math.Round(dist); break;
                    case 'F': dx = (int)Math.Round(dist); dy = (int)Math.Round(dist); break;
                    case 'G': dx = -(int)Math.Round(dist); dy = (int)Math.Round(dist); break;
                    case 'H': dx = -(int)Math.Round(dist); dy = -(int)Math.Round(dist); break;
                }

                // Apply rotation (A + TA)
                (dx, dy) = Rotate(dx, dy, angle, turnAngle);

                int targetX = x + dx;
                int targetY = y + dy;

                if (!blind)
                {
                    env.Screen.Line(x, y, targetX, targetY, color);
                }

                if (!noUpdate)
                {
                    x = targetX;
                    y = targetY;
                }
                else
                {
                    x = prevX;
                    y = prevY;
                }
                continue;
            }

            if (c == 'M')
            {
                bool relX = false, relY = false;
                while (pos < commands.Length && char.IsWhiteSpace(commands[pos])) pos++;
                if (pos < commands.Length && (commands[pos] == '+' || commands[pos] == '-')) relX = true;
                int mx = ReadSignedValue(commands, ref pos, env);
                while (pos < commands.Length && (commands[pos] == ',' || char.IsWhiteSpace(commands[pos]))) pos++;
                if (pos < commands.Length && (commands[pos] == '+' || commands[pos] == '-')) relY = true;
                int my = ReadSignedValue(commands, ref pos, env);

                int targetX = relX ? x + mx : mx;
                int targetY = relY ? y + my : my;

                if (!blind)
                {
                    env.Screen.Line(x, y, targetX, targetY, color);
                }

                if (!noUpdate)
                {
                    x = targetX;
                    y = targetY;
                }
                else
                {
                    x = prevX;
                    y = prevY;
                }
            }
        }

        env.LastGraphicX = x;
        env.LastGraphicY = y;
        return (color, scale, angle, turnAngle);
    }

    private static (int, int) Rotate(int dx, int dy, int angle, int turnAngle)
    {
        int totalAngle = angle * 90 + turnAngle;
        if (totalAngle % 360 == 0) return (dx, dy);

        double rad = totalAngle * (Math.PI / 180.0);
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        // Counterclockwise in screen coordinates (+Y downwards):
        double rx = dx * cos + dy * sin;
        double ry = -dx * sin + dy * cos;
        return ((int)Math.Round(rx), (int)Math.Round(ry));
    }

    private static int ReadValue(string str, ref int pos, BasicEnvironment env)
    {
        while (pos < str.Length && char.IsWhiteSpace(str[pos])) pos++;
        if (pos < str.Length && str[pos] == '=')
        {
            pos++;
            int vStart = pos;
            while (pos < str.Length && str[pos] != ';' && !char.IsWhiteSpace(str[pos]) && str[pos] != ',') pos++;
            string varName = str[vStart..pos].Trim();
            if (pos < str.Length && str[pos] == ';') pos++;
            return env.GetVariable(varName).AsInteger;
        }

        int val = ReadInt(str, ref pos);
        if (pos < str.Length && str[pos] == ';') pos++;
        return val;
    }

    private static int ReadSignedValue(string str, ref int pos, BasicEnvironment env)
    {
        while (pos < str.Length && char.IsWhiteSpace(str[pos])) pos++;
        if (pos < str.Length && str[pos] == '=')
        {
            pos++;
            int vStart = pos;
            while (pos < str.Length && str[pos] != ';' && !char.IsWhiteSpace(str[pos]) && str[pos] != ',') pos++;
            string varName = str[vStart..pos].Trim();
            if (pos < str.Length && str[pos] == ';') pos++;
            return env.GetVariable(varName).AsInteger;
        }

        int val = ReadSignedInt(str, ref pos);
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

    private static int ReadSignedInt(string str, ref int pos)
    {
        int start = pos;
        if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
        while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
        if (pos > start && int.TryParse(str[start..pos], out int val)) return val;
        return 0;
    }
}
