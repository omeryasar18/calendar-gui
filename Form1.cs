using System;
using System.Globalization;
using System.Windows.Forms;
using System.Data.SQLite;
using Guna.UI2.WinForms;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Threading.Tasks;

namespace CalendarGui
{
    public partial class Form1 : Form
    {
        private const string DbPath = "events.db";

        // Telegram
        private static readonly HttpClient http = new HttpClient();
        private const string TELEGRAM_BOT_TOKEN = "8354043798:AAHZmXHV4efi7CFMCJtMPDpr6JQBAoFKfe8";
        private const string TELEGRAM_CHAT_ID = "8069386533";

        private enum AppLanguage { Turkish, English, Bosnian }
        private AppLanguage currentLanguage = AppLanguage.Turkish;

        private string messageEmptyDesc_;
        private string messageSelectForDelete_;
        private string messageNotFound_;
        private Button buttonAllDates_;
        private string messageNoEvents_;


        private Dictionary<AppLanguage, Dictionary<string, string>> specialDays;
        private string currentSpecialDayName_;

        private Label labelSpecialDay;
        private SpecialCalendar specialCalendar;
        private HashSet<string> eventDatesForMonth = new HashSet<string>();

      
        private DateTime? _lastToastDate = null;
        private DateTime _lastToastAt = DateTime.MinValue;

        private readonly Dictionary<string, string> specialDayEmojis = new Dictionary<string, string>
        {
            ["01-01"] = "🎅",
            ["01-02"] = "✨",
            ["03-01"] = "🇧🇦",
            ["04-23"] = "🧒",
            ["05-01"] = "🛠️",
            ["05-02"] = "🛠️",
            ["05-19"] = "🏃",
            ["07-04"] = "🎆",
            ["07-15"] = "🕊️",
            ["08-30"] = "🇹🇷",
            ["10-29"] = "🎉",
            ["11-11"] = "🎖️",
            ["11-25"] = "🇧🇦",
            ["11-28"] = "🦃",
            ["12-25"] = "🎄"
        };

        private string GetSpecialEmoji_(DateTime date)
        {
            string key = date.ToString("MM-dd", CultureInfo.InvariantCulture);
            return specialDayEmojis.TryGetValue(key, out var emj) ? emj : "✨";
        }

        private void TryShowSpecialToast_(DateTime date)
        {
            if (specialDays == null) return;
            if (!specialDays.TryGetValue(currentLanguage, out var dict)) return;

            string key = date.ToString("MM-dd", CultureInfo.InvariantCulture);
            if (!dict.TryGetValue(key, out string dayName)) return;


            if (_lastToastDate.HasValue && _lastToastDate.Value.Date == date.Date)
            {
                if ((DateTime.Now - _lastToastAt).TotalMilliseconds < 900)
                    return;
            }

            _lastToastDate = date.Date;
            _lastToastAt = DateTime.Now;

            string title = currentLanguage == AppLanguage.English ? "Special day"
                       : currentLanguage == AppLanguage.Bosnian ? "Poseban dan"
                       : "Özel gün";

            string emoji = GetSpecialEmoji_(date);

          
            SpecialDayToast.ShowToast(this, title, emoji, dayName);
        }

      
        private void ApplyCalendarCulture()
        {
            if (specialCalendar == null) return;

            switch (currentLanguage)
            {
                case AppLanguage.Turkish:
                    specialCalendar.CalendarCulture = CultureInfo.GetCultureInfo("tr-TR");
                    break;
                case AppLanguage.English:
                    specialCalendar.CalendarCulture = CultureInfo.GetCultureInfo("en-US");
                    break;
                case AppLanguage.Bosnian:
                    specialCalendar.CalendarCulture = CultureInfo.GetCultureInfo("bs-BA");
                    break;
            }

            specialCalendar.Invalidate();
        }

       
        private readonly Color C_BG = Color.FromArgb(245, 243, 255);
        private readonly Color C_CARD = Color.White;
        private readonly Color C_TEXT = Color.FromArgb(17, 24, 39);
        private readonly Color C_MUTED = Color.FromArgb(107, 114, 128);
        private readonly Color C_ACCENT = Color.FromArgb(16, 185, 129);
        private readonly Color C_ACCENT_SOFT = Color.FromArgb(209, 250, 229);
        private readonly Color C_DANGER = Color.FromArgb(244, 63, 94);
        private readonly Color C_BORDER = Color.FromArgb(229, 231, 235);

       
        private const int LEFT_WRAP_WIDTH = 360;
        private const int LEFT_WRAP_MARGIN = 20;
        private const int GAP = 30;

        private bool _concept3LeftBuilt = false;

       
        private Guna2Panel panelTopBar;
        private Guna2Panel panelInputRow;
        private Guna2Panel panelEventsCard;

        public Form1()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Maximized;
            this.AcceptButton = buttonAdd;

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown_;
            this.MouseDown += Form1_MouseDown;
            this.Resize += Form1_Resize_;

            if (guna2DateTimePicker1 != null)
            {
                guna2DateTimePicker1.Value = DateTime.Today;
                guna2DateTimePicker1.MouseDown += guna2DateTimePicker1_MouseDown;
            }

         
            if (textBoxEvent != null)
            {
                textBoxEvent.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        buttonAdd.PerformClick();
                    }
                };
            }

            labelSpecialDay = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Visible = false
            };
            this.Controls.Add(labelSpecialDay);

            if (comboLanguage != null)
            {
                if (comboLanguage.Items.Count == 0)
                {
                    comboLanguage.Items.Add("Türkçe");
                    comboLanguage.Items.Add("English");
                    comboLanguage.Items.Add("Bosanski");
                }

                comboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
                if (comboLanguage.SelectedIndex < 0)
                    comboLanguage.SelectedIndex = 0;
            }

            ApplyConcept3Theme_();

            InitializeSpecialDays_();
            ApplyLanguage_();

            specialCalendar = new SpecialCalendar();
            specialCalendar.CurrentMonth = guna2DateTimePicker1.Value;


            ApplyCalendarCulture();

           
            specialCalendar.IsSpecialDay = date =>
            {
                if (specialDays != null && specialDays.TryGetValue(currentLanguage, out var dict))
                {
                    string key = date.ToString("MM-dd", CultureInfo.InvariantCulture);
                    return dict.ContainsKey(key);
                }
                return false;
            };

            specialCalendar.HasEvent = date =>
            {
                string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return eventDatesForMonth.Contains(key);
            };

           
            specialCalendar.SelectedDateChanged += date =>
            {
                if (guna2DateTimePicker1 != null)
                    guna2DateTimePicker1.Value = date;

                TryShowSpecialToast_(date.Date);
            };

            this.Controls.Add(specialCalendar);

            InitializeDatabase_();
            LoadEventsForSelectedDate_();
            RefreshEventDatesForMonth(guna2DateTimePicker1.Value);

            ShowSpecialDayIfExists(guna2DateTimePicker1.Value.Date);

            BuildConcept3LeftColumn_();
            BuildRightSideLayout_();

            specialCalendar.SelectedDate = guna2DateTimePicker1.Value.Date;
            specialCalendar.Invalidate();
        }


        private async Task SendTelegramMessageAsync_(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TELEGRAM_BOT_TOKEN) || TELEGRAM_BOT_TOKEN.Contains("BURAYA_")) return;
                if (string.IsNullOrWhiteSpace(TELEGRAM_CHAT_ID) || TELEGRAM_CHAT_ID.Contains("BURAYA_")) return;

                string url =
                    $"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/sendMessage" +
                    $"?chat_id={TELEGRAM_CHAT_ID}&text={Uri.EscapeDataString(message)}";

                await http.GetAsync(url);
            }
            catch
            {
       
            }
        }



        private void ApplyConcept3Theme_()
        {
            this.BackColor = C_BG;
            this.ForeColor = C_TEXT;
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            if (label1 != null)
            {
                label1.ForeColor = C_TEXT;
                label1.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            }

            if (labelSpecialDay != null)
            {
                labelSpecialDay.ForeColor = C_ACCENT;
                labelSpecialDay.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }

            if (comboLanguage is Guna2ComboBox gcb)
            {
                gcb.BorderRadius = 10;
                gcb.FillColor = C_CARD;
                gcb.ForeColor = C_TEXT;
                gcb.BorderColor = C_BORDER;
                gcb.HoverState.BorderColor = C_ACCENT;
                gcb.FocusedState.BorderColor = C_ACCENT;
            }
            else if (comboLanguage != null)
            {
                comboLanguage.BackColor = C_CARD;
                comboLanguage.ForeColor = C_TEXT;
            }

            StyleEventTextBox_(textBoxEvent);
            StyleButton_(buttonAdd, isDanger: false);
            StyleButton_(buttonDelete, isDanger: true);
            StyleListBoxAsCards_(listBoxEvents);
        }

        private void StyleEventTextBox_(TextBox tb)
        {
            if (tb == null) return;
            tb.BackColor = C_CARD;
            tb.ForeColor = C_TEXT;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        }

        private void StyleButton_(Control btn, bool isDanger)
        {
            if (btn == null) return;

            if (btn is Guna2Button gb)
            {
                gb.BorderRadius = 14;
                gb.FillColor = isDanger ? C_DANGER : C_ACCENT;
                gb.ForeColor = Color.White;
                gb.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                gb.HoverState.FillColor = isDanger
                    ? Color.FromArgb(251, 113, 133)
                    : Color.FromArgb(52, 211, 153);
                gb.Cursor = Cursors.Hand;

                gb.ShadowDecoration.Enabled = true;
                gb.ShadowDecoration.Depth = 6;
                gb.ShadowDecoration.BorderRadius = 14;
            }
            else if (btn is Button b)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = isDanger ? C_DANGER : C_ACCENT;
                b.ForeColor = Color.White;
                b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                b.Cursor = Cursors.Hand;
            }
        }

        private void StyleListBoxAsCards_(ListBox lb)
        {
            if (lb == null) return;

            lb.BorderStyle = BorderStyle.None;
            lb.BackColor = C_BG;
            lb.ForeColor = C_TEXT;
            lb.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            lb.ItemHeight = 46;
            lb.DrawMode = DrawMode.OwnerDrawFixed;

            lb.DrawItem -= ListBox_DrawItem_Concept3_;
            lb.DrawItem += ListBox_DrawItem_Concept3_;
        }

        private void ListBox_DrawItem_Concept3_(object sender, DrawItemEventArgs e)
        {
            var lb = (ListBox)sender;
            e.DrawBackground();
            if (e.Index < 0) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = e.Bounds;
            r.Inflate(-6, -6);

            Color fill = selected ? C_ACCENT_SOFT : C_CARD;
            Color border = selected ? C_ACCENT : C_BORDER;

            using (GraphicsPath path = RoundedRect_(r, 14))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1))
            using (SolidBrush textBrush = new SolidBrush(C_TEXT))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);

                string text = lb.Items[e.Index].ToString();
                Rectangle textRect = new Rectangle(r.X + 14, r.Y + 12, r.Width - 28, r.Height - 10);
                g.DrawString(text, lb.Font, textBrush, textRect);
            }

            e.DrawFocusRectangle();
        }

        private GraphicsPath RoundedRect_(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

     
        private void BuildConcept3LeftColumn_()
        {
            if (_concept3LeftBuilt) return;
            _concept3LeftBuilt = true;

            var leftWrap = new Guna2Panel
            {
                Name = "panelLeftWrap",
                BorderRadius = 26,
                FillColor = Color.FromArgb(233, 238, 246),
                Location = new Point(LEFT_WRAP_MARGIN, LEFT_WRAP_MARGIN),
                Size = new Size(LEFT_WRAP_WIDTH, this.ClientSize.Height - (LEFT_WRAP_MARGIN * 2)),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                Padding = new Padding(18),
                BackColor = Color.Transparent
            };
            leftWrap.ShadowDecoration.Enabled = true;
            leftWrap.ShadowDecoration.Depth = 10;
            leftWrap.ShadowDecoration.BorderRadius = 26;

            this.Controls.Add(leftWrap);
            leftWrap.BringToFront();

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 320));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftWrap.Controls.Add(tlp);

            var card1 = MakeCardPanel_();
            var card2 = MakeCardPanel_();
            var card3 = MakeCardPanel_();

            card1.Margin = new Padding(0, 0, 0, 18);
            card2.Margin = new Padding(0, 0, 0, 18);

            tlp.Controls.Add(card1, 0, 0);
            tlp.Controls.Add(card2, 0, 1);
            tlp.Controls.Add(card3, 0, 2);

            if (guna2DateTimePicker1 != null)
            {
                guna2DateTimePicker1.Parent = card1;
                guna2DateTimePicker1.Location = new Point(18, 18);
                guna2DateTimePicker1.Size = new Size(card1.Width - 36, 46);
                guna2DateTimePicker1.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

                guna2DateTimePicker1.BorderRadius = 14;
                guna2DateTimePicker1.FillColor = Color.White;
                guna2DateTimePicker1.ForeColor = C_TEXT;
                guna2DateTimePicker1.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            }

            var lblHint = new Label
            {
                Text = "Seçili tarih",
                AutoSize = true,
                ForeColor = C_MUTED,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(22, 70)
            };
            card1.Controls.Add(lblHint);

            if (specialCalendar != null)
            {
                specialCalendar.Parent = card2;
                specialCalendar.Location = new Point(10, 10);
                specialCalendar.Size = new Size(card2.Width - 20, card2.Height - 20);
                specialCalendar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                specialCalendar.CalendarBackColor = Color.White;
                specialCalendar.TextColor = C_TEXT;
                specialCalendar.MutedTextColor = C_MUTED;
                specialCalendar.AccentColor = C_ACCENT;
                specialCalendar.AccentSoftColor = C_ACCENT_SOFT;
                specialCalendar.BorderColor = C_BORDER;

                specialCalendar.SpecialDayFillColor = C_DANGER;
                specialCalendar.SpecialDayTextColor = Color.White;

                specialCalendar.EventDayFillColor = Color.FromArgb(59, 130, 246);
                specialCalendar.EventDayTextColor = Color.White;

                specialCalendar.SelectedDate = guna2DateTimePicker1?.Value.Date ?? DateTime.Today;
            }

            if (pictureLogo != null)
            {
                pictureLogo.Parent = card3;
                pictureLogo.SizeMode = PictureBoxSizeMode.Zoom;
                pictureLogo.Dock = DockStyle.Fill;
                pictureLogo.Margin = new Padding(18);
            }
        }

        private Guna2Panel MakeCardPanel_()
        {
            var p = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 18,
                FillColor = Color.White,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };
            p.ShadowDecoration.Enabled = true;
            p.ShadowDecoration.Depth = 10;
            p.ShadowDecoration.BorderRadius = 18;
            return p;
        }

 
        private void BuildRightSideLayout_()
        {
            int startX = LEFT_WRAP_MARGIN + LEFT_WRAP_WIDTH + GAP;

            int rightPadding = 30;
            int availableW = this.ClientSize.Width - startX - rightPadding;
            if (availableW < 350) availableW = 350;

            // Üst bar
            if (panelTopBar == null)
            {
                panelTopBar = new Guna2Panel
                {
                    Name = "panelTopBar",
                    BorderRadius = 18,
                    FillColor = Color.White,
                    Location = new Point(startX, 20),
                    Size = new Size(availableW, 70),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.Transparent,
                    Padding = new Padding(18, 14, 18, 14)
                };
                panelTopBar.ShadowDecoration.Enabled = true;
                panelTopBar.ShadowDecoration.BorderRadius = 18;
                panelTopBar.ShadowDecoration.Depth = 8;
                this.Controls.Add(panelTopBar);
                panelTopBar.BringToFront();

                var lblTitle = new Label
                {
                    Text = this.Text,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = C_TEXT,
                    Location = new Point(18, 18),
                    Name = "lblTopTitle"
                };
                panelTopBar.Controls.Add(lblTitle);
            }

            panelTopBar.Location = new Point(startX, 20);
            panelTopBar.Width = availableW;

            int rightX = panelTopBar.Width - 18;

            if (comboLanguage != null)
            {
                comboLanguage.Parent = panelTopBar;
                comboLanguage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                comboLanguage.Width = 140;
                comboLanguage.Height = 36;
                comboLanguage.Top = 18;
                comboLanguage.Left = rightX - comboLanguage.Width;
                rightX = comboLanguage.Left - 12;
            }

            if (pictureFlag != null)
            {
                pictureFlag.Parent = panelTopBar;
                pictureFlag.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                pictureFlag.SizeMode = PictureBoxSizeMode.Zoom;
                pictureFlag.Size = new Size(28, 20);
                pictureFlag.Top = 26;
                pictureFlag.Left = rightX - pictureFlag.Width;
                rightX = pictureFlag.Left - 14;
            }

            if (buttonDelete != null)
            {
                buttonDelete.Parent = panelTopBar;
                buttonDelete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                buttonDelete.Size = new Size(110, 36);
                buttonDelete.Top = 18;
                buttonDelete.Left = rightX - buttonDelete.Width;
                rightX = buttonDelete.Left - 10;
            }
           

            if (buttonAllDates_ == null)
            {
                buttonAllDates_ = new Button
                {
                    Name = "buttonAllDates",
                    Text = "Tüm Tarihler",
                    FlatStyle = FlatStyle.Flat
                };
                buttonAllDates_.FlatAppearance.BorderSize = 0;
                buttonAllDates_.Click += buttonAllDates_Click_;

                StyleButton_(buttonAllDates_, isDanger: false);
            }

            buttonAllDates_.Parent = panelTopBar;
            buttonAllDates_.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonAllDates_.Size = new Size(140, 36);
            buttonAllDates_.Top = 18;
            buttonAllDates_.Left = rightX - buttonAllDates_.Width;
            rightX = buttonAllDates_.Left - 10;


            if (buttonAdd != null)
            {
                buttonAdd.Parent = panelTopBar;
                buttonAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                buttonAdd.Size = new Size(110, 36);
                buttonAdd.Top = 18;
                buttonAdd.Left = rightX - buttonAdd.Width;
                rightX = buttonAdd.Left - 10;
            }

          
            var titleLbl = panelTopBar.Controls["lblTopTitle"] as Label;

            if (labelSpecialDay != null)
            {
                labelSpecialDay.Parent = panelTopBar;

                labelSpecialDay.AutoSize = false;
                labelSpecialDay.AutoEllipsis = true;
                labelSpecialDay.TextAlign = ContentAlignment.MiddleCenter;

                labelSpecialDay.ForeColor = C_ACCENT;
                labelSpecialDay.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

                int leftBound = (titleLbl != null) ? (titleLbl.Right + 16) : 18;
                int rightBound = (buttonAdd != null) ? (buttonAdd.Left - 16) : (panelTopBar.Width - 18);

                int w = rightBound - leftBound;
                if (w < 80) w = 80;

                labelSpecialDay.SetBounds(leftBound, 24, w, 22);
                labelSpecialDay.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

         
            if (panelInputRow == null)
            {
                panelInputRow = new Guna2Panel
                {
                    Name = "panelInputRow",
                    BorderRadius = 18,
                    FillColor = Color.White,
                    Location = new Point(startX, panelTopBar.Bottom + 16),
                    Size = new Size(availableW, 86),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.Transparent,
                    Padding = new Padding(18, 18, 18, 18)
                };
                panelInputRow.ShadowDecoration.Enabled = true;
                panelInputRow.ShadowDecoration.BorderRadius = 18;
                panelInputRow.ShadowDecoration.Depth = 8;
                this.Controls.Add(panelInputRow);
                panelInputRow.BringToFront();
            }

            panelInputRow.Location = new Point(startX, panelTopBar.Bottom + 16);
            panelInputRow.Width = availableW;
            panelInputRow.Height = 86;

            int rowY = (panelInputRow.Height - 34) / 2;

            int labelW = 170;

            if (label1 != null)
            {
                label1.Parent = panelInputRow;

                label1.AutoSize = false;
                label1.Width = labelW;
                label1.Height = 34;
                label1.TextAlign = ContentAlignment.MiddleLeft;

                label1.ForeColor = C_MUTED;
                label1.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                label1.Location = new Point(18, rowY);
            }

            if (textBoxEvent != null)
            {
                textBoxEvent.Parent = panelInputRow;
                textBoxEvent.Height = 34;
                textBoxEvent.Top = rowY;

                int textLeft = 18 + labelW + 12;
                textBoxEvent.Left = textLeft;

                textBoxEvent.Width = panelInputRow.Width - textLeft - 18;
                textBoxEvent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

      
            if (panelEventsCard == null)
            {
                panelEventsCard = new Guna2Panel
                {
                    Name = "panelEventsCard",
                    BorderRadius = 18,
                    FillColor = Color.White,
                    Location = new Point(startX, panelInputRow.Bottom + 16),
                    Size = new Size(availableW, this.ClientSize.Height - (panelInputRow.Bottom + 16) - 20),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.Transparent,
                    Padding = new Padding(14)
                };
                panelEventsCard.ShadowDecoration.Enabled = true;
                panelEventsCard.ShadowDecoration.BorderRadius = 18;
                panelEventsCard.ShadowDecoration.Depth = 8;
                this.Controls.Add(panelEventsCard);
                panelEventsCard.BringToFront();
            }

            panelEventsCard.Location = new Point(startX, panelInputRow.Bottom + 16);
            panelEventsCard.Size = new Size(availableW, this.ClientSize.Height - panelEventsCard.Top - 20);

            if (listBoxEvents != null)
            {
                listBoxEvents.Parent = panelEventsCard;
                listBoxEvents.Dock = DockStyle.Fill;
            }

            var title = panelTopBar.Controls["lblTopTitle"] as Label;
            if (title != null) title.Text = this.Text;
        }

    
        private void Form1_KeyDown_(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (listBoxEvents != null && listBoxEvents.SelectedItem != null)
                {
                    buttonDelete.PerformClick();
                    e.Handled = true;
                }
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (ActiveControl == textBoxEvent || ActiveControl == guna2DateTimePicker1 || ActiveControl == comboLanguage)
                {
                    buttonAdd.PerformClick();
                    e.Handled = true;
                }
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            this.ActiveControl = null;
        }

        private void Form1_Resize_(object sender, EventArgs e)
        {
            BuildRightSideLayout_();
        }

        // DB
       
        private void InitializeDatabase_()
        {
            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "CREATE TABLE IF NOT EXISTS Events (" +
                        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "EventDate TEXT NOT NULL, " +
                        "Description TEXT NOT NULL" +
                        ");";

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void RefreshEventDatesForMonth(DateTime referenceDate)
        {
            eventDatesForMonth.Clear();

            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    DateTime monthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
                    DateTime monthEnd = monthStart.AddMonths(1);

                    cmd.CommandText =
                        "SELECT DISTINCT EventDate FROM Events " +
                        "WHERE EventDate >= $start AND EventDate < $end;";

                    cmd.Parameters.AddWithValue("$start", monthStart.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("$end", monthEnd.ToString("yyyy-MM-dd"));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string dateStr = reader.GetString(0);
                            eventDatesForMonth.Add(dateStr);
                        }
                    }
                }
            }
        }

        private void LoadEventsForSelectedDate_()
        {
            DateTime date = guna2DateTimePicker1.Value.Date;
            listBoxEvents.Items.Clear();

            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT Id, EventDate, Description " +
                        "FROM Events " +
                        "WHERE EventDate = $date " +
                        "ORDER BY Id;";

                    cmd.Parameters.AddWithValue("$date",
                        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string dateStr = reader.GetString(1);
                            string desc = reader.GetString(2);

                            listBoxEvents.Items.Add(new EventItem
                            {
                                Id = id,
                                Text = dateStr + " - " + desc
                            });
                        }
                    }
                }
            }

            int maxWidth = 0;
            foreach (var obj in listBoxEvents.Items)
            {
                string text = obj.ToString();
                int w = TextRenderer.MeasureText(text, listBoxEvents.Font).Width;
                if (w > maxWidth) maxWidth = w;
            }

            listBoxEvents.HorizontalScrollbar = true;
            listBoxEvents.HorizontalExtent = maxWidth + 10;
        }

        private void SaveEventToDatabase(DateTime date, string text)
        {
            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Events (EventDate, Description) " +
                        "VALUES ($date, $desc);";

                    cmd.Parameters.AddWithValue("$date",
                        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("$desc", text);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private bool DeleteEventById(int id)
        {
            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Events WHERE Id = $id;";
                    cmd.Parameters.AddWithValue("$id", id);

                    int affected = cmd.ExecuteNonQuery();
                    return affected > 0;
                }
            }
        }

    
        private void HandleLanguageChange()
        {
            int index = comboLanguage.SelectedIndex;

            if (index == 0) currentLanguage = AppLanguage.Turkish;
            else if (index == 1) currentLanguage = AppLanguage.English;
            else if (index == 2) currentLanguage = AppLanguage.Bosnian;
            else currentLanguage = AppLanguage.Turkish;

            ApplyLanguage_();
            ApplyCalendarCulture(); 

            if (guna2DateTimePicker1 != null)
                ShowSpecialDayIfExists(guna2DateTimePicker1.Value.Date);

            specialCalendar?.Invalidate();

            if (panelTopBar != null)
            {
                var title = panelTopBar.Controls["lblTopTitle"] as Label;
                if (title != null) title.Text = this.Text;
            }

            BuildRightSideLayout_(); 
        }

        private void comboLanguage_SelectedIndexChanged(object sender, EventArgs e) => HandleLanguageChange();
        private void comboLanguage_SelectedIndexChanged_1(object sender, EventArgs e) => HandleLanguageChange();

        private void ApplyLanguage_()
        {
            switch (currentLanguage)
            {
                case AppLanguage.Turkish:
                    messageNoEvents_ = "Henüz hiç etkinlik yok.";
                    if (buttonAllDates_ != null) buttonAllDates_.Text = "Tüm Tarihler";

                    this.Text = "Takvim";
                    buttonAdd.Text = "Ekle";
                    buttonDelete.Text = "Sil";
                    messageEmptyDesc_ = "Etkinlik açıklaması boş olamaz.";
                    messageSelectForDelete_ = "Silmek için listeden bir etkinlik seç.";
                    messageNotFound_ = "Etkinlik bulunamadı (ID geçersiz).";
                    if (label1 != null) label1.Text = "Etkinliği gir:";
                    break;

                case AppLanguage.English:
                    messageNoEvents_ = "There are no events yet.";
                    if (buttonAllDates_ != null) buttonAllDates_.Text = "All Dates";

                    this.Text = "Calendar";
                    buttonAdd.Text = "Add";
                    buttonDelete.Text = "Delete";
                    messageEmptyDesc_ = "Event description cannot be empty.";
                    messageSelectForDelete_ = "Select an event from the list to delete.";
                    messageNotFound_ = "No event found (invalid ID).";
                    if (label1 != null) label1.Text = "Enter the event:";
                    break;

                case AppLanguage.Bosnian:
                    messageNoEvents_ = "Još nema događaja.";
                    if (buttonAllDates_ != null) buttonAllDates_.Text = "Svi Datumi";

                    this.Text = "Kalendar";
                    buttonAdd.Text = "Dodaj";
                    buttonDelete.Text = "Obriši";
                    messageEmptyDesc_ = "Opis događaja ne može biti prazan.";
                    messageSelectForDelete_ = "Odaberi događaj iz liste za brisanje.";
                    messageNotFound_ = "Događaj nije pronađen (neispravan ID).";
                    if (label1 != null) label1.Text = "Unesite događaj:";
                    break;
            }
            Update_Flag();
        }

        private void Update_Flag()
        {
            if (pictureFlag == null) return;

            switch (currentLanguage)
            {
                case AppLanguage.Turkish:
                    pictureFlag.Image = Properties.Resources.Flag_tr;
                    break;

                case AppLanguage.English:
                    pictureFlag.Image = Properties.Resources.flag_eng;
                    break;

                case AppLanguage.Bosnian:
                    pictureFlag.Image = Properties.Resources.flag_bih;
                    break;
            }
        }

      
        private void guna2DateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            LoadEventsForSelectedDate_();
            ShowSpecialDayIfExists(guna2DateTimePicker1.Value.Date);

            if (specialCalendar != null)
            {
                specialCalendar.CurrentMonth = guna2DateTimePicker1.Value;
                RefreshEventDatesForMonth(guna2DateTimePicker1.Value);
                specialCalendar.SelectedDate = guna2DateTimePicker1.Value.Date;
                specialCalendar.Invalidate();
            }

            TryShowSpecialToast_ (guna2DateTimePicker1.Value.Date);
        }

        private void guna2DateTimePicker1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (string.IsNullOrEmpty(currentSpecialDayName_)) return;

            string title = currentLanguage == AppLanguage.English ? "Special Day"
                         : currentLanguage == AppLanguage.Bosnian ? "Poseban dan"
                         : "Özel Gün";

            MessageBox.Show(currentSpecialDayName_, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

       
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            DateTime date = guna2DateTimePicker1.Value.Date;
            string desc = textBoxEvent.Text.Trim();

            if (string.IsNullOrWhiteSpace(desc))
            {
                MessageBox.Show(messageEmptyDesc_);
                return;
            }

            SaveEventToDatabase(date, desc);
            textBoxEvent.Clear();
            LoadEventsForSelectedDate_();

            RefreshEventDatesForMonth(guna2DateTimePicker1.Value);
            specialCalendar?.Invalidate();

            _ = SendTelegramMessageAsync_($"📅 Yeni etkinlik eklendi!\nTarih: {date:yyyy-MM-dd}\nAçıklama: {desc}");
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            EventItem item = listBoxEvents.SelectedItem as EventItem;
            if (item == null)
            {
                MessageBox.Show(messageSelectForDelete_);
                return;
            }

            if (DeleteEventById(item.Id))
            {
                LoadEventsForSelectedDate_();
                RefreshEventDatesForMonth(guna2DateTimePicker1.Value);
                specialCalendar?.Invalidate();
            }
            else
            {
                MessageBox.Show(messageNotFound_);
            }
        }

        
        private void InitializeSpecialDays_()
        {
            specialDays = new Dictionary<AppLanguage, Dictionary<string, string>>();

            var tr = new Dictionary<string, string>
            {
                ["01-01"] = "Yılbaşı",
                ["04-23"] = "Ulusal Egemenlik ve Çocuk Bayramı",
                ["05-01"] = "Emek ve Dayanışma Günü",
                ["05-19"] = "Atatürk'ü Anma, Gençlik ve Spor Bayramı",
                ["07-15"] = "Demokrasi ve Millî Birlik Günü",
                ["08-30"] = "Zafer Bayramı",
                ["10-29"] = "Cumhuriyet Bayramı"
            };

            var en = new Dictionary<string, string>
            {
                ["01-01"] = "New Year's Day",
                ["07-04"] = "Independence Day",
                ["11-11"] = "Veterans Day",
                ["11-28"] = "Thanksgiving Day",
                ["12-25"] = "Christmas Day"
            };

            var bs = new Dictionary<string, string>
            {
                ["01-01"] = "Nova godina",
                ["01-02"] = "Drugi dan Nove godine",
                ["03-01"] = "Dan nezavisnosti Bosne i Hercegovine",
                ["05-01"] = "Praznik rada",
                ["05-02"] = "Drugi dan Praznika rada",
                ["11-25"] = "Dan državnosti Bosne i Hercegovine"
            };

            specialDays[AppLanguage.Turkish] = tr;
            specialDays[AppLanguage.English] = en;
            specialDays[AppLanguage.Bosnian] = bs;
        }

        private void ShowSpecialDayIfExists(DateTime date)
        {
            if (specialDays == null) return;
            if (!specialDays.TryGetValue(currentLanguage, out var dict)) return;

            string key = date.ToString("MM-dd", CultureInfo.InvariantCulture);

            if (dict.TryGetValue(key, out string dayName))
            {
                currentSpecialDayName_ = dayName;

                string caption = currentLanguage == AppLanguage.English ? "Special day: " + dayName
                               : currentLanguage == AppLanguage.Bosnian ? "Poseban dan: " + dayName
                               : "Özel gün: " + dayName;

                labelSpecialDay.Text = caption;
                labelSpecialDay.Visible = true;
            }
            else
            {
                currentSpecialDayName_ = null;
                labelSpecialDay.Text = "";
                labelSpecialDay.Visible = false;
            }
        }

        private class EventItem
        {
            public int Id { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text;
        }

     
        private void Form1_Load(object sender, EventArgs e) { }
        private void textBoxEvent_TextChanged(object sender, EventArgs e) { }
        private void buttonAdd_Click_1(object sender, EventArgs e) { buttonAdd_Click(sender, e); }
        private void buttonDelete_Click_1(object sender, EventArgs e) { buttonDelete_Click(sender, e); }
        private void label1_Click(object sender, EventArgs e) { }

        private void buttonAllDates_Click_(object sender, EventArgs e)
        {
            ShowAllEventDatesDialog();
        }

        private void ShowAllEventDatesDialog()
        {
            var items = new List<EventDateItem_>();

            using (var connection = new SQLiteConnection("Data Source=" + DbPath))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT EventDate, COUNT(*) " +
                        "FROM Events " +
                        "GROUP BY EventDate " +
                        "ORDER BY EventDate;";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string dateStr = reader.GetString(0);
                            int count = reader.GetInt32(1);

                            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out var dt))
                            {
                                items.Add(new EventDateItem_ { Date = dt, Count = count });
                            }
                        }
                    }
                }
            }

            if (items.Count == 0)
            {
                MessageBox.Show(messageNoEvents_ ?? "No events.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string title =
                currentLanguage == AppLanguage.English ? "All event dates" :
                currentLanguage == AppLanguage.Bosnian ? "Svi datumi događaja" :
                "Tüm etkinlik tarihleri";

            var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Size = new Size(360, 420),
                BackColor = C_BG
            };

            var lb = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                IntegralHeight = false
            };

            foreach (var it in items)
                lb.Items.Add(it);

            lb.DoubleClick += (s, e) =>
            {
                if (lb.SelectedItem is EventDateItem_ it)
                {
                    if (guna2DateTimePicker1 != null)
                        guna2DateTimePicker1.Value = it.Date;

                    dlg.Close();
                }
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(12), BackColor = Color.White };

            var btnClose_ = new Button
            {
                Text = currentLanguage == AppLanguage.English ? "Close" :
                       currentLanguage == AppLanguage.Bosnian ? "Zatvori" : "Kapat",
                Dock = DockStyle.Right,
                Width = 110
            };
            StyleButton_(btnClose_, isDanger: true);
            btnClose_.Click += (s, e) => dlg.Close();

            var hint = new Label
            {
                Text = currentLanguage == AppLanguage.English ? "Double-click a date to jump." :
                       currentLanguage == AppLanguage.Bosnian ? "Dvaput klikni datum za odlazak." :
                       "Bir tarihe gitmek için çift tıkla.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = C_MUTED,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            bottom.Controls.Add(btnClose_);
            bottom.Controls.Add(hint);

            dlg.Controls.Add(lb);
            dlg.Controls.Add(bottom);

            dlg.ShowDialog(this);
        }

        private class EventDateItem_
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }

            public override string ToString()
            {
           
                return $"{Date:yyyy-MM-dd} ({Count})";
            }
        }


    }
}
