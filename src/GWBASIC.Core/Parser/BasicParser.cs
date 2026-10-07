using System.Globalization;
using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Lexer;
using GWBASIC.Core.Parser.Expressions;
using GWBASIC.Core.Parser.Statements;
using GWBASIC.Core.Runtime;

namespace GWBASIC.Core.Parser;

public class BasicParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public BasicParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public static (int? LineNumber, List<Statement> Statements) ParseLine(string lineText)
    {
        var lexer = new BasicLexer(lineText);
        var tokens = lexer.Tokenize();
        var parser = new BasicParser(tokens);
        return parser.ParseProgramLine();
    }

    public (int? LineNumber, List<Statement> Statements) ParseProgramLine()
    {
        int? lineNumber = null;
        if (MatchLineNumber(out int lNum))
        {
            if (lNum is < 0 or > 65529)
                throw new BasicException(BasicErrorCode.UndefinedLineNumber, lNum, "Line number out of range (0-65529)");
            lineNumber = lNum;
        }

        var statements = new List<Statement>();
        while (!IsAtEnd() && !Check(TokenType.EndOfLine))
        {
            // Skip empty statements or multiple colons
            if (Match(TokenType.Colon))
                continue;

            var stmt = ParseStatement();
            if (stmt != null)
            {
                stmt.LineNumber = lineNumber;
                statements.Add(stmt);
            }

            if (!Match(TokenType.Colon))
            {
                break;
            }
        }

        return (lineNumber, statements);
    }

    public Statement ParseStatement()
    {
        if (Match(TokenType.Rem))
        {
            return new RemStatement(Previous().Value?.AsString ?? Previous().Text);
        }

        if (Match(TokenType.Print))
            return ParsePrint();

        if (Match(TokenType.Write))
            return ParseWrite();

        if (Match(TokenType.Input))
            return ParseInput();

        if (Match(TokenType.Line))
        {
            if (Match(TokenType.Input))
                return ParseLineInput();
            return ParseLineGraphics();
        }

        if (Match(TokenType.If))
            return ParseIf();

        if (Match(TokenType.Goto))
            return new GotoStatement(ParseExpression());

        if (Match(TokenType.Gosub))
            return new GosubStatement(ParseExpression());

        if (Match(TokenType.Return))
        {
            Expression? target = !IsStatementTerminator() ? ParseExpression() : null;
            return new ReturnStatement(target);
        }

        if (Match(TokenType.For))
            return ParseFor();

        if (Match(TokenType.Next))
            return ParseNext();

        if (Match(TokenType.While))
            return new WhileStatement(ParseExpression());

        if (Match(TokenType.Wend))
            return new WendStatement();

        if (Match(TokenType.On))
            return ParseOn();

        if (Match(TokenType.Data))
            return ParseData();

        if (Match(TokenType.Read))
            return ParseRead();

        if (Match(TokenType.Restore))
        {
            Expression? target = !IsStatementTerminator() ? ParseExpression() : null;
            return new RestoreStatement(target);
        }

        if (Match(TokenType.Dim))
            return ParseDim();

        if (Match(TokenType.Option))
        {
            Consume(TokenType.Base, "Expected BASE after OPTION");
            int baseVal = (int)Consume(TokenType.IntegerLiteral, "Expected 0 or 1 for OPTION BASE").Value!.Value.AsInteger;
            if (baseVal is not (0 or 1)) throw new BasicException(BasicErrorCode.SyntaxError);
            return new OptionBaseStatement(baseVal);
        }

        if (Match(TokenType.DefInt)) return ParseDefType(BasicType.Integer);
        if (Match(TokenType.DefSng)) return ParseDefType(BasicType.Single);
        if (Match(TokenType.DefDbl)) return ParseDefType(BasicType.Double);
        if (Match(TokenType.DefStr)) return ParseDefType(BasicType.String);

        if (Match(TokenType.Def))
        {
            if (Match(TokenType.Seg))
            {
                Expression? seg = Match(TokenType.Equal) ? ParseExpression() : (!IsStatementTerminator() ? ParseExpression() : null);
                return new DefSegStatement(seg);
            }
            if (Match(TokenType.Fn))
            {
                return ParseDefFn();
            }
            // def fn where fn is part of identifier
            if (Check(TokenType.Identifier) && Peek().Text.StartsWith("FN", StringComparison.OrdinalIgnoreCase))
            {
                return ParseDefFn();
            }
            throw new BasicException(BasicErrorCode.SyntaxError);
        }

        if (Match(TokenType.Swap))
        {
            var t1 = ParseVariableOrArrayTarget("Expected first variable in SWAP");
            Consume(TokenType.Comma, "Expected ',' in SWAP");
            var t2 = ParseVariableOrArrayTarget("Expected second variable in SWAP");
            return new SwapStatement(t1, t2);
        }

        if (Match(TokenType.Erase))
        {
            var arrs = new List<string> { ConsumeIdentifier("Expected array name in ERASE") };
            while (Match(TokenType.Comma))
                arrs.Add(ConsumeIdentifier("Expected array name in ERASE"));
            return new EraseStatement(arrs);
        }

        if (Match(TokenType.Clear))
        {
            Expression? mem = !IsStatementTerminator() ? ParseExpression() : null;
            return new ClearStatement(mem);
        }

        if (Match(TokenType.Randomize))
        {
            Expression? seed = !IsStatementTerminator() ? ParseExpression() : null;
            return new RandomizeStatement(seed);
        }

        if (Match(TokenType.Cls))
        {
            Expression? mode = !IsStatementTerminator() ? ParseExpression() : null;
            return new ClsStatement(mode);
        }

        if (Match(TokenType.Locate))
            return ParseLocate();

        if (Match(TokenType.Color))
            return ParseColor();

        if (Match(TokenType.Screen))
            return ParseScreen();

        if (Match(TokenType.Width))
            return new WidthStatement(ParseExpression());

        if (Match(TokenType.Pset))
            return ParsePset(false);

        if (Match(TokenType.Preset))
            return ParsePset(true);

        if (Match(TokenType.Circle))
            return ParseCircle();

        if (Match(TokenType.Paint))
            return ParsePaint();

        if (Match(TokenType.Window))
            return ParseWindow();

        if (Match(TokenType.View))
            return ParseView();

        if (Match(TokenType.Draw))
            return new DrawStatement(ParseExpression());

        if (Match(TokenType.Beep))
            return new BeepStatement();

        if (Match(TokenType.Sound))
        {
            var freq = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in SOUND");
            var dur = ParseExpression();
            return new SoundStatement(freq, dur);
        }

        if (Match(TokenType.Play))
            return new PlayStatement(ParseExpression());

        if (Match(TokenType.Poke))
        {
            var addr = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in POKE");
            var val = ParseExpression();
            return new PokeStatement(addr, val);
        }

        if (Match(TokenType.Open))
            return ParseOpen();

        if (Match(TokenType.Close))
            return ParseClose();

        if (Match(TokenType.Field))
            return ParseField();

        if (Match(TokenType.Lset))
        {
            string varName = ConsumeIdentifier("Expected variable in LSET");
            Consume(TokenType.Equal, "Expected '=' in LSET");
            return new LsetStatement(varName, ParseExpression());
        }

        if (Match(TokenType.Rset))
        {
            string varName = ConsumeIdentifier("Expected variable in RSET");
            Consume(TokenType.Equal, "Expected '=' in RSET");
            return new RsetStatement(varName, ParseExpression());
        }

        if (Match(TokenType.Put))
        {
            if (Check(TokenType.OpenParen))
            {
                return ParseGraphicsPut();
            }
            Match(TokenType.Hash); // optional #
            var fNum = ParseExpression();
            Expression? rec = Match(TokenType.Comma) ? ParseExpression() : null;
            return new PutStatement(fNum, rec);
        }

        if (Match(TokenType.Get))
        {
            if (Check(TokenType.OpenParen))
            {
                return ParseGraphicsGet();
            }
            Match(TokenType.Hash); // optional #
            var fNum = ParseExpression();
            Expression? rec = Match(TokenType.Comma) ? ParseExpression() : null;
            return new GetStatement(fNum, rec);
        }

        if (Match(TokenType.Tron)) return new TronStatement();
        if (Match(TokenType.Troff)) return new TroffStatement();
        if (Match(TokenType.Stop)) return new StopStatement();
        if (Match(TokenType.End)) return new EndStatement();
        if (Match(TokenType.System)) return new SystemStatement();
        if (Match(TokenType.Cont)) return new ContStatement();
        if (Match(TokenType.New)) return new NewStatement();

        if (Match(TokenType.Run))
        {
            Expression? target = !IsStatementTerminator() ? ParseExpression() : null;
            return new RunStatement(target);
        }

        if (Match(TokenType.List))
            return ParseList(false);

        if (Match(TokenType.Llist))
            return ParseList(true);

        if (Match(TokenType.Auto))
            return ParseAuto();

        if (Match(TokenType.Renum))
            return ParseRenum();

        if (Match(TokenType.Delete))
            return ParseDelete();

        if (Match(TokenType.Bload))
        {
            var fileName = ParseExpression();
            Expression? offset = Match(TokenType.Comma) ? ParseExpression() : null;
            return new BloadStatement(fileName, offset);
        }

        if (Match(TokenType.Bsave))
        {
            var fileName = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in BSAVE");
            var offset = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in BSAVE");
            var length = ParseExpression();
            return new BsaveStatement(fileName, offset, length);
        }

        if (Match(TokenType.Load))
        {
            var fileName = ParseExpression();
            bool run = Match(TokenType.Comma) && Match(TokenType.Identifier) && Previous().Text.Equals("R", StringComparison.OrdinalIgnoreCase);
            return new LoadStatement(fileName, run);
        }

        if (Match(TokenType.Save))
        {
            var fileName = ParseExpression();
            bool ascii = Match(TokenType.Comma) && Match(TokenType.Identifier) && Previous().Text.Equals("A", StringComparison.OrdinalIgnoreCase);
            return new SaveStatement(fileName, ascii);
        }

        if (Match(TokenType.Merge))
            return new MergeStatement(ParseExpression());

        if (Match(TokenType.Files))
        {
            Expression? pat = !IsStatementTerminator() ? ParseExpression() : null;
            return new FilesStatement(pat);
        }

        if (Match(TokenType.Kill))
            return new KillStatement(ParseExpression());

        if (Match(TokenType.Name))
        {
            var oldN = ParseExpression();
            Consume(TokenType.As, "Expected AS in NAME");
            var newN = ParseExpression();
            return new NameStatement(oldN, newN);
        }

        if (Match(TokenType.Key))
            return ParseKey();

        if (Match(TokenType.Resume))
        {
            if (Match(TokenType.Next)) return new ResumeStatement(ResumeTarget.Next);
            if (!IsStatementTerminator())
            {
                var targetExpr = ParseExpression();
                return new ResumeStatement(ResumeTarget.Line, targetExpr);
            }
            return new ResumeStatement(ResumeTarget.Current);
        }

        if (Match(TokenType.Shell))
        {
            Expression? cmd = !IsStatementTerminator() ? ParseExpression() : null;
            return new ShellStatement(cmd);
        }

        if (Match(TokenType.Reset))
            return new ResetStatement();

        if (Match(TokenType.Common))
            return ParseCommon();

        if (Match(TokenType.Chain))
            return ParseChain();

        if (Match(TokenType.Environ))
            return new EnvironStatement(ParseExpression());

        if (Match(TokenType.Edit))
            return new EditStatement(ParseExpression());

        if (Match(TokenType.Out))
        {
            var port = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in OUT");
            var val = ParseExpression();
            return new OutStatement(port, val);
        }

        if (Match(TokenType.DateStr))
        {
            Consume(TokenType.Equal, "Expected '=' in DATE$ statement");
            return new DateStatement(ParseExpression());
        }

        if (Match(TokenType.TimeStr))
        {
            Consume(TokenType.Equal, "Expected '=' in TIME$ statement");
            return new TimeStatement(ParseExpression());
        }

        if (Match(TokenType.Let))
            return ParseAssignment();

        // Check for MID$ statement assignment: MID$(A$, 2, 3) = "xyz"
        if (Match(TokenType.MidStr))
            return ParseMidAssignment();

        // Default: Variable or Array Assignment
        if (Check(TokenType.Identifier))
            return ParseAssignment();

        throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, $"Unexpected token {Peek().Type} ('{Peek().Text}')");
    }

    #region Specific Statement Parsers

    private Statement ParsePrint()
    {
        Expression? fileNum = null;
        if (Match(TokenType.Hash))
        {
            fileNum = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' after file number");
        }

        Expression? usingFormat = null;
        if (Match(TokenType.Using))
        {
            usingFormat = ParseExpression();
            Consume(TokenType.Semicolon, "Expected ';' after PRINT USING format string");
        }

        var items = new List<PrintItem>();
        while (!IsStatementTerminator())
        {
            if (Match(TokenType.Comma))
            {
                items.Add(new PrintItem(PrintItemType.Comma));
            }
            else if (Match(TokenType.Semicolon))
            {
                items.Add(new PrintItem(PrintItemType.Semicolon));
            }
            else if (Match(TokenType.Tab))
            {
                Consume(TokenType.OpenParen, "Expected '(' after TAB");
                var expr = ParseExpression();
                Consume(TokenType.CloseParen, "Expected ')' after TAB expression");
                items.Add(new PrintItem(PrintItemType.Tab, expr));
            }
            else if (Match(TokenType.Spc))
            {
                Consume(TokenType.OpenParen, "Expected '(' after SPC");
                var expr = ParseExpression();
                Consume(TokenType.CloseParen, "Expected ')' after SPC expression");
                items.Add(new PrintItem(PrintItemType.Spc, expr));
            }
            else
            {
                var expr = ParseExpression();
                items.Add(new PrintItem(PrintItemType.Expression, expr));
            }
        }

        return new PrintStatement(fileNum, usingFormat, items);
    }

    private Statement ParseWrite()
    {
        Expression? fileNum = null;
        if (Match(TokenType.Hash))
        {
            fileNum = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' after file number");
        }

        var items = new List<Expression>();
        while (!IsStatementTerminator())
        {
            items.Add(ParseExpression());
            if (!Match(TokenType.Comma)) break;
        }

        return new WriteStatement(fileNum, items);
    }

    private Statement ParseInput()
    {
        Expression? fileNum = null;
        if (Match(TokenType.Hash))
        {
            fileNum = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' after file number");
            var varNames = new List<string> { ConsumeIdentifier("Expected variable name") };
            while (Match(TokenType.Comma)) varNames.Add(ConsumeIdentifier("Expected variable name"));
            return new InputStatement(fileNum, false, null, false, varNames);
        }

        bool suppressNewline = Match(TokenType.Semicolon);
        string? prompt = null;
        bool qMark = true;

        if (Match(TokenType.StringLiteral))
        {
            prompt = Previous().Value?.AsString ?? Previous().Text;
            if (Match(TokenType.Comma))
            {
                qMark = false;
            }
            else
            {
                Consume(TokenType.Semicolon, "Expected ';' or ',' after INPUT prompt");
            }
        }

        var names = new List<string> { ConsumeIdentifier("Expected variable name") };
        while (Match(TokenType.Comma))
        {
            names.Add(ConsumeIdentifier("Expected variable name"));
        }

        return new InputStatement(fileNum, suppressNewline, prompt, qMark, names);
    }

    private Statement ParseLineInput()
    {
        Expression? fileNum = null;
        if (Match(TokenType.Hash))
        {
            fileNum = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' after file number");
            string vName = ConsumeIdentifier("Expected string variable");
            return new LineInputStatement(fileNum, false, null, vName);
        }

        bool suppressNewline = Match(TokenType.Semicolon);
        string? prompt = null;
        if (Match(TokenType.StringLiteral))
        {
            prompt = Previous().Value?.AsString ?? Previous().Text;
            Consume(TokenType.Semicolon, "Expected ';' after LINE INPUT prompt");
        }

        string varName = ConsumeIdentifier("Expected string variable");
        return new LineInputStatement(fileNum, suppressNewline, prompt, varName);
    }

    private Statement ParseIf()
    {
        var condition = ParseExpression();
        bool hasThen = Match(TokenType.Then);
        bool hasGoto = !hasThen && Match(TokenType.Goto);

        if (!hasThen && !hasGoto)
            throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, "Expected THEN or GOTO in IF statement");

        var thenStatements = new List<Statement>();

        if (hasGoto || (hasThen && Check(TokenType.IntegerLiteral)))
        {
            // IF cond THEN line  or  IF cond GOTO line
            var lineToken = Consume(TokenType.IntegerLiteral, "Expected line number");
            int line = (int)lineToken.Value!.Value.AsInteger;
            thenStatements.Add(new GotoStatement(new LiteralExpression(BasicValue.FromInteger((short)line))));
        }
        else
        {
            // Parse statements until ELSE, EndOfLine, or EndOfFile
            while (!IsAtEnd() && !Check(TokenType.EndOfLine) && !Check(TokenType.Else))
            {
                if (Match(TokenType.Colon)) continue;
                thenStatements.Add(ParseStatement());
                if (!Check(TokenType.Else) && !Match(TokenType.Colon)) break;
            }
        }

        List<Statement>? elseStatements = null;
        if (Match(TokenType.Else))
        {
            elseStatements = new List<Statement>();
            if (Check(TokenType.IntegerLiteral))
            {
                var lineToken = Consume(TokenType.IntegerLiteral, "Expected line number after ELSE");
                int line = (int)lineToken.Value!.Value.AsInteger;
                elseStatements.Add(new GotoStatement(new LiteralExpression(BasicValue.FromInteger((short)line))));
            }
            else
            {
                while (!IsAtEnd() && !Check(TokenType.EndOfLine))
                {
                    if (Match(TokenType.Colon)) continue;
                    elseStatements.Add(ParseStatement());
                    if (!Match(TokenType.Colon)) break;
                }
            }
        }

        return new IfStatement(condition, thenStatements, elseStatements);
    }

    private Statement ParseFor()
    {
        string varName = ConsumeIdentifier("Expected loop variable");
        Consume(TokenType.Equal, "Expected '=' in FOR");
        var start = ParseExpression();
        Consume(TokenType.To, "Expected TO in FOR");
        var end = ParseExpression();
        Expression? step = Match(TokenType.Step) ? ParseExpression() : null;
        return new ForStatement(varName, start, end, step);
    }

    private Statement ParseNext()
    {
        var names = new List<string>();
        if (Check(TokenType.Identifier))
        {
            names.Add(ConsumeIdentifier("Expected variable in NEXT"));
            while (Match(TokenType.Comma))
            {
                names.Add(ConsumeIdentifier("Expected variable in NEXT"));
            }
        }
        return new NextStatement(names);
    }

    private Statement ParseOn()
    {
        if (Match(TokenType.Error))
        {
            Consume(TokenType.Goto, "Expected GOTO after ON ERROR");
            var lineTok = Consume(TokenType.IntegerLiteral, "Expected line number");
            int target = (int)lineTok.Value!.Value.AsInteger;
            return new OnErrorGotoStatement(target);
        }

        var selector = ParseExpression();
        bool isGosub = false;
        if (Match(TokenType.Gosub)) isGosub = true;
        else Consume(TokenType.Goto, "Expected GOTO or GOSUB after ON expression");

        var targets = new List<int>();
        do
        {
            var lineTok = Consume(TokenType.IntegerLiteral, "Expected line number");
            targets.Add((int)lineTok.Value!.Value.AsInteger);
        } while (Match(TokenType.Comma));

        return new OnGotoStatement(selector, targets, isGosub);
    }

    private Statement ParseData()
    {
        var items = new List<string>();
        while (!IsStatementTerminator())
        {
            if (Match(TokenType.StringLiteral))
            {
                items.Add(Previous().Value?.AsString ?? Previous().Text);
            }
            else
            {
                // Read raw text until comma, colon, newline
                int start = Peek().Position;
                while (!IsAtEnd() && !Check(TokenType.Comma) && !Check(TokenType.Colon) && !Check(TokenType.EndOfLine))
                {
                    Advance();
                }
                string raw = string.Join("", _tokens.Where(t => t.Position >= start && t.Position < Peek().Position).Select(t => t.Text));
                items.Add(raw.Trim());
            }
            if (!Match(TokenType.Comma)) break;
        }
        return new DataStatement(items);
    }

    private Statement ParseRead()
    {
        var targets = new List<(string Name, List<Expression>? Indices)>();
        do
        {
            string name = ConsumeIdentifier("Expected variable in READ");
            List<Expression>? indices = null;
            if (Match(TokenType.OpenParen))
            {
                indices = new List<Expression>();
                do
                {
                    indices.Add(ParseExpression());
                } while (Match(TokenType.Comma));
                Consume(TokenType.CloseParen, "Expected ')' in array index");
            }
            targets.Add((name, indices));
        } while (Match(TokenType.Comma));

        return new ReadStatement(targets);
    }

    private Statement ParseDim()
    {
        var dims = new List<(string Name, List<Expression> Dimensions)>();
        do
        {
            string name = ConsumeIdentifier("Expected array name in DIM");
            Consume(TokenType.OpenParen, "Expected '(' in DIM");
            var sizes = new List<Expression>();
            do
            {
                sizes.Add(ParseExpression());
            } while (Match(TokenType.Comma));
            Consume(TokenType.CloseParen, "Expected ')' in DIM");
            dims.Add((name, sizes));
        } while (Match(TokenType.Comma));

        return new DimStatement(dims);
    }

    private Statement ParseDefType(BasicType type)
    {
        var ranges = new List<(char Start, char End)>();
        do
        {
            string startStr = ConsumeIdentifier("Expected letter in DEF" + type);
            char start = startStr[0];
            char end = start;
            if (Match(TokenType.Minus))
            {
                string endStr = ConsumeIdentifier("Expected ending letter in range");
                end = endStr[0];
            }
            ranges.Add((start, end));
        } while (Match(TokenType.Comma));

        return new DefTypeStatement(type, ranges);
    }

    private Statement ParseDefFn()
    {
        string fnName = ConsumeIdentifier("Expected FN name");
        if (!fnName.StartsWith("FN", StringComparison.OrdinalIgnoreCase))
            fnName = "FN" + fnName;

        var parameters = new List<string>();
        if (Match(TokenType.OpenParen))
        {
            if (!Check(TokenType.CloseParen))
            {
                do
                {
                    parameters.Add(ConsumeIdentifier("Expected parameter name"));
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.CloseParen, "Expected ')' after parameters");
        }

        Consume(TokenType.Equal, "Expected '=' in DEF FN");
        var body = ParseExpression();
        return new DefFnStatement(fnName, parameters, body);
    }

    private Statement ParseLocate()
    {
        Expression? row = null;
        Expression? col = null;
        Expression? cursor = null;

        if (!Check(TokenType.Comma) && !IsStatementTerminator()) row = ParseExpression();
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) col = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (!IsStatementTerminator()) cursor = ParseExpression();
            }
        }
        return new LocateStatement(row, col, cursor);
    }

    private Statement ParseColor()
    {
        Expression? fg = null, bg = null, bdr = null;
        if (!Check(TokenType.Comma) && !IsStatementTerminator()) fg = ParseExpression();
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) bg = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (!IsStatementTerminator()) bdr = ParseExpression();
            }
        }
        return new ColorStatement(fg, bg, bdr);
    }

    private Statement ParseScreen()
    {
        var mode = ParseExpression();
        Expression? burst = null, apage = null, vpage = null;
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) burst = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (!Check(TokenType.Comma) && !IsStatementTerminator()) apage = ParseExpression();
                if (Match(TokenType.Comma))
                {
                    if (!IsStatementTerminator()) vpage = ParseExpression();
                }
            }
        }
        return new ScreenStatement(mode, burst, apage, vpage);
    }

    private Statement ParsePset(bool isPreset)
    {
        Consume(TokenType.OpenParen, "Expected '(' in PSET/PRESET");
        var x = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in PSET/PRESET");
        var y = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in PSET/PRESET");
        Expression? col = Match(TokenType.Comma) ? ParseExpression() : null;
        return new PsetStatement(x, y, col, isPreset);
    }

    private Statement ParseLineGraphics()
    {
        Expression? x1 = null, y1 = null;
        if (Match(TokenType.OpenParen))
        {
            x1 = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in LINE start coordinate");
            y1 = ParseExpression();
            Consume(TokenType.CloseParen, "Expected ')' after start coordinate");
        }

        Consume(TokenType.Minus, "Expected '-' between coordinates in LINE");
        Consume(TokenType.OpenParen, "Expected '(' for end coordinate in LINE");
        var x2 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in end coordinate in LINE");
        var y2 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' after end coordinate in LINE");

        Expression? color = null;
        bool box = false;
        bool boxFill = false;
        Expression? style = null;

        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) color = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (Check(TokenType.Identifier))
                {
                    string flag = ConsumeIdentifier("Expected B or BF").ToUpperInvariant();
                    if (flag == "BF") { box = true; boxFill = true; }
                    else if (flag == "B") { box = true; }
                }
                if (Match(TokenType.Comma))
                {
                    style = ParseExpression();
                }
            }
        }

        return new LineGraphicsStatement(x1, y1, x2, y2, color, box, boxFill, style);
    }

    private Statement ParseCircle()
    {
        Consume(TokenType.OpenParen, "Expected '(' in CIRCLE");
        var x = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in CIRCLE coordinate");
        var y = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in CIRCLE coordinate");
        Consume(TokenType.Comma, "Expected ',' after coordinate in CIRCLE");
        var radius = ParseExpression();

        Expression? color = null, start = null, end = null, aspect = null;
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) color = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (!Check(TokenType.Comma) && !IsStatementTerminator()) start = ParseExpression();
                if (Match(TokenType.Comma))
                {
                    if (!Check(TokenType.Comma) && !IsStatementTerminator()) end = ParseExpression();
                    if (Match(TokenType.Comma))
                    {
                        aspect = ParseExpression();
                    }
                }
            }
        }

        return new CircleStatement(x, y, radius, color, start, end, aspect);
    }

    private Statement ParsePaint()
    {
        Consume(TokenType.OpenParen, "Expected '(' in PAINT");
        var x = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in PAINT");
        var y = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in PAINT");

        Expression? paintCol = null, boundCol = null;
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator()) paintCol = ParseExpression();
            if (Match(TokenType.Comma))
            {
                boundCol = ParseExpression();
            }
        }
        return new PaintStatement(x, y, paintCol, boundCol);
    }

    private Statement ParseGraphicsGet()
    {
        Consume(TokenType.OpenParen, "Expected '(' in GET");
        var x1 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in GET");
        var y1 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in GET");
        Consume(TokenType.Minus, "Expected '-' between coordinates in GET");
        Consume(TokenType.OpenParen, "Expected '(' in GET");
        var x2 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in GET");
        var y2 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in GET");
        Consume(TokenType.Comma, "Expected ',' before array name in GET");
        string arrayName = ConsumeIdentifier("Expected array name in GET");
        if (Match(TokenType.OpenParen))
        {
            ParseExpression();
            Consume(TokenType.CloseParen, "Expected ')' after array index");
        }
        return new GraphicsGetStatement(x1, y1, x2, y2, arrayName);
    }

    private Statement ParseGraphicsPut()
    {
        Consume(TokenType.OpenParen, "Expected '(' in PUT");
        var x = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in PUT");
        var y = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in PUT");
        Consume(TokenType.Comma, "Expected ',' before array name in PUT");
        string arrayName = ConsumeIdentifier("Expected array name in PUT");
        if (Match(TokenType.OpenParen))
        {
            ParseExpression();
            Consume(TokenType.CloseParen, "Expected ')' after array index");
        }
        PutAction action = PutAction.Xor;
        if (Match(TokenType.Comma))
        {
            if (Match(TokenType.Pset)) action = PutAction.Pset;
            else if (Match(TokenType.Preset)) action = PutAction.Preset;
            else if (Match(TokenType.And)) action = PutAction.And;
            else if (Match(TokenType.Or)) action = PutAction.Or;
            else if (Match(TokenType.Xor)) action = PutAction.Xor;
            else if (Check(TokenType.Identifier))
            {
                string act = ConsumeIdentifier("Expected action");
                action = act switch
                {
                    "PSET" => PutAction.Pset,
                    "PRESET" => PutAction.Preset,
                    "AND" => PutAction.And,
                    "OR" => PutAction.Or,
                    "XOR" => PutAction.Xor,
                    _ => throw new BasicException(BasicErrorCode.SyntaxError)
                };
            }
        }
        return new GraphicsPutStatement(x, y, arrayName, action);
    }

    private Statement ParseWindow()
    {
        if (IsStatementTerminator())
        {
            return new WindowStatement(null, null, null, null, false);
        }
        bool screenCoords = Match(TokenType.Screen);
        Consume(TokenType.OpenParen, "Expected '(' in WINDOW");
        var x1 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in WINDOW");
        var y1 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in WINDOW");
        Consume(TokenType.Minus, "Expected '-' in WINDOW");
        Consume(TokenType.OpenParen, "Expected '(' in WINDOW");
        var x2 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in WINDOW");
        var y2 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in WINDOW");
        return new WindowStatement(x1, y1, x2, y2, screenCoords);
    }

    private Statement ParseView()
    {
        if (IsStatementTerminator())
        {
            return new ViewStatement(null, null, null, null, null, null, false);
        }
        bool screenCoords = Match(TokenType.Screen);
        Consume(TokenType.OpenParen, "Expected '(' in VIEW");
        var x1 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in VIEW");
        var y1 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in VIEW");
        Consume(TokenType.Minus, "Expected '-' in VIEW");
        Consume(TokenType.OpenParen, "Expected '(' in VIEW");
        var x2 = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in VIEW");
        var y2 = ParseExpression();
        Consume(TokenType.CloseParen, "Expected ')' in VIEW");

        Expression? fill = null;
        Expression? border = null;
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator())
                fill = ParseExpression();
            if (Match(TokenType.Comma))
            {
                if (!IsStatementTerminator())
                    border = ParseExpression();
            }
        }
        return new ViewStatement(x1, y1, x2, y2, fill, border, screenCoords);
    }

    private Statement ParseCommon()
    {
        var vars = new List<string> { ConsumeIdentifier("Expected variable in COMMON") };
        while (Match(TokenType.Comma))
        {
            vars.Add(ConsumeIdentifier("Expected variable in COMMON"));
        }
        return new CommonStatement(vars);
    }

    private Statement ParseChain()
    {
        bool merge = Match(TokenType.Merge);
        var fileName = ParseExpression();
        Expression? line = null;
        bool all = false;
        if (Match(TokenType.Comma))
        {
            if (!Check(TokenType.Comma) && !IsStatementTerminator())
            {
                if (Check(TokenType.Identifier) && Peek().Text.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    Advance();
                    all = true;
                }
                else
                {
                    line = ParseExpression();
                }
            }
            if (!all && Match(TokenType.Comma))
            {
                if (Check(TokenType.Identifier) && Peek().Text.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    Advance();
                    all = true;
                }
            }
        }
        return new ChainStatement(fileName, line, merge, all);
    }

    private Statement ParseOpen()
    {
        // Syntax 1: OPEN mode, #num, filename [, reclen]
        // Syntax 2: OPEN filename FOR mode AS #num [LEN = reclen]
        var first = ParseExpression();
        if (Match(TokenType.For) || Check(TokenType.As))
        {
            FileModeType mode = FileModeType.Random;
            if (Previous().Type == TokenType.For)
            {
                if (Match(TokenType.Input)) mode = FileModeType.Input;
                else if (Match(TokenType.Output)) mode = FileModeType.Output;
                else if (Match(TokenType.Append)) mode = FileModeType.Append;
                else if (Match(TokenType.Identifier) && Previous().Text.Equals("RANDOM", StringComparison.OrdinalIgnoreCase)) mode = FileModeType.Random;
                else throw new BasicException(BasicErrorCode.BadFileMode);
            }

            Consume(TokenType.As, "Expected AS in OPEN");
            Match(TokenType.Hash); // optional #
            var fNum = ParseExpression();
            Expression? len = null;
            if (Match(TokenType.Len) || (Match(TokenType.Identifier) && Previous().Text.Equals("LEN", StringComparison.OrdinalIgnoreCase)))
            {
                Consume(TokenType.Equal, "Expected '=' in LEN");
                len = ParseExpression();
            }
            return new OpenStatement(first, mode, fNum, len);
        }
        else
        {
            // Syntax 1: first is mode (string e.g. "O", "I", "R", "A")
            Consume(TokenType.Comma, "Expected ',' in OPEN");
            Match(TokenType.Hash); // optional #
            var fNum = ParseExpression();
            Consume(TokenType.Comma, "Expected ',' in OPEN");
            var fName = ParseExpression();
            Expression? len = Match(TokenType.Comma) ? ParseExpression() : null;

            FileModeType mode = FileModeType.Input;
            string modeStr = first.Evaluate(null!).AsString.ToUpperInvariant();
            if (modeStr.StartsWith("O")) mode = FileModeType.Output;
            else if (modeStr.StartsWith("I")) mode = FileModeType.Input;
            else if (modeStr.StartsWith("A")) mode = FileModeType.Append;
            else if (modeStr.StartsWith("R")) mode = FileModeType.Random;

            return new OpenStatement(fName, mode, fNum, len);
        }
    }

    private Statement ParseClose()
    {
        var fNums = new List<Expression>();
        if (!IsStatementTerminator())
        {
            do
            {
                Match(TokenType.Hash); // optional #
                fNums.Add(ParseExpression());
            } while (Match(TokenType.Comma));
        }
        return new CloseStatement(fNums.Count > 0 ? fNums : null);
    }

    private Statement ParseField()
    {
        Match(TokenType.Hash); // optional #
        var fNum = ParseExpression();
        Consume(TokenType.Comma, "Expected ',' in FIELD");

        var fields = new List<(Expression Width, string VariableName)>();
        do
        {
            var width = ParseExpression();
            Consume(TokenType.As, "Expected AS in FIELD");
            string name = ConsumeIdentifier("Expected string variable in FIELD");
            fields.Add((width, name));
        } while (Match(TokenType.Comma));

        return new FieldStatement(fNum, fields);
    }

    private Statement ParseAssignment()
    {
        string name = ConsumeIdentifier("Expected variable name");

        // Array assignment
        if (Match(TokenType.OpenParen))
        {
            var indices = new List<Expression>();
            do
            {
                indices.Add(ParseExpression());
            } while (Match(TokenType.Comma));
            Consume(TokenType.CloseParen, "Expected ')'");
            Consume(TokenType.Equal, "Expected '=' in assignment");
            var val = ParseExpression();
            return new ArraySetStatement(name, indices, val);
        }

        Consume(TokenType.Equal, "Expected '=' in assignment");
        var value = ParseExpression();
        return new LetStatement(name, value);
    }

    private Statement ParseMidAssignment()
    {
        Consume(TokenType.OpenParen, "Expected '(' in MID$ assignment");
        string varName = ConsumeIdentifier("Expected string variable in MID$");
        Consume(TokenType.Comma, "Expected ',' in MID$");
        var start = ParseExpression();
        Expression? len = null;
        if (Match(TokenType.Comma))
        {
            len = ParseExpression();
        }
        Consume(TokenType.CloseParen, "Expected ')' in MID$");
        Consume(TokenType.Equal, "Expected '=' in MID$ assignment");
        var rep = ParseExpression();
        return new MidSetStatement(varName, start, len, rep);
    }

    private Statement ParseList(bool isLlist)
    {
        int? start = null, end = null;
        if (Match(TokenType.IntegerLiteral))
        {
            start = (int)Previous().Value!.Value.AsInteger;
            if (Match(TokenType.Minus))
            {
                if (Match(TokenType.IntegerLiteral))
                    end = (int)Previous().Value!.Value.AsInteger;
                else
                    end = int.MaxValue;
            }
            else
            {
                end = start;
            }
        }
        else if (Match(TokenType.Minus))
        {
            start = 0;
            if (Match(TokenType.IntegerLiteral))
                end = (int)Previous().Value!.Value.AsInteger;
            else
                end = int.MaxValue;
        }

        return isLlist ? new LlistStatement(start, end) : new ListStatement(start, end);
    }

    private Statement ParseAuto()
    {
        int? startLine = null;
        int? increment = null;
        bool useCurrentLine = false;

        if (Match(TokenType.Dot))
        {
            useCurrentLine = true;
            if (Match(TokenType.Comma))
            {
                if (MatchLineNumber(out int inc))
                {
                    increment = inc;
                }
            }
        }
        else if (MatchLineNumber(out int start))
        {
            startLine = start;
            if (Match(TokenType.Comma))
            {
                if (MatchLineNumber(out int inc))
                {
                    increment = inc;
                }
            }
        }
        else if (Match(TokenType.Comma))
        {
            startLine = 0;
            if (MatchLineNumber(out int inc))
            {
                increment = inc;
            }
        }

        if (!IsStatementTerminator())
        {
            throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, "Syntax error in AUTO");
        }

        return new AutoStatement(startLine, increment, useCurrentLine);
    }

    private Statement ParseRenum()
    {
        int? newStart = null, oldStart = null, inc = null;
        if (Match(TokenType.IntegerLiteral))
        {
            newStart = (int)Previous().Value!.Value.AsInteger;
            if (Match(TokenType.Comma))
            {
                if (Match(TokenType.IntegerLiteral)) oldStart = (int)Previous().Value!.Value.AsInteger;
                if (Match(TokenType.Comma))
                {
                    if (Match(TokenType.IntegerLiteral)) inc = (int)Previous().Value!.Value.AsInteger;
                }
            }
        }
        return new RenumStatement(newStart, oldStart, inc);
    }

    private Statement ParseDelete()
    {
        int? start = null, end = null;
        if (Match(TokenType.IntegerLiteral))
        {
            start = (int)Previous().Value!.Value.AsInteger;
            if (Match(TokenType.Minus))
            {
                if (Match(TokenType.IntegerLiteral)) end = (int)Previous().Value!.Value.AsInteger;
                else end = int.MaxValue;
            }
            else end = start;
        }
        else if (Match(TokenType.Minus))
        {
            start = 0;
            if (Match(TokenType.IntegerLiteral)) end = (int)Previous().Value!.Value.AsInteger;
            else end = int.MaxValue;
        }
        return new DeleteStatement(start, end);
    }

    private Statement ParseKey()
    {
        if (Match(TokenType.On)) return new KeyStatement(KeyCommandType.On);
        if (Match(TokenType.Off)) return new KeyStatement(KeyCommandType.Off);
        if (Match(TokenType.List)) return new KeyStatement(KeyCommandType.List);

        if (Match(TokenType.IntegerLiteral))
        {
            int num = (int)Previous().Value!.Value.AsInteger;
            Consume(TokenType.Comma, "Expected ',' in KEY statement");
            var str = ParseExpression();
            return new KeyStatement(KeyCommandType.Set, num, str);
        }
        throw new BasicException(BasicErrorCode.SyntaxError);
    }

    #endregion

    #region Expression Parsing (Precedence Pratt / Climber)

    public Expression ParseExpression() => ParseImp();

    private Expression ParseImp()
    {
        var expr = ParseEqv();
        while (Match(TokenType.Imp))
        {
            var op = Previous().Type;
            var right = ParseEqv();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseEqv()
    {
        var expr = ParseXor();
        while (Match(TokenType.Eqv))
        {
            var op = Previous().Type;
            var right = ParseXor();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseXor()
    {
        var expr = ParseOr();
        while (Match(TokenType.Xor))
        {
            var op = Previous().Type;
            var right = ParseOr();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseOr()
    {
        var expr = ParseAnd();
        while (Match(TokenType.Or))
        {
            var op = Previous().Type;
            var right = ParseAnd();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseAnd()
    {
        var expr = ParseNot();
        while (Match(TokenType.And))
        {
            var op = Previous().Type;
            var right = ParseNot();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseNot()
    {
        if (Match(TokenType.Not))
        {
            var op = Previous().Type;
            var right = ParseNot();
            return new UnaryExpression(op, right);
        }
        return ParseRelational();
    }

    private Expression ParseRelational()
    {
        var expr = ParseAddition();
        while (Match(TokenType.Equal, TokenType.NotEqual, TokenType.LessThan, TokenType.GreaterThan, TokenType.LessEqual, TokenType.GreaterEqual))
        {
            var op = Previous().Type;
            var right = ParseAddition();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseAddition()
    {
        var expr = ParseMod();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var op = Previous().Type;
            var right = ParseMod();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseMod()
    {
        var expr = ParseIntegerDivide();
        while (Match(TokenType.Mod))
        {
            var op = Previous().Type;
            var right = ParseIntegerDivide();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseIntegerDivide()
    {
        var expr = ParseMultiplication();
        while (Match(TokenType.IntegerDivide))
        {
            var op = Previous().Type;
            var right = ParseMultiplication();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseMultiplication()
    {
        var expr = ParseUnary();
        while (Match(TokenType.Multiply, TokenType.Divide))
        {
            var op = Previous().Type;
            var right = ParseUnary();
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.Hash))
        {
            return ParseUnary();
        }
        if (Match(TokenType.Minus, TokenType.Plus))
        {
            var op = Previous().Type;
            var right = ParseUnary();
            return new UnaryExpression(op, right);
        }
        return ParsePower();
    }

    private Expression ParsePower()
    {
        var expr = ParsePrimary();
        while (Match(TokenType.Power))
        {
            var op = Previous().Type;
            var right = ParseUnary(); // Power is right-associative in math
            expr = new BinaryExpression(op, expr, right);
        }
        return expr;
    }

    private Expression ParsePrimary()
    {
        if (Match(TokenType.IntegerLiteral, TokenType.SingleLiteral, TokenType.DoubleLiteral, TokenType.StringLiteral))
        {
            return new LiteralExpression(Previous().Value!.Value);
        }

        if (Match(TokenType.OpenParen))
        {
            var expr = ParseExpression();
            Consume(TokenType.CloseParen, "Expected ')' after expression");
            return expr;
        }

        // Built-in function tokens
        if (IsBuiltInFunctionToken(Peek().Type))
        {
            var token = Advance();
            string fnName = token.Text.ToUpperInvariant();
            var args = new List<Expression>();
            if (Match(TokenType.OpenParen))
            {
                if (!Check(TokenType.CloseParen))
                {
                    do
                    {
                        args.Add(ParseExpression());
                    } while (Match(TokenType.Comma));
                }
                Consume(TokenType.CloseParen, "Expected ')' after function arguments");
            }
            return new FunctionCallExpression(fnName, args);
        }

        if (Match(TokenType.Identifier))
        {
            string name = Previous().Text.ToUpperInvariant();

            // Function call with arguments: A(...) or array access
            if (Match(TokenType.OpenParen))
            {
                var args = new List<Expression>();
                if (!Check(TokenType.CloseParen))
                {
                    do
                    {
                        args.Add(ParseExpression());
                    } while (Match(TokenType.Comma));
                }
                Consume(TokenType.CloseParen, "Expected ')' after arguments");

                if (name.StartsWith("FN", StringComparison.OrdinalIgnoreCase))
                {
                    return new FunctionCallExpression(name, args);
                }

                // If identifier matches built-in name
                return new ArrayAccessExpression(name, args);
            }

            return new VariableExpression(name);
        }

        throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, $"Unexpected token in expression: {Peek().Type} ('{Peek().Text}')");
    }

    private static bool IsBuiltInFunctionToken(TokenType type) => type is
        TokenType.Abs or TokenType.Asc or TokenType.Atn or TokenType.Cdbl or TokenType.ChrStr or
        TokenType.Cint or TokenType.Cos or TokenType.Csng or TokenType.Csrlin or TokenType.Cvd or
        TokenType.Cvi or TokenType.Cvs or TokenType.Eof or TokenType.Erl or TokenType.Err or
        TokenType.Exp or TokenType.Fix or TokenType.Fre or TokenType.HexStr or TokenType.InkeyStr or
        TokenType.Inp or TokenType.InputStr or TokenType.Instr or TokenType.Int or TokenType.LeftStr or
        TokenType.Len or TokenType.Loc or TokenType.Lof or TokenType.Log or TokenType.Lpos or
        TokenType.MkdStr or TokenType.MkiStr or TokenType.MksStr or TokenType.OctStr or TokenType.Peek or
        TokenType.Point or TokenType.Pos or TokenType.RightStr or TokenType.Rnd or TokenType.Sgn or
        TokenType.Sin or TokenType.SpaceStr or TokenType.Sqr or TokenType.StrStr or TokenType.StringStr or
        TokenType.Tan or TokenType.Timer or TokenType.Val or TokenType.DateStr or TokenType.TimeStr or TokenType.EnvironStr or TokenType.MidStr or TokenType.Varptr or TokenType.Play;

    #endregion

    #region Helpers

    private bool IsStatementTerminator() =>
        IsAtEnd() || Check(TokenType.Colon) || Check(TokenType.EndOfLine) || Check(TokenType.Else);

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;

    private Token Advance()
    {
        if (!IsAtEnd()) _pos++;
        return Previous();
    }

    private bool IsAtEnd() => _pos >= _tokens.Count || Peek().Type == TokenType.EndOfFile;

    private Token Peek() => _tokens[_pos];

    private Token Previous() => _tokens[_pos - 1];

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, message);
    }

    private string ConsumeIdentifier(string message)
    {
        if (Check(TokenType.Identifier))
            return Advance().Text.ToUpperInvariant();
        throw new BasicException(BasicErrorCode.SyntaxError, Peek().Line, message);
    }

    private (string Name, List<Expression>? Indices) ParseVariableOrArrayTarget(string message)
    {
        string name = ConsumeIdentifier(message);
        List<Expression>? indices = null;
        if (Match(TokenType.OpenParen))
        {
            indices = new List<Expression>();
            do
            {
                indices.Add(ParseExpression());
            } while (Match(TokenType.Comma));
            Consume(TokenType.CloseParen, "Expected ')' after array indices");
        }
        return (name, indices);
    }

    private bool MatchLineNumber(out int lineNumber)
    {
        if (Match(TokenType.IntegerLiteral))
        {
            lineNumber = Previous().Value?.AsInteger ?? int.Parse(Previous().Text);
            return true;
        }
        if (Match(TokenType.SingleLiteral))
        {
            if (int.TryParse(Previous().Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber))
            {
                return true;
            }
            _pos--;
        }
        lineNumber = 0;
        return false;
    }

    #endregion
}
