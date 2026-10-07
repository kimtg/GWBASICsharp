using System.Globalization;
using System.Text;
using GWBASIC.Core.Runtime;

namespace GWBASIC.Core.IO;

/// <summary>
/// Authentic encoder and decoder for Microsoft GW-BASIC 3.23 tokenized binary programs (.BAS),
/// including support for protected (encrypted) files (0xFE magic byte) and MBF binary floats.
/// </summary>
public static class TokenizedBasicCodec
{
    private static readonly byte[] ProtectedXorKey =
    {
        0xA5, 0x5A, 0xF3, 0x3F, 0xC6, 0x6C, 0x99, 0x99, 0x55, 0xAA, 0x00
    };

    // Standard 1-byte keyword tokens (0x81 - 0xF4)
    private static readonly Dictionary<byte, string> SingleTokens = new()
    {
        { 0x81, "END" },
        { 0x82, "FOR" },
        { 0x83, "NEXT" },
        { 0x84, "DATA" },
        { 0x85, "INPUT" },
        { 0x86, "DIM" },
        { 0x87, "READ" },
        { 0x88, "LET" },
        { 0x89, "GOTO" },
        { 0x8A, "RUN" },
        { 0x8B, "IF" },
        { 0x8C, "RESTORE" },
        { 0x8D, "GOSUB" },
        { 0x8E, "RETURN" },
        { 0x8F, "REM" },
        { 0x90, "STOP" },
        { 0x91, "PRINT" },
        { 0x92, "CLEAR" },
        { 0x93, "LIST" },
        { 0x94, "NEW" },
        { 0x95, "ON" },
        { 0x96, "WAIT" },
        { 0x97, "DEF" },
        { 0x98, "POKE" },
        { 0x99, "LINES" },
        { 0x9A, "CONT" },
        { 0x9B, "OUT" },
        { 0x9C, "LPRINT" },
        { 0x9D, "LLIST" },
        { 0x9E, "CLS" },
        { 0x9F, "WIDTH" },
        { 0xA0, "ELSE" },
        { 0xA1, "TRON" },
        { 0xA2, "TROFF" },
        { 0xA3, "SWAP" },
        { 0xA4, "ERASE" },
        { 0xA5, "EDIT" },
        { 0xA6, "ERROR" },
        { 0xA7, "RESUME" },
        { 0xA8, "DELETE" },
        { 0xA9, "AUTO" },
        { 0xAA, "RENUM" },
        { 0xAB, "DEFSTR" },
        { 0xAC, "DEFINT" },
        { 0xAD, "DEFSNG" },
        { 0xAE, "DEFDBL" },
        { 0xAF, "LINE" },
        { 0xB0, "WHILE" },
        { 0xB1, "WEND" },
        { 0xB2, "CALL" },
        { 0xB7, "WRITE" },
        { 0xB8, "COMMON" },
        { 0xB9, "CHAIN" },
        { 0xBA, "OPTION" },
        { 0xBB, "RANDOMIZE" },
        { 0xBC, "OPEN" },
        { 0xBD, "CLOSE" },
        { 0xBE, "LOAD" },
        { 0xBF, "MERGE" },
        { 0xC0, "SAVE" },
        { 0xC1, "COLOR" },
        { 0xC2, "CLS" },
        { 0xC3, "MOTOR" },
        { 0xC4, "BSAVE" },
        { 0xC5, "BLOAD" },
        { 0xC6, "SOUND" },
        { 0xC7, "BEEP" },
        { 0xC8, "PSET" },
        { 0xC9, "PRESET" },
        { 0xCA, "SCREEN" },
        { 0xCB, "KEY" },
        { 0xCC, "LOCATE" },
        { 0xCD, "VIEW" },
        { 0xCE, "WINDOW" },
        { 0xCF, "PAINT" },
        { 0xD0, "COM" },
        { 0xD1, "CIRCLE" },
        { 0xD2, "DRAW" },
        { 0xD3, "PLAY" },
        { 0xD4, "TIMER" },
        { 0xD5, "ERDEV" },
        { 0xD6, "IOCTL" },
        { 0xD7, "CHDIR" },
        { 0xD8, "MKDIR" },
        { 0xD9, "RMDIR" },
        { 0xDA, "SHELL" },
        { 0xDB, "ENVIRON" },
        { 0xDC, "VIEW" },
        { 0xDD, "WINDOW" },
        { 0xDE, "POINT" },
        { 0xE6, "TO" },
        { 0xE7, "THEN" },
        { 0xE8, "TAB(" },
        { 0xE9, "STEP" },
        { 0xEA, "USR" },
        { 0xEB, "FN" },
        { 0xEC, "SPC(" },
        { 0xED, "NOT" },
        { 0xEE, "ERL" },
        { 0xEF, "ERR" },
        { 0xF0, "STRING$" },
        { 0xF1, "USING" },
        { 0xF2, "INSTR" },
        { 0xF3, "'" },
        { 0xF4, "VARPTR" },
        { 0xF5, "CSRLIN" },
        { 0xF6, "POINT" },
        { 0xF7, "OFF" },
        { 0xF8, "INKEY$" },
        { 0xF9, ">" },
        { 0xFA, "=" },
        { 0xFB, "<" },
        { 0xFC, "+" },
        { 0xFD, "-" },
        { 0xFE, "*" },
        { 0xFF, "/" }
    };

    // Extended 2-byte tokens prefixed with 0xFF
    private static readonly Dictionary<byte, string> FfTokens = new()
    {
        { 0x81, "LEFT$" },
        { 0x82, "RIGHT$" },
        { 0x83, "MID$" },
        { 0x84, "SGN" },
        { 0x85, "INT" },
        { 0x86, "ABS" },
        { 0x87, "SQR" },
        { 0x88, "RND" },
        { 0x89, "SIN" },
        { 0x8A, "LOG" },
        { 0x8B, "EXP" },
        { 0x8C, "COS" },
        { 0x8D, "TAN" },
        { 0x8E, "ATN" },
        { 0x8F, "FRE" },
        { 0x90, "INP" },
        { 0x91, "POS" },
        { 0x92, "LEN" },
        { 0x93, "STR$" },
        { 0x94, "VAL" },
        { 0x95, "ASC" },
        { 0x96, "CHR$" },
        { 0x97, "PEEK" },
        { 0x98, "SPACE$" },
        { 0x99, "OCT$" },
        { 0x9A, "HEX$" },
        { 0x9B, "LPOS" },
        { 0x9C, "CINT" },
        { 0x9D, "CSNG" },
        { 0x9E, "CDBL" },
        { 0x9F, "FIX" },
        { 0xA1, "CVI" },
        { 0xA2, "CVS" },
        { 0xA3, "CVD" },
        { 0xA4, "EOF" },
        { 0xA5, "LOC" },
        { 0xA6, "LOF" },
        { 0xA7, "MKI$" },
        { 0xA8, "MKS$" },
        { 0xA9, "MKD$" }
    };

    // Extended 2-byte tokens prefixed with 0xFD
    private static readonly Dictionary<byte, string> FdTokens = new()
    {
        { 0x81, "CVI" },
        { 0x82, "CVS" },
        { 0x83, "CVD" },
        { 0x84, "MKI$" },
        { 0x85, "MKS$" },
        { 0x86, "MKD$" },
        { 0x8B, "EXTERR" }
    };

    // Extended 2-byte tokens prefixed with 0xFE
    private static readonly Dictionary<byte, string> FeTokens = new()
    {
        { 0x81, "FILES" },
        { 0x82, "FIELD" },
        { 0x83, "SYSTEM" },
        { 0x84, "NAME" },
        { 0x85, "LSET" },
        { 0x86, "RSET" },
        { 0x87, "KILL" },
        { 0x88, "PUT" },
        { 0x89, "GET" },
        { 0x8A, "RESET" },
        { 0x8B, "COMMON" },
        { 0x8C, "CHAIN" },
        { 0x8D, "DATE$" },
        { 0x8E, "TIME$" },
        { 0x8F, "PAINT" }
    };

    // Reverse keyword lookup for encoder
    private static readonly Dictionary<string, byte> KeywordToSingle = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, byte> KeywordToFf = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, byte> KeywordToFe = new(StringComparer.OrdinalIgnoreCase);

    static TokenizedBasicCodec()
    {
        foreach (var (k, v) in SingleTokens)
        {
            if (v.Length > 1 && char.IsLetter(v[0]) && !KeywordToSingle.ContainsKey(v))
                KeywordToSingle[v] = k;
        }
        foreach (var (k, v) in FfTokens)
        {
            if (!KeywordToFf.ContainsKey(v))
                KeywordToFf[v] = k;
        }
        foreach (var (k, v) in FeTokens)
        {
            if (!KeywordToFe.ContainsKey(v))
                KeywordToFe[v] = k;
        }
    }

    public static bool IsTokenized(byte[] data) => data.Length > 0 && data[0] == 0xFF;
    public static bool IsProtected(byte[] data) => data.Length > 0 && data[0] == 0xFE;

    public static byte[] DescrambleProtected(byte[] data)
    {
        if (data.Length == 0) return data;
        byte[] decrypted = new byte[data.Length];
        decrypted[0] = 0xFF; // Set tokenized magic byte

        for (int i = 1; i < data.Length; i++)
        {
            byte keyByte = ProtectedXorKey[(i - 1) % ProtectedXorKey.Length];
            decrypted[i] = (byte)(data[i] ^ keyByte);
        }

        return decrypted;
    }

    public static string Decode(byte[] rawData)
    {
        if (rawData == null || rawData.Length == 0) return "";

        byte[] data = rawData;
        if (IsProtected(data))
        {
            data = DescrambleProtected(data);
        }

        if (!IsTokenized(data))
        {
            return Encoding.Latin1.GetString(data);
        }

        var sb = new StringBuilder();
        int pos = 1; // Skip 0xFF magic byte

        while (pos + 4 <= data.Length)
        {
            ushort nextLink = BitConverter.ToUInt16(data, pos);
            pos += 2;
            if (nextLink == 0) break; // End of program link

            ushort lineNum = BitConverter.ToUInt16(data, pos);
            pos += 2;

            sb.Append(lineNum);
            sb.Append(' ');

            bool inQuotes = false;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                if (b == 0x00) break; // End of line

                if (b == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append('"');
                    continue;
                }

                if (inQuotes)
                {
                    sb.Append((char)b);
                    continue;
                }

                // Colon + REM shorthand (')
                if (b == 0x3A && pos < data.Length && data[pos] == 0x8F)
                {
                    pos++;
                    sb.Append(':');
                    sb.Append("REM ");
                    continue;
                }

                // Constants
                if (b == 0x0B) // Octal constant
                {
                    if (pos + 2 <= data.Length)
                    {
                        ushort val = BitConverter.ToUInt16(data, pos);
                        pos += 2;
                        sb.Append("&O").Append(Convert.ToString(val, 8));
                    }
                    continue;
                }
                if (b == 0x0C) // Hex constant
                {
                    if (pos + 2 <= data.Length)
                    {
                        ushort val = BitConverter.ToUInt16(data, pos);
                        pos += 2;
                        sb.Append("&H").Append(val.ToString("X"));
                    }
                    continue;
                }
                if (b == 0x0D) // Line pointer constant
                {
                    if (pos + 2 <= data.Length)
                    {
                        ushort targetLine = BitConverter.ToUInt16(data, pos);
                        pos += 2;
                        sb.Append(targetLine);
                    }
                    continue;
                }
                if (b is 0x0E or 0x1C) // 2-byte decimal integer
                {
                    if (pos + 2 <= data.Length)
                    {
                        short val = BitConverter.ToInt16(data, pos);
                        pos += 2;
                        sb.Append(val);
                    }
                    continue;
                }
                if (b == 0x0F) // 1-byte decimal integer
                {
                    if (pos < data.Length)
                    {
                        byte val = data[pos++];
                        sb.Append(val);
                    }
                    continue;
                }
                if (b is >= 0x11 and <= 0x1B) // Digits 0 to 10
                {
                    sb.Append(b - 0x11);
                    continue;
                }
                if (b == 0x1D) // 4-byte single float (MBF)
                {
                    if (pos + 4 <= data.Length)
                    {
                        float val = MbfToSingle(data, pos);
                        pos += 4;
                        sb.Append(val.ToString("G7", CultureInfo.InvariantCulture));
                    }
                    continue;
                }
                if (b == 0x1F) // 8-byte double float (MBF)
                {
                    if (pos + 8 <= data.Length)
                    {
                        double val = MbfToDouble(data, pos);
                        pos += 8;
                        sb.Append(val.ToString("G15", CultureInfo.InvariantCulture));
                    }
                    continue;
                }

                // Extended tokens
                if (b == 0xFF && pos < data.Length)
                {
                    byte ext = data[pos++];
                    if (FfTokens.TryGetValue(ext, out string? extName))
                    {
                        sb.Append(extName);
                    }
                    continue;
                }
                if (b == 0xFD && pos < data.Length)
                {
                    byte ext = data[pos++];
                    if (FdTokens.TryGetValue(ext, out string? extName))
                    {
                        sb.Append(extName);
                    }
                    continue;
                }
                if (b == 0xFE && pos < data.Length)
                {
                    byte ext = data[pos++];
                    if (FeTokens.TryGetValue(ext, out string? extName))
                    {
                        sb.Append(extName);
                    }
                    continue;
                }

                // Single byte tokens
                if (SingleTokens.TryGetValue(b, out string? name))
                {
                    sb.Append(name);
                    if (char.IsLetter(name[^1]))
                    {
                        sb.Append(' ');
                    }
                    continue;
                }

                // Regular ASCII character
                sb.Append((char)b);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static byte[] Encode(IEnumerable<ProgramLine> lines)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0xFF); // Tokenized magic byte

        ushort currentOffset = 0x0100; // Simulated memory address

        foreach (var line in lines)
        {
            int lineStartPos = (int)ms.Position;
            // Write 2-byte placeholder for link
            ms.Write(new byte[2], 0, 2);

            // Write 2-byte line number
            ms.Write(BitConverter.GetBytes((ushort)line.LineNumber), 0, 2);

            // Tokenize text into bytes
            string text = line.Text;
            int i = 0;
            bool inQuotes = false;

            while (i < text.Length)
            {
                char c = text[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    ms.WriteByte((byte)'"');
                    i++;
                    continue;
                }

                if (inQuotes)
                {
                    ms.WriteByte((byte)c);
                    i++;
                    continue;
                }

                // Try to match longest keyword token
                bool matched = false;
                if (char.IsLetter(c))
                {
                    // Check FF tokens
                    foreach (var (kw, tokenByte) in KeywordToFf)
                    {
                        if (i + kw.Length <= text.Length &&
                            text.AsSpan(i, kw.Length).Equals(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            ms.WriteByte(0xFF);
                            ms.WriteByte(tokenByte);
                            i += kw.Length;
                            matched = true;
                            break;
                        }
                    }

                    if (!matched)
                    {
                        // Check FE tokens
                        foreach (var (kw, tokenByte) in KeywordToFe)
                        {
                            if (i + kw.Length <= text.Length &&
                                text.AsSpan(i, kw.Length).Equals(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                ms.WriteByte(0xFE);
                                ms.WriteByte(tokenByte);
                                i += kw.Length;
                                matched = true;
                                break;
                            }
                        }
                    }

                    if (!matched)
                    {
                        // Check standard single tokens
                        foreach (var (kw, tokenByte) in KeywordToSingle)
                        {
                            if (i + kw.Length <= text.Length &&
                                text.AsSpan(i, kw.Length).Equals(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                ms.WriteByte(tokenByte);
                                i += kw.Length;
                                matched = true;
                                break;
                            }
                        }
                    }
                }

                if (!matched)
                {
                    ms.WriteByte((byte)c);
                    i++;
                }
            }

            ms.WriteByte(0x00); // End of line

            int nextOffset = (int)ms.Position;
            int lineLength = nextOffset - lineStartPos;
            currentOffset += (ushort)lineLength;

            // Patch next line pointer
            ms.Seek(lineStartPos, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(currentOffset), 0, 2);
            ms.Seek(nextOffset, SeekOrigin.Begin);
        }

        // Program terminator: 2 zero bytes (link 0)
        ms.Write(new byte[2], 0, 2);
        return ms.ToArray();
    }

    private static float MbfToSingle(byte[] bytes, int offset)
    {
        byte exp = bytes[offset + 3];
        if (exp == 0) return 0f;

        byte sign = (byte)(bytes[offset + 2] & 0x80);
        byte m2 = (byte)(bytes[offset + 2] & 0x7F);
        byte m1 = bytes[offset + 1];
        byte m0 = bytes[offset];

        int ieeeExp = exp - 2;
        if (ieeeExp is <= 0 or >= 255) return 0f;

        uint bits = ((uint)sign << 24) | ((uint)ieeeExp << 23) | ((uint)m2 << 16) | ((uint)m1 << 8) | m0;
        return BitConverter.UInt32BitsToSingle(bits);
    }

    private static double MbfToDouble(byte[] bytes, int offset)
    {
        byte exp = bytes[offset + 7];
        if (exp == 0) return 0.0;

        byte sign = (byte)(bytes[offset + 6] & 0x80);
        byte m6 = (byte)(bytes[offset + 6] & 0x7F);

        int ieeeExp = exp - 2 + (1023 - 127);
        if (ieeeExp is <= 0 or >= 2047) return 0.0;

        ulong mantissa = ((ulong)m6 << 48) |
                         ((ulong)bytes[offset + 5] << 40) |
                         ((ulong)bytes[offset + 4] << 32) |
                         ((ulong)bytes[offset + 3] << 24) |
                         ((ulong)bytes[offset + 2] << 16) |
                         ((ulong)bytes[offset + 1] << 8) |
                         bytes[offset];

        ulong bits = ((ulong)sign << 56) | ((ulong)ieeeExp << 52) | (mantissa & 0x000FFFFFFFFFFFFFUL);
        return BitConverter.UInt64BitsToDouble(bits);
    }
}
