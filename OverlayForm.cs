using WhisperNow.Native;

namespace WhisperNow;

internal sealed class OverlayForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private string _statusText = "READY";
    private Color _statusColor = Color.FromArgb(60, 180, 75);
    private readonly Font _font;
    private bool _clickThrough = true;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.85;
        DoubleBuffered = true;

        _font = new Font("Segoe UI", 9f, FontStyle.Bold);

        var screen = Screen.PrimaryScreen!.WorkingArea;
        Width = 140;
        Height = 28;
        Left = (screen.Width - Width) / 2;
        Top = 6;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            if (_clickThrough)
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), 14);
        g.FillPath(bgBrush, path);

        int dotSize = 10;
        int dotY = (Height - dotSize) / 2;
        using var dotBrush = new SolidBrush(_statusColor);
        g.FillEllipse(dotBrush, 12, dotY, dotSize, dotSize);

        using var textBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
        var textRect = new RectangleF(28, 0, Width - 32, Height);
        var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        g.DrawString(_statusText, _font, textBrush, textRect, sf);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    public void SetState(OverlayState state)
    {
        void Apply()
        {
            switch (state)
            {
                case OverlayState.Ready:
                    _statusText = "READY";
                    _statusColor = Color.FromArgb(60, 180, 75);
                    break;
                case OverlayState.Recording:
                    _statusText = "● REC";
                    _statusColor = Color.FromArgb(220, 40, 40);
                    break;
                case OverlayState.Transcribing:
                    _statusText = "WORKING...";
                    _statusColor = Color.FromArgb(255, 165, 0);
                    break;
                case OverlayState.Disabled:
                    _statusText = "PAUSED";
                    _statusColor = Color.FromArgb(120, 120, 120);
                    break;
            }
            Invalidate();
        }

        if (InvokeRequired)
            BeginInvoke(Apply);
        else
            Apply();
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnFormClosing(e);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _font.Dispose();
        base.Dispose(disposing);
    }
}

internal enum OverlayState
{
    Ready,
    Recording,
    Transcribing,
    Disabled
}
