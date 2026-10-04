namespace GWBASIC.Core.Runtime;

public static class DrawEngine
{
    public static void Execute(string commands, BasicEnvironment env)
    {
        int x = env.LastGraphicX;
        int y = env.LastGraphicY;
        int color = env.Screen.ForegroundColor;
        double scale = 1.0; // S4 is default scale = 1.0 (scale = n / 4)
        int angle = 0; // 0=0, 1=90, 2=180, 3=270

        int pos = 0;
        while (pos < commands.Length)
        {
            char c = char.ToUpperInvariant(commands[pos++]);
            if (char.IsWhiteSpace(c)) continue;

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
                color = ReadInt(commands, ref pos);
                continue;
            }
            if (c == 'S')
            {
                int sVal = ReadInt(commands, ref pos);
                scale = sVal > 0 ? sVal / 4.0 : 1.0;
                continue;
            }
            if (c == 'A')
            {
                angle = ReadInt(commands, ref pos) % 4;
                continue;
            }

            if (c is 'U' or 'D' or 'L' or 'R' or 'E' or 'F' or 'G' or 'H')
            {
                int distance = ReadInt(commands, ref pos);
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

                // Apply rotation
                (dx, dy) = Rotate(dx, dy, angle);

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
                if (pos < commands.Length && (commands[pos] == '+' || commands[pos] == '-')) relX = true;
                int mx = ReadSignedInt(commands, ref pos);
                while (pos < commands.Length && (commands[pos] == ',' || char.IsWhiteSpace(commands[pos]))) pos++;
                if (pos < commands.Length && (commands[pos] == '+' || commands[pos] == '-')) relY = true;
                int my = ReadSignedInt(commands, ref pos);

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
    }

    private static (int, int) Rotate(int dx, int dy, int angle) => angle switch
    {
        1 => (-dy, dx),  // 90 deg
        2 => (-dx, -dy), // 180 deg
        3 => (dy, -dx),  // 270 deg
        _ => (dx, dy)
    };

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
