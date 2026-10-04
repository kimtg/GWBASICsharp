using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using GWBASIC.Desktop.Drivers;

namespace GWBASIC.Desktop;

public partial class Form1 : Form
{
    private readonly GuiScreenDriver _screen;
    private readonly GuiAudioDriver _audio;
    private readonly GuiInputDriver _input;
    private readonly PhysicalFileSystemDriver _fileSystem;
    private readonly BasicEnvironment _environment;
    private readonly Interpreter _interpreter;

    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly Thread _interpreterThread;
    private bool _cursorBlinkState = true;
    private int _blinkCounter = 0;

    // Interactive input buffer
    private readonly StringBuilder _currentLine = new();
    private int _cursorPos = 0;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public Form1()
    {
        InitializeComponent();

        Text = "GW-BASIC 3.23";
        ClientSize = new Size(960, 600); // 1.5x scaling of 640x400
        BackColor = Color.Black;
        DoubleBuffered = true;

        _screen = new GuiScreenDriver();
        _audio = new GuiAudioDriver();
        _input = new GuiInputDriver();
        _fileSystem = new PhysicalFileSystemDriver();

        _environment = new BasicEnvironment(_screen, _audio, _input, _fileSystem);
        _interpreter = new Interpreter(_environment);

        _renderTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 FPS
        _renderTimer.Tick += (s, e) =>
        {
            _blinkCounter++;
            if (_blinkCounter >= 15) // Blink every ~500ms
            {
                _cursorBlinkState = !_cursorBlinkState;
                _blinkCounter = 0;
            }
            Invalidate();
        };
        _renderTimer.Start();

        _interpreterThread = new Thread(RunInterpreterLoop) { IsBackground = true };
        _interpreterThread.Start();
    }

    private void RunInterpreterLoop()
    {
        _screen.WriteLine("GW-BASIC 3.23");
        _screen.WriteLine("(C) Copyright Microsoft 1983,1984,1986,1987,1988");
        _screen.WriteLine("60300 Bytes free");
        _screen.WriteLine("Ok");

        while (true)
        {
            string line = _input.ReadLine();
            bool keepGoing = _interpreter.ExecuteInputLine(line);
            if (!keepGoing)
            {
                Invoke(Close);
                break;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        float charW = ClientSize.Width / (float)_screen.Width;
        float charH = ClientSize.Height / (float)_screen.Height;

        using var font = new Font("Consolas", Math.Max(8, charH * 0.75f), FontStyle.Bold);

        if (_screen.Mode == 0)
        {
            // Text mode
            for (int r = 0; r < _screen.Height; r++)
            {
                for (int c = 0; c < _screen.Width; c++)
                {
                    char ch = _screen.CharBuffer[r, c];
                    int fg = _screen.FgColorBuffer[r, c];
                    int bg = _screen.BgColorBuffer[r, c];

                    var fgEntry = CgaPalette.GetColor(fg);
                    var bgEntry = CgaPalette.GetColor(bg);

                    var destRect = new RectangleF(c * charW, r * charH, charW, charH);

                    // Background
                    using var bgBrush = new SolidBrush(Color.FromArgb(bgEntry.R, bgEntry.G, bgEntry.B));
                    g.FillRectangle(bgBrush, destRect);

                    // Foreground character
                    if (ch != ' ')
                    {
                        using var fgBrush = new SolidBrush(Color.FromArgb(fgEntry.R, fgEntry.G, fgEntry.B));
                        g.DrawString(ch.ToString(), font, fgBrush, destRect.X, destRect.Y - 2);
                    }
                }
            }

            // Blinking cursor
            if (_cursorBlinkState && _screen.CursorVisible)
            {
                int cr = _screen.CursorRow - 1;
                int cc = _screen.CursorCol - 1 + _cursorPos;
                if (cr >= 0 && cr < _screen.Height && cc >= 0 && cc < _screen.Width)
                {
                    var curRect = new RectangleF(cc * charW, (cr + 0.85f) * charH, charW, charH * 0.15f);
                    using var curBrush = new SolidBrush(Color.White);
                    g.FillRectangle(curBrush, curRect);
                }
            }

            // Soft function keys line (Row 25)
            if (_screen.KeyRowVisible)
            {
                int r = _screen.Height - 1;
                float keyW = ClientSize.Width / 10.0f;

                for (int i = 0; i < 10; i++)
                {
                    string label = i < _screen.KeyLabels.Length && _screen.KeyLabels[i] != null ? _screen.KeyLabels[i] : "";
                    string num = ((i + 1) % 10).ToString();
                    string text = label.Length > 1 ? label[1..] : "";

                    var numRect = new RectangleF(i * keyW, r * charH, keyW * 0.2f, charH);
                    var textRect = new RectangleF(i * keyW + keyW * 0.2f, r * charH, keyW * 0.8f, charH);

                    // Number (white on black/gray)
                    g.FillRectangle(Brushes.White, numRect);
                    g.DrawString(num, font, Brushes.Black, numRect.X, numRect.Y - 2);

                    // Text (cyan bar)
                    using var barBrush = new SolidBrush(Color.DarkCyan);
                    g.FillRectangle(barBrush, textRect);
                    g.DrawString(text, font, Brushes.White, textRect.X, textRect.Y - 2);
                }
            }
        }
        else
        {
            // Graphics mode (Screen 1, 2)
            g.DrawImage(_screen.GraphicBitmap, 0, 0, ClientSize.Width, ClientSize.Height);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode is >= Keys.F1 and <= Keys.F10)
        {
            int fIdx = (e.KeyCode - Keys.F1) + 1;
            string macro = _environment.GetFunctionKey(fIdx);
            foreach (char ch in macro)
            {
                if (ch == '\r')
                {
                    string submit = _currentLine.ToString();
                    _screen.WriteLine(submit);
                    _history.Add(submit);
                    _currentLine.Clear();
                    _cursorPos = 0;
                    _input.EnqueueLine(submit);
                    return;
                }
                _currentLine.Insert(_cursorPos++, ch);
                _screen.Write(ch.ToString());
            }
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            string submit = _currentLine.ToString();
            _screen.WriteLine("");
            if (!string.IsNullOrWhiteSpace(submit))
            {
                _history.Add(submit);
            }
            _currentLine.Clear();
            _cursorPos = 0;
            _historyIndex = -1;
            _input.EnqueueLine(submit);
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Back)
        {
            if (_cursorPos > 0)
            {
                _cursorPos--;
                _currentLine.Remove(_cursorPos, 1);
                // Erase character on screen
                _screen.Locate(_screen.CursorRow, Math.Max(1, _screen.CursorCol - 1));
                _screen.Write(" ");
                _screen.Locate(_screen.CursorRow, Math.Max(1, _screen.CursorCol - 1));
            }
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            if (_history.Count > 0 && _historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                string hist = _history[_history.Count - 1 - _historyIndex];
                _currentLine.Clear();
                _currentLine.Append(hist);
                _cursorPos = hist.Length;
            }
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                string hist = _history[_history.Count - 1 - _historyIndex];
                _currentLine.Clear();
                _currentLine.Append(hist);
                _cursorPos = hist.Length;
            }
            else if (_historyIndex == 0)
            {
                _historyIndex = -1;
                _currentLine.Clear();
                _cursorPos = 0;
            }
            e.Handled = true;
            return;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        _input.EnqueueChar(e.KeyChar);

        if (!char.IsControl(e.KeyChar))
        {
            _currentLine.Insert(_cursorPos++, e.KeyChar);
            _screen.Write(e.KeyChar.ToString());
            e.Handled = true;
        }
    }
}
