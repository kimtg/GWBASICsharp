using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GWBASIC.ConsoleApp.Drivers;

public class GraphicsWindow : Form
{
    private readonly ConsoleScreenDriver _screenDriver;
    private readonly ConsoleInputDriver? _inputDriver;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private bool _cursorBlinkState = true;
    private int _blinkCounter = 0;

    public GraphicsWindow(ConsoleScreenDriver screenDriver, ConsoleInputDriver? inputDriver)
    {
        _screenDriver = screenDriver;
        _inputDriver = inputDriver;

        Text = "GW-BASIC - Screen 1 (320x200 CGA)";
        ClientSize = new Size(960, 600); // 1.5x scaling of 640x400
        MinimumSize = new Size(640, 400);
        BackColor = Color.Black;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;

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
    }

    public void UpdateMode(int mode)
    {
        Text = mode == 1
            ? "GW-BASIC - Screen 1 (320x200 CGA 4-Color)"
            : "GW-BASIC - Screen 2 (640x200 CGA High-Res Monochrome)";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        lock (_screenDriver.GraphicLock)
        {
            g.DrawImage(_screenDriver.GraphicBitmap, 0, 0, ClientSize.Width, ClientSize.Height);

            // Draw blinking cursor in graphics mode when visible
            if (_cursorBlinkState && _screenDriver.CursorVisible && _screenDriver.Mode != 0)
            {
                float cellW = ClientSize.Width / (float)_screenDriver.Width;
                float cellH = ClientSize.Height / (float)_screenDriver.Height;
                int cr = _screenDriver.CursorRow - 1;
                int cc = _screenDriver.CursorCol - 1;
                if (cr >= 0 && cr < _screenDriver.Height && cc >= 0 && cc < _screenDriver.Width)
                {
                    var curRect = new RectangleF(cc * cellW, (cr + 0.85f) * cellH, cellW, cellH * 0.15f);
                    using var curBrush = new SolidBrush(Color.White);
                    g.FillRectangle(curBrush, curRect);
                }
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _screenDriver.OnGraphicsWindowClosedByUser();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_inputDriver == null) return;

        if (e.KeyCode is >= Keys.F1 and <= Keys.F10)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\0', (ConsoleKey)e.KeyCode, e.Shift, e.Alt, e.Control));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Back)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Tab)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));
            e.Handled = true;
            return;
        }

        if (e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right or
            Keys.Home or Keys.End or Keys.Insert or Keys.Delete or
            Keys.PageUp or Keys.PageDown)
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo('\0', (ConsoleKey)e.KeyCode, e.Shift, e.Alt, e.Control));
            e.Handled = true;
            return;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (_inputDriver == null) return;

        if (!char.IsControl(e.KeyChar))
        {
            _inputDriver.EnqueueKey(new ConsoleKeyInfo(e.KeyChar, (ConsoleKey)char.ToUpperInvariant(e.KeyChar), false, false, false));
            e.Handled = true;
        }
    }
}
