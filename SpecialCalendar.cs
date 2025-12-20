using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace CalendarGui
{
    public class SpecialCalendar : Control
    {
        public DateTime CurrentMonth
        {
            get => _currentMonth;
            set
            {
                _currentMonth = new DateTime(value.Year, value.Month, 1);
                Invalidate();
            }
        }
        private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        public Func<DateTime, bool> IsSpecialDay { get; set; }
        public Func<DateTime, bool> HasEvent { get; set; }

        public event Action<DateTime> SelectedDateChanged;

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value.Date;
                Invalidate();
            }
        }
        private DateTime _selectedDate = DateTime.Today;

        // ✅ Dil/culture (Form1 bunu TR/EN/BS olarak set edecek)
        public CultureInfo CalendarCulture { get; set; } = CultureInfo.GetCultureInfo("tr-TR");

        // Tema
        public Color CalendarBackColor { get; set; } = Color.White;
        public Color TextColor { get; set; } = Color.FromArgb(17, 24, 39);
        public Color MutedTextColor { get; set; } = Color.FromArgb(107, 114, 128);
        public Color AccentColor { get; set; } = Color.FromArgb(16, 185, 129);
        public Color AccentSoftColor { get; set; } = Color.FromArgb(209, 250, 229);
        public Color BorderColor { get; set; } = Color.FromArgb(229, 231, 235);

        // Özel gün (kırmızı dolu)
        public Color SpecialDayFillColor { get; set; } = Color.FromArgb(244, 63, 94);
        public Color SpecialDayTextColor { get; set; } = Color.White;

        // Event günü (mavi dolu)
        public Color EventDayFillColor { get; set; } = Color.FromArgb(59, 130, 246);
        public Color EventDayTextColor { get; set; } = Color.White;

        public SpecialCalendar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = CalendarBackColor;

            Size = new Size(278, 180);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var pad = 14;
            var r = new Rectangle(pad, pad, Width - 2 * pad, Height - 2 * pad);

            using (var bg = new SolidBrush(CalendarBackColor))
                g.FillRectangle(bg, r);

            // ✅ Culture seç
            var culture = CalendarCulture ?? CultureInfo.CurrentCulture;

            // Header (ay adı bu culture'a göre değişecek)
            string header = CurrentMonth.ToString("MMMM yyyy", culture);
            using (var fHeader = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var bText = new SolidBrush(TextColor))
                g.DrawString(header, fHeader, bText, r.X, r.Y);

            // ✅ Weekdays (culture'dan al, Pazartesi başlangıçlı)
            var dtf = culture.DateTimeFormat;
            string[] dn = dtf.AbbreviatedDayNames; // 0=Sunday
            string[] wd = new string[7]
            {
                dn[1], // Mon
                dn[2], // Tue
                dn[3], // Wed
                dn[4], // Thu
                dn[5], // Fri
                dn[6], // Sat
                dn[0]  // Sun
            };

            int headerH = 26;
            int wdY = r.Y + headerH + 8;

            int gridTop = wdY + 18;
            int cols = 7, rows = 6;

            float cellW = r.Width / (float)cols;
            float cellH = (r.Bottom - gridTop) / (float)rows;

            using (var bMuted = new SolidBrush(MutedTextColor))
            using (var fWd = new Font("Segoe UI", 9f, FontStyle.Regular))
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = r.X + c * cellW + (cellW / 2f);
                    var sz = g.MeasureString(wd[c], fWd);
                    g.DrawString(wd[c], fWd, bMuted, x - sz.Width / 2f, wdY);
                }
            }

            var first = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            int dow = ((int)first.DayOfWeek + 6) % 7; // Monday=0
            int days = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);

            using (var pSelected = new Pen(AccentColor, 2.0f))
            using (var bText = new SolidBrush(TextColor))
            using (var bSpecialFill = new SolidBrush(SpecialDayFillColor))
            using (var bSpecialText = new SolidBrush(SpecialDayTextColor))
            using (var bEventFill = new SolidBrush(EventDayFillColor))
            using (var bEventText = new SolidBrush(EventDayTextColor))
            using (var fDay = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                for (int i = 0; i < rows * cols; i++)
                {
                    int rr = i / cols;
                    int cc = i % cols;

                    float x = r.X + cc * cellW;
                    float y = gridTop + rr * cellH;

                    int dayIndex = i - dow + 1;
                    if (dayIndex < 1 || dayIndex > days) continue;

                    var date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, dayIndex);

                    bool selected = date.Date == SelectedDate.Date;
                    bool special = IsSpecialDay?.Invoke(date) == true;
                    bool hasEvent = HasEvent?.Invoke(date) == true;

                    // Hücre alanı
                    var cellRect = new RectangleF(x + 3, y + 3, cellW - 6, cellH - 6);

                    // Öncelik: Special (kırmızı) > Event (mavi)
                    if (special)
                        g.FillRectangle(bSpecialFill, cellRect);
                    else if (hasEvent)
                        g.FillRectangle(bEventFill, cellRect);

                    // Seçili gün: border
                    if (selected)
                        g.DrawRectangle(pSelected, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

                    // Gün numarası (sol üst)
                    string s = dayIndex.ToString(CultureInfo.InvariantCulture);

                    Brush numBrush =
                        special ? bSpecialText :
                        hasEvent ? bEventText :
                        bText;

                    float numX = x + 8;
                    float numY = y + 6;
                    g.DrawString(s, fDay, numBrush, numX, numY);

                    // ✅ Event dot (istersen kalsın)
                    if (hasEvent)
                    {
                        using (var bDot = new SolidBrush(Color.White))
                        {
                            float dotX = x + cellW / 2f;
                            float dotY = y + cellH - 12;
                            g.FillEllipse(bDot, dotX - 2.5f, dotY - 2.5f, 5f, 5f);
                        }
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            var pad = 14;
            var r = new Rectangle(pad, pad, Width - 2 * pad, Height - 2 * pad);

            int headerH = 26;
            int wdY = r.Y + headerH + 8;
            int gridTop = wdY + 18;

            int cols = 7, rows = 6;
            float cellW = r.Width / (float)cols;
            float cellH = (r.Bottom - gridTop) / (float)rows;

            var first = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            int dow = ((int)first.DayOfWeek + 6) % 7;
            int days = DateTime.DaysInMonth(CurrentMonth.Year, CurrentMonth.Month);

            int cc = (int)((e.X - r.X) / cellW);
            int rr = (int)((e.Y - gridTop) / (cellH == 0 ? 1 : cellH));
            if (cc < 0 || cc >= cols || rr < 0 || rr >= rows) return;

            int idx = rr * cols + cc;
            int dayIndex = idx - dow + 1;
            if (dayIndex < 1 || dayIndex > days) return;

            var date = new DateTime(CurrentMonth.Year, CurrentMonth.Month, dayIndex);
            SelectedDate = date;
            SelectedDateChanged?.Invoke(date);
        }
    }
}
