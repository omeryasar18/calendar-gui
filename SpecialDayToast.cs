using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CalendarGui
{
    public class SpecialDayToast : Form
    {
        private readonly Timer _timer = new Timer();
        private int _life = 1700; // ms

        public static void ShowToast(Form owner, string title, string emoji, string message)
        {
            var t = new SpecialDayToast(title, emoji, message);

            // Formun TAM ORTASI
            Rectangle rect = owner.RectangleToScreen(owner.ClientRectangle);
            int x = rect.Left + (rect.Width - t.Width) / 2;
            int y = rect.Top + (rect.Height - t.Height) / 2;
            t.Location = new Point(Math.Max(0, x), Math.Max(0, y));

            t.Show(owner);
        }

        private SpecialDayToast(string title, string emoji, string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;

            Width = 340;
            Height = 110;
            BackColor = Color.White;

            Region = Rounded(new Rectangle(0, 0, Width, Height), 18);

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = false,
                Left = 18,
                Top = 14,
                Width = Width - 36,
                Height = 24
            };

            var lblEmoji = new Label
            {
                Text = string.IsNullOrWhiteSpace(emoji) ? "✨" : emoji,
                Font = new Font("Segoe UI Emoji", 26f, FontStyle.Regular),
                AutoSize = false,
                Left = 18,
                Top = 48,
                Width = 48,
                Height = 48,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblMsg = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(55, 65, 81),
                AutoSize = false,
                Left = 72,
                Top = 55,
                Width = Width - 90,
                Height = 40
            };

            Controls.Add(lblTitle);
            Controls.Add(lblEmoji);
            Controls.Add(lblMsg);

            _timer.Interval = 50;
            _timer.Tick += (s, e) =>
            {
                _life -= _timer.Interval;
                if (_life <= 0)
                {
                    _timer.Stop();
                    Close();
                }
            };

            Shown += (s, e) => _timer.Start();

            Click += (s, e) => Close();
            lblTitle.Click += (s, e) => Close();
            lblEmoji.Click += (s, e) => Close();
            lblMsg.Click += (s, e) => Close();
        }

        private Region Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return new Region(path);
        }
    }
}
