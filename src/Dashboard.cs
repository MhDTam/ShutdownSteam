using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ShutdownSteam
{
    internal sealed class DashboardForm : Form
    {
        private readonly Color ink = Color.FromArgb(239, 245, 255);
        private readonly Color muted = Color.FromArgb(160, 181, 207);
        private readonly Color cyan = Color.FromArgb(88, 235, 244);
        private readonly Color surface = Color.FromArgb(10, 24, 39);
        private readonly TextBox path = new TextBox();
        private readonly DarkSelector games = new DarkSelector();
        private readonly DarkSelector actions = new DarkSelector();
        private readonly Panel main = new Panel();
        private readonly Panel secondary = new Panel();
        private readonly ListBox logView = new ListBox();
        private readonly Timer poll = new Timer();
        private readonly List<string> history = new List<string>();
        private readonly Image emblem;
        private readonly Image skin;
        private readonly SteamArtworkCache artwork = new SteamArtworkCache();
        private ModernButton start, stop, browse, refresh;
        private string page = "Main", state = "Ready", detail = "Waiting to start monitoring...", footer = "Select a Steam app";
        private SteamApp selected;
        private string armedAction;
        private bool sawIncomplete;
        private bool running;
        private string progress = "-", size = "-", downloadState = "-";
        private readonly NumericUpDown interval = new NumericUpDown();
        private readonly Dictionary<Control, Rectangle> layout = new Dictionary<Control, Rectangle>();
        private readonly Dictionary<Control, float> fontSizes = new Dictionary<Control, float>();
        private bool layoutReady;
        private readonly bool demo;

        public DashboardForm(string steamOverride = null, bool demonstration = false)
        {
            demo = demonstration;
            Text = "ShutdownSteam";
            Font = new Font("Segoe UI", 10F);
            BackColor = surface;
            ForeColor = ink;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1180, 840);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Emblem.png"))
            using (var source = Image.FromStream(stream)) emblem = new Bitmap(source);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dashboard.png"))
            using (var source = Image.FromStream(stream)) skin = new Bitmap(source);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Build();
            CaptureLayout(this);
            layoutReady = true;
            poll.Interval = 15000;
            poll.Tick += delegate { CheckDownload(); };
            path.Text = demo ? @"C:\Program Files (x86)\Steam" : steamOverride ?? SteamScanner.GetSteamRoot() ?? "";
            LoadGames();
            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Y < 72 && e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
            };
            Shown += delegate
            {
                var work = Screen.FromControl(this).WorkingArea;
                if (Height > work.Height || Width > work.Width)
                {
                    float factor = Math.Min((work.Height - 20F) / Height, (work.Width - 20F) / Width);
                    ClientSize = new Size((int)(1180 * factor), (int)(840 * factor));
                    Location = new Point(work.X + (work.Width - Width) / 2, work.Y + (work.Height - Height) / 2);
                }
            };
        }

        private void Build()
        {
            ButtonAt(this, "", 994, 18, 57, 42, delegate { WindowState = FormWindowState.Minimized; }).NativeGlyph = "\uE921";
            ButtonAt(this, "", 1108, 18, 57, 42, delegate { Close(); }).NativeGlyph = "\uE8BB";
            var fit = ButtonAt(this, "", 1051, 18, 57, 42, delegate
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                Invalidate();
            });
            fit.NativeGlyph = "\uE922";
            var tooltip = new ToolTip(); tooltip.SetToolTip(fit, "Maximize / restore");
            string[] pages = { "Main", "Settings", "Logs", "About" };
            string[] glyphs = { "\uE768", "\uE713", "\uE8A5", "\uE946" };
            for (int i = 0; i < pages.Length; i++)
            {
                string destination = pages[i];
                var nav = ButtonAt(this, destination, 17, 104 + i * 61, 160, 57, delegate { SelectPage(destination); });
                nav.Tag = destination;
                nav.NativeGlyph = glyphs[i];
            }
            main.Bounds = new Rectangle(201, 315, 688, 500);
            main.BackColor = surface;
            main.Paint += PaintMain;
            Controls.Add(main);
            path.Bounds = new Rectangle(92, 94, 390, 25);
            path.BorderStyle = BorderStyle.None; path.BackColor = surface; path.ForeColor = ink;
            path.Font = new Font("Segoe UI", 11F); main.Controls.Add(path);
            browse = ButtonAt(main, "Browse", 519, 82, 149, 42, delegate
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = path.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) { path.Text = dialog.SelectedPath; LoadGames(); }
                }
            });
            browse.IconKind = "folder";
            SetupCombo(games, main, new Rectangle(94, 220, 388, 28));
            games.ImageForItem = delegate(object item)
            {
                var app = item as SteamApp;
                return demo || app == null ? null : artwork.Get(path.Text.Trim(), app.AppId);
            };
            games.SelectedIndexChanged += delegate { ResetInfo(); main.Invalidate(); Invalidate(); };
            refresh = ButtonAt(main, "Refresh", 519, 211, 149, 44, delegate { LoadGames(); });
            refresh.IconKind = "refresh";
            SetupCombo(actions, main, new Rectangle(101, 348, 543, 28));
            actions.Items.AddRange(new object[] { "Test message", "Shutdown", "Restart", "Hibernate", "Lock" });
            actions.SelectedIndex = 0;
            actions.SelectedIndexChanged += delegate { Invalidate(); };
            start = ButtonAt(main, "Start Monitoring", 23, 402, 224, 79, delegate { Start(); });
            start.PrimaryGradient = true; start.ForeColor = Color.FromArgb(3, 24, 45);
            start.NativeGlyph = "\uE768";
            stop = ButtonAt(main, "Stop", 260, 400, 145, 81, delegate { Stop("Monitoring stopped."); });
            stop.Enabled = false;
            stop.NativeGlyph = "\uE71A";
            secondary.Bounds = new Rectangle(200, 94, 960, 716);
            secondary.BackColor = surface; secondary.Visible = false;
            Controls.Add(secondary);
            SelectPage("Main");
        }

        private ModernButton ButtonAt(Control owner, string caption, int x, int y, int w, int h, Action action)
        {
            var button = new ReferenceButton();
            button.Bounds = new Rectangle(x, y, w, h); button.Text = caption;
            button.BackColor = Color.FromArgb(20, 47, 78); button.ForeColor = ink;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false; button.Cursor = Cursors.Hand;
            if (owner == this || owner == main)
            {
                button.Art = skin;
                int ox = owner == main ? 201 : 0, oy = owner == main ? 315 : 0;
                button.ArtBounds = new RectangleF((x + ox) * skin.Width / 1180F, (y + oy) * skin.Height / 840F, w * skin.Width / 1180F, h * skin.Height / 840F);
                button.AccessibleName = caption;
            }
            button.Click += delegate { action(); }; owner.Controls.Add(button); return button;
        }

        private void SetupCombo(DarkSelector box, Control owner, Rectangle bounds)
        {
            box.Bounds = bounds; box.BackColor = surface; box.ForeColor = ink;
            box.Font = new Font("Segoe UI", 11F);
            owner.Controls.Add(box);
        }

        private void SelectPage(string name)
        {
            page = name; main.Visible = name == "Main"; secondary.Visible = name != "Main";
            foreach (Control control in Controls)
                if (control.Tag is string) control.BackColor = (string)control.Tag == name ? Color.FromArgb(24, 77, 126) : Color.FromArgb(12, 28, 45);
            foreach (ReferenceButton button in Controls.OfType<ReferenceButton>())
                if (button.Tag is string) { button.UseArt = name == "Main"; button.Invalidate(); }
            // Keep the log view alive while replacing the other page controls.
            secondary.Controls.Remove(logView);
            foreach (Control control in secondary.Controls.Cast<Control>().ToArray()) { secondary.Controls.Remove(control); if (control != interval) control.Dispose(); }
            if (name != "Main")
            {
                LabelAt(secondary, name, 30, 25, 860, 45, 24, ink);
                if (name == "Settings")
                {
                    LabelAt(secondary, "Monitoring interval (seconds)", 32, 100, 450, 32, 12, muted);
                    interval.Bounds = new Rectangle(34, 145, 130, 35); interval.Minimum = 5; interval.Maximum = 120;
                    interval.Value = poll.Interval / 1000;
                    interval.BackColor = surface; interval.ForeColor = ink; interval.Enabled = !running;
                    interval.ValueChanged -= IntervalChanged; interval.ValueChanged += IntervalChanged;
                    secondary.Controls.Add(interval);
                    ButtonAt(secondary, "Test notification", 32, 215, 210, 48, delegate { PowerAction.Run("Test message"); AddLog("Test notification displayed."); });
                    ButtonAt(secondary, "Cancel scheduled shutdown", 32, 285, 270, 48, delegate
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/a") { CreateNoWindow = true, UseShellExecute = false }); AddLog("Requested cancellation of scheduled shutdown."); }
                        catch (Exception ex) { AddLog(ex.Message); }
                    });
                }
                if (name == "Logs")
                {
                    logView.Bounds = new Rectangle(32, 95, 890, 520); logView.BackColor = surface; logView.ForeColor = ink;
                    logView.BorderStyle = BorderStyle.None; logView.HorizontalScrollbar = true;
                    logView.Items.Clear(); logView.Items.AddRange(history.Cast<object>().ToArray()); secondary.Controls.Add(logView);
                    ButtonAt(secondary, "Clear logs", 32, 640, 140, 44, delegate { history.Clear(); logView.Items.Clear(); });
                }
                if (name == "About")
                {
                    var picture = new PictureBox { Bounds = new Rectangle(32, 100, 225, 225), Image = emblem, SizeMode = PictureBoxSizeMode.Zoom };
                    secondary.Controls.Add(picture);
                    LabelAt(secondary, "ShutdownSteam", 290, 120, 550, 50, 24, ink);
                    LabelAt(secondary, "Portable Steam download watcher", 290, 184, 550, 35, 13, cyan);
                    LabelAt(secondary, "Shutdown, restart, hibernate, lock or show a notification\nwhen the selected download finishes.", 290, 240, 580, 80, 12, muted);
                    LabelAt(secondary, "Copyright (c) 2026 MhDTam. Source code licensed under MIT.", 290, 340, 590, 60, 10, muted);
                }
            }
            if (layoutReady) { CaptureLayout(secondary); ApplyLayout(); }
            Invalidate();
        }

        private void CaptureLayout(Control owner)
        {
            foreach (Control control in owner.Controls)
            {
                if (!layout.ContainsKey(control)) { layout[control] = control.Bounds; fontSizes[control] = control.Font.Size; }
                CaptureLayout(control);
            }
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (layoutReady && WindowState != FormWindowState.Minimized) ApplyLayout();
        }
        private void ApplyLayout()
        {
            float sx = ClientSize.Width / 1180F, sy = ClientSize.Height / 840F;
            foreach (var entry in layout.ToArray())
            {
                var control = entry.Key;
                if (control.IsDisposed) { layout.Remove(control); fontSizes.Remove(control); continue; }
                var r = entry.Value;
                control.Bounds = new Rectangle((int)(r.X * sx), (int)(r.Y * sy), (int)(r.Width * sx), (int)(r.Height * sy));
                float size = fontSizes[control] * Math.Min(sx, sy);
                if (Math.Abs(control.Font.Size - size) > .1F) control.Font = new Font(control.Font.FontFamily, size, control.Font.Style);
            }
            main.Invalidate(); Invalidate();
        }

        private void IntervalChanged(object sender, EventArgs e) { poll.Interval = (int)interval.Value * 1000; }
        private void LabelAt(Control owner, string value, int x, int y, int w, int h, float fontSize, Color color)
        {
            owner.Controls.Add(new Label { Text = value, Bounds = new Rectangle(x, y, w, h), ForeColor = color, BackColor = surface, Font = new Font("Segoe UI", fontSize), AutoEllipsis = true });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(ClientSize.Width / 1180F, ClientSize.Height / 840F);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(skin, new Rectangle(0, 0, 1180, 840));
            // The supplied artwork is the chrome; live fields and status are rendered separately.
            RestoreLogoBackground(g, new Rectangle(29, 9, 65, 56), 96);
            g.DrawImage(emblem, new Rectangle(38, 14, 43, 45));
            if (page != "Main") return;
            RestoreLogoBackground(g, new Rectangle(220, 111, 105, 101), 326);
            g.DrawImage(emblem, new Rectangle(231, 120, 83, 86));
            FillLive(g, new Rectangle(992, 375, 151, 63));
            Words(g, state, 998, 376, 15, cyan, true);
            TextIn(g, detail, new Rectangle(998, 406, 145, 28), 8, muted);
            FillLive(g, new Rectangle(1040, 522, 99, 110));
            string[] values = { downloadState, progress, "-", "-", size };
            for (int i = 0; i < values.Length; i++) TextIn(g, values[i], new Rectangle(1053, 526 + i * 22, 85, 22), 9, ink);
            FillLive(g, new Rectangle(932, 738, 210, 27));
            Words(g, running ? armedAction : actions.Text, 937, 739, 12, ink, true);
        }

        private void PaintMain(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(main.Width / 688F, main.Height / 500F);
            g.DrawImage(skin, new Rectangle(0, 0, 688, 500), new RectangleF(201 * skin.Width / 1180F, 315 * skin.Height / 840F, 688 * skin.Width / 1180F, 500 * skin.Height / 840F), GraphicsUnit.Pixel);
            // Replace every example field value from the mockup with working controls.
            using (var brush = new SolidBrush(surface))
            {
                g.FillRectangle(brush, 86, 88, 397, 30);
                g.FillRectangle(brush, 37, 216, 461, 34);
                g.FillRectangle(brush, 96, 343, 549, 32);
            }
            var gameArt = games.SelectedItem == null ? null : games.ImageForItem(games.SelectedItem);
            if (gameArt != null) DarkSelector.DrawThumbnail(g, gameArt, new Rectangle(38, 217, 44, 31));
            else using (var glyphFont = new Font("Segoe MDL2 Assets", 18F))
                TextRenderer.DrawText(g, "\uE7FC", glyphFont, new Rectangle(40, 218, 43, 30), Color.FromArgb(152, 206, 238), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            FillLive(g, new Rectangle(498, 418, 169, 48), true);
            TextIn(g, footer, new Rectangle(504, 424, 158, 20), 8, ink);
            TextIn(g, running ? "Monitoring selected app" : "Ready to monitor selected app.", new Rectangle(504, 448, 158, 19), 8, muted);
        }

        private void FillLive(Graphics g, Rectangle rect, bool insideMain = false)
        {
            // Stretch a text-free vertical strip from the same panel to retain its lighting.
            float sampleX = insideMain ? 866 : 1147;
            float sampleY = rect.Y + (insideMain ? 315 : 0);
            g.DrawImage(skin, rect, new RectangleF(sampleX * skin.Width / 1180F, sampleY * skin.Height / 840F, 2 * skin.Width / 1180F, rect.Height * skin.Height / 840F), GraphicsUnit.Pixel);
        }

        private void RestoreLogoBackground(Graphics g, Rectangle rect, float sampleX)
        {
            g.DrawImage(skin, rect, new RectangleF(sampleX * skin.Width / 1180F, rect.Y * skin.Height / 840F, 2 * skin.Width / 1180F, rect.Height * skin.Height / 840F), GraphicsUnit.Pixel);
        }

        private void Feature(Graphics g, string title, string subtitle, int x, string icon)
        {
            FieldPanel.DrawIcon(g, icon, x + 16, 244, muted, cyan);
            Words(g, title, x + 42, 227, 9, ink, true); Words(g, subtitle, x + 42, 250, 8, muted, false);
        }
        private static GraphicsPath Shape(Rectangle r, int radius)
        {
            int d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        private static void Box(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            using (var p = Shape(r, radius))
            using (var b = new LinearGradientBrush(r, fill, ControlPaint.Light(fill, .06F), 30F))
            using (var pen = new Pen(border)) { g.FillPath(b, p); g.DrawPath(pen, p); }
        }
        private static void Words(Graphics g, string value, int x, int y, float size, Color color, bool bold)
        {
            using (var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(color)) g.DrawString(value ?? "", font, brush, x, y);
        }
        private static void TextIn(Graphics g, string value, Rectangle bounds, float size, Color color)
        {
            using (var font = new Font("Segoe UI", size))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(value ?? "", font, brush, bounds, format);
        }

        private void LoadGames()
        {
            artwork.Clear();
            if (demo)
            {
                games.Items.Clear();
                games.Items.Add(new SteamApp { AppId = "1000", Name = "Example Game" });
                games.SelectedIndex = 0;
                footer = "Demo library";
                main.Invalidate(); Invalidate();
                return;
            }
            try
            {
                var oldId = games.SelectedItem as SteamApp; games.Items.Clear();
                if (!Directory.Exists(Path.Combine(path.Text.Trim(), "steamapps"))) { footer = "Choose your Steam folder"; return; }
                foreach (var app in SteamScanner.GetApps(path.Text.Trim())) games.Items.Add(app);
                games.SelectedItem = games.Items.Cast<SteamApp>().FirstOrDefault(a => oldId != null && a.AppId == oldId.AppId);
                if (games.SelectedIndex < 0 && games.Items.Count > 0) games.SelectedIndex = 0;
                footer = "Found " + games.Items.Count + " Steam apps."; AddLog(footer);
            }
            catch (Exception ex) { footer = "Could not read Steam library"; AddLog(ex.Message); }
            finally { main.Invalidate(); Invalidate(); }
        }
        private void Start()
        {
            selected = games.SelectedItem as SteamApp;
            if (selected == null) { MessageBox.Show(this, "Choose a Steam app first.", Text); return; }
            armedAction = actions.Text; sawIncomplete = false; running = true;
            start.Enabled = false; stop.Enabled = true; games.Enabled = actions.Enabled = path.Enabled = browse.Enabled = refresh.Enabled = false;
            state = "Monitoring"; AddLog("Monitoring " + selected.Name + "; action: " + armedAction); poll.Start(); CheckDownload();
        }
        private void Stop(string message)
        {
            poll.Stop(); running = false; start.Enabled = true; stop.Enabled = false;
            games.Enabled = actions.Enabled = path.Enabled = browse.Enabled = refresh.Enabled = true;
            state = "Ready"; detail = message; footer = message; AddLog(message); main.Invalidate(); Invalidate();
        }
        private void ResetInfo() { progress = size = downloadState = "-"; }
        private void CheckDownload()
        {
            if (!running) return;
            try
            {
                // Missing or partially written manifests are unknown, never completion evidence.
                if (!File.Exists(selected.ManifestPath)) { detail = "Waiting for Steam manifest"; Invalidate(); return; }
                var manifest = File.ReadAllText(selected.ManifestPath);
                Func<string, long> number = key => { var m = Regex.Match(manifest, "\"" + key + "\"\\s+\"(\\d+)\""); long v; return m.Success && long.TryParse(m.Groups[1].Value, out v) ? v : -1; };
                long flags = number("StateFlags"); if (flags < 0) return;
                long total = number("BytesToDownload"), done = number("BytesDownloaded");
                size = total > 0 ? (total / 1073741824.0).ToString("0.00") + " GB" : "-";
                progress = total > 0 && done >= 0 ? Math.Min(100, done * 100.0 / total).ToString("0.0") + "%" : "-";
                bool complete = flags == 4 && !Directory.Exists(Path.Combine(selected.LibraryPath, "downloading", selected.AppId));
                downloadState = complete ? "Installed" : "Pending";
                if (!complete) sawIncomplete = true;
                detail = complete ? "Waiting for a new download" : "Watching download / install";
                footer = "Last check " + DateTime.Now.ToString("HH:mm:ss"); main.Invalidate(); Invalidate();
                if (complete && sawIncomplete) { Stop("Download completed."); PowerAction.Run(armedAction); }
            }
            catch (IOException) { detail = "Steam is updating its files"; Invalidate(); }
            catch (Exception ex) { Stop("Monitoring stopped: read error."); AddLog(ex.Message); }
        }
        private void AddLog(string message) { history.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message); if (history.Count > 250) history.RemoveAt(0); }
        internal void SavePreviews(string directory)
        {
            Directory.CreateDirectory(directory);
            Show();
            Application.DoEvents();
            foreach (string destination in new[] { "Main", "Settings", "Logs", "About" })
            {
                SelectPage(destination);
                Application.DoEvents();
                using (var bitmap = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bitmap, ClientRectangle);
                    bitmap.Save(Path.Combine(directory, destination + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            SelectPage("Main");
            ClientSize = new Size(1000, 712);
            Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height))
            {
                DrawToBitmap(bitmap, ClientRectangle);
                bitmap.Save(Path.Combine(directory, "Main-Compact.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            if (games.SelectedItem != null)
            {
                Start();
                if (!running || actions.Enabled || games.Enabled || !stop.Enabled) throw new InvalidOperationException("Monitoring controls did not lock correctly.");
                Stop("Preview check passed.");
                if (!actions.Enabled || !games.Enabled || stop.Enabled) throw new InvalidOperationException("Monitoring controls did not restore correctly.");
            }
        }
        internal void SavePublicAssets(string directory)
        {
            if (!demo) throw new InvalidOperationException("Public screenshots require demonstration data.");
            Directory.CreateDirectory(directory);
            Show(); ClientSize = new Size(1180, 840); SelectPage("Main"); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height))
            {
                DrawToBitmap(bitmap, ClientRectangle);
                bitmap.Save(Path.Combine(directory, "Dashboard.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            emblem.Save(Path.Combine(directory, "Emblem-Public.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { poll.Dispose(); artwork.Dispose(); emblem.Dispose(); skin.Dispose(); } base.Dispose(disposing);
        }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    }

    internal sealed class ReferenceButton : ModernButton
    {
        public Image Art;
        public RectangleF ArtBounds;
        public bool UseArt = true;
        private bool hot;
        protected override void OnMouseEnter(EventArgs e) { hot = true; base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Art == null || !UseArt) { base.OnPaint(e); return; }
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(Art, ClientRectangle, ArtBounds, GraphicsUnit.Pixel);
            if (!Enabled && Text != "Stop") using (var veil = new SolidBrush(Color.FromArgb(120, 8, 18, 30))) e.Graphics.FillRectangle(veil, ClientRectangle);
            if (hot && Enabled) using (var light = new SolidBrush(Color.FromArgb(25, 106, 229, 255))) e.Graphics.FillRectangle(light, ClientRectangle);
            if (Text == "Stop" && Enabled)
            {
                using (var brush = new SolidBrush(Color.FromArgb(23, 48, 73))) e.Graphics.FillRectangle(brush, 55, 22, Width - 65, Height - 40);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(55, 0, Width - 65, Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
        }
    }

    internal sealed class DarkSelector : Control
    {
        public readonly List<object> Items = new List<object>();
        public Func<object, Image> ImageForItem;
        private int index = -1;
        public event EventHandler SelectedIndexChanged;
        public DarkSelector()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ComboBox;
        }
        public int SelectedIndex
        {
            get { return index; }
            set { index = value >= 0 && value < Items.Count ? value : -1; Text = SelectedItem == null ? "" : SelectedItem.ToString(); Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        public object SelectedItem
        {
            get { return index >= 0 && index < Items.Count ? Items[index] : null; }
            set { SelectedIndex = Items.IndexOf(value); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(2, 0, Width - 30, Height), Enabled ? ForeColor : Color.FromArgb(121, 150, 175), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(ForeColor, 1.7F)) e.Graphics.DrawLines(pen, new[] { new Point(Width - 20, Height / 2 - 3), new Point(Width - 14, Height / 2 + 3), new Point(Width - 8, Height / 2 - 3) });
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Open(); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up) { SelectedIndex = Math.Max(0, index - 1); e.Handled = true; }
            if (e.KeyCode == Keys.Down) { SelectedIndex = Math.Min(Items.Count - 1, index + 1); e.Handled = true; }
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.F4) { Open(); e.Handled = true; }
        }
        private void Open()
        {
            if (!Enabled || Items.Count == 0) return;
            var list = new ListBox { Font = Font, BackColor = BackColor, ForeColor = ForeColor, BorderStyle = BorderStyle.None, IntegralHeight = false, Width = Width, Height = Math.Min(320, Items.Count * 31 + 4), ItemHeight = 31, DrawMode = DrawMode.OwnerDrawFixed };
            list.Items.AddRange(Items.ToArray()); list.SelectedIndex = index;
            list.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(28, 82, 125) : BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0)
                {
                    var textBounds = e.Bounds;
                    if (ImageForItem != null)
                    {
                        Image art = ImageForItem(Items[e.Index]);
                        var iconBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 2, 28, 27);
                        if (art != null) DrawThumbnail(e.Graphics, art, iconBounds);
                        else using (var iconFont = new Font("Segoe MDL2 Assets", 13F)) TextRenderer.DrawText(e.Graphics, "\uE7FC", iconFont, iconBounds, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        textBounds.X += 40; textBounds.Width -= 40;
                    }
                    TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, textBounds, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            };
            var popup = new ToolStripDropDown { Padding = Padding.Empty, BackColor = BackColor };
            popup.Items.Add(new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size });
            Action commit = delegate { if (list.SelectedIndex >= 0) SelectedIndex = list.SelectedIndex; popup.Close(); Focus(); };
            list.MouseClick += delegate { commit(); };
            list.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) commit(); if (e.KeyCode == Keys.Escape) popup.Close(); };
            popup.Closed += delegate { popup.Dispose(); };
            popup.Show(this, new Point(0, Height + 6)); list.Focus();
        }
        internal static void DrawThumbnail(Graphics graphics, Image image, Rectangle bounds)
        {
            float scale = Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
            int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height));
        }
    }
}
