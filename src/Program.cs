using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ShutdownSteam
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--public-preview")
            {
                using (var form = new DashboardForm(null, true)) form.SavePublicAssets(args[1]);
                return;
            }
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--preview")
            {
                using (var form = new DashboardForm(args.Length == 3 ? args[2] : null)) form.SavePreviews(args[1]);
                return;
            }
            Application.Run(new DashboardForm());
        }
    }

    internal class RoundedPanel : Panel
    {
        public int Radius = 14;
        public Color BorderColor = Color.FromArgb(46, 55, 68);
        public Color TopGlow = Color.FromArgb(50, 255, 255, 255);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(BackColor))
            using (var pen = new Pen(BorderColor))
            using (var glow = new Pen(TopGlow))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
                e.Graphics.DrawLine(glow, Radius, 1, Width - Radius, 1);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            using (var path = RoundedRect(new Rectangle(0, 0, Width, Height), Radius))
            {
                Region = new Region(path);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class FieldPanel : RoundedPanel
    {
        public string IconKind = "";

        public FieldPanel()
        {
            Radius = 10;
            BorderColor = Color.FromArgb(105, 105, 136, 178);
            TopGlow = Color.FromArgb(26, 255, 255, 255);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawIcon(e.Graphics, IconKind, 26, Height / 2, Color.FromArgb(160, 190, 214, 240), Color.FromArgb(116, 225, 255));
        }

        internal static void DrawIcon(Graphics g, string kind, int x, int cy, Color muted, Color accent)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(kind == "folder" || kind == "refresh" ? accent : muted, 2F))
            using (var thin = new Pen(muted, 1.7F))
            {
                if (kind == "folder")
                {
                    g.DrawRectangle(pen, x - 12, cy - 6, 24, 14);
                    g.DrawLine(pen, x - 12, cy - 6, x - 7, cy - 12);
                    g.DrawLine(pen, x - 7, cy - 12, x + 2, cy - 12);
                    g.DrawLine(pen, x + 2, cy - 12, x + 7, cy - 6);
                }
                else if (kind == "game")
                {
                    g.DrawArc(thin, x - 13, cy - 9, 26, 18, 190, 160);
                    g.DrawLine(thin, x - 10, cy + 2, x - 7, cy - 4);
                    g.DrawLine(thin, x - 4, cy + 2, x - 7, cy - 4);
                    g.DrawEllipse(thin, x + 5, cy - 4, 3, 3);
                    g.DrawEllipse(thin, x + 10, cy + 1, 3, 3);
                }
                else if (kind == "bolt")
                {
                    g.DrawLines(thin, new[] { new Point(x + 5, cy - 14), new Point(x - 8, cy + 2), new Point(x + 1, cy + 2), new Point(x - 6, cy + 15), new Point(x + 10, cy - 5), new Point(x + 1, cy - 5) });
                }
                else if (kind == "message")
                {
                    g.DrawRectangle(thin, x - 12, cy - 8, 24, 16);
                    g.DrawLine(thin, x - 4, cy + 8, x + 4, cy + 13);
                    g.DrawLine(thin, x - 6, cy - 1, x + 6, cy - 1);
                }
                else if (kind == "refresh")
                {
                    g.DrawArc(pen, x - 12, cy - 12, 24, 24, 35, 250);
                    g.DrawLine(pen, x + 12, cy - 2, x + 17, cy - 7);
                    g.DrawLine(pen, x + 12, cy - 2, x + 6, cy - 6);
                }
            }
        }
    }

    internal class ModernButton : Button
    {
        private bool hover;
        public bool PrimaryGradient;
        public string IconKind = "";
        public string NativeGlyph = "";

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var fill = Enabled ? BackColor : Color.FromArgb(38, 48, 61);
            if (hover && Enabled)
            {
                fill = ControlPaint.Light(fill, 0.18F);
            }

            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var brush = PrimaryGradient
                ? (Brush)new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, Color.FromArgb(91, 245, 190), Color.FromArgb(52, 198, 246), 12F)
                : new SolidBrush(fill))
            using (var border = new Pen(Enabled ? Color.FromArgb(88, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255)))
            using (var textBrush = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(120, 130, 142)))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
                var textRect = ClientRectangle;
                if (!string.IsNullOrEmpty(NativeGlyph))
                {
                    using (var glyphFont = new Font("Segoe MDL2 Assets", 15F))
                    {
                        var glyphBounds = string.IsNullOrEmpty(Text) ? ClientRectangle : new Rectangle(12, 0, 36, Height);
                        TextRenderer.DrawText(e.Graphics, NativeGlyph, glyphFont, glyphBounds, Enabled ? ForeColor : Color.FromArgb(120, 140, 165), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                    if (!string.IsNullOrEmpty(Text)) textRect = new Rectangle(44, 0, Width - 48, Height);
                }
                if (!string.IsNullOrEmpty(IconKind))
                {
                    FieldPanel.DrawIcon(e.Graphics, IconKind, 32, Height / 2, Color.FromArgb(210, 180, 208, 236), Color.FromArgb(116, 225, 255));
                    textRect = new Rectangle(48, 0, Width - 54, Height);
                }

                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? ForeColor : Color.FromArgb(120, 130, 142), flags);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var path = RoundedRect(new Rectangle(0, 0, Width, Height), 9))
            {
                Region = new Region(path);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SteamApp
    {
        public string AppId;
        public string Name;
        public int StateFlags;
        public string LibraryPath;
        public string ManifestPath;

        public override string ToString()
        {
            return Name + " (" + AppId + ")";
        }
    }

    internal sealed class CompletionResult
    {
        public bool Complete;
        public string Message;
    }

    internal static class SteamScanner
    {
        public static string GetSteamRoot()
        {
            var candidates = new List<string>();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            candidates.Add(value);
                        }
                    }
                }
            }
            catch
            {
            }

            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));

            foreach (var path in candidates.Where(p => !string.IsNullOrEmpty(p)))
            {
                if (Directory.Exists(Path.Combine(path, "steamapps")))
                {
                    return path;
                }
            }

            return null;
        }

        public static List<SteamApp> GetApps(string steamRoot)
        {
            var apps = new List<SteamApp>();
            foreach (var library in GetLibraries(steamRoot))
            {
                foreach (var manifestPath in Directory.GetFiles(library, "appmanifest_*.acf"))
                {
                    var data = ReadKeyValueFile(manifestPath);
                    var id = data.ContainsKey("appid") ? data["appid"] : Path.GetFileNameWithoutExtension(manifestPath).Replace("appmanifest_", "");
                    var name = data.ContainsKey("name") ? data["name"] : "App " + id;
                    var state = 0;
                    if (data.ContainsKey("StateFlags"))
                    {
                        int.TryParse(data["StateFlags"], out state);
                    }

                    apps.Add(new SteamApp { AppId = id, Name = name, StateFlags = state, LibraryPath = library, ManifestPath = manifestPath });
                }
            }

            return apps.OrderBy(a => a.Name).ToList();
        }

        public static CompletionResult CheckComplete(string steamRoot, string appId)
        {
            var app = GetApps(steamRoot).FirstOrDefault(a => a.AppId == appId);
            if (app == null)
            {
                return new CompletionResult { Complete = false, Message = "AppID " + appId + " was not found." };
            }

            var downloading = Directory.Exists(Path.Combine(app.LibraryPath, "downloading", appId));
            var complete = !downloading && ((app.StateFlags & 4) == 4);
            var message = app.Name + " | " + (downloading ? "downloading" : "no download folder") + " | state " + app.StateFlags;
            return new CompletionResult { Complete = complete, Message = message };
        }

        public static CompletionResult CheckComplete(SteamApp app)
        {
            if (app == null || string.IsNullOrEmpty(app.AppId))
            {
                return new CompletionResult { Complete = false, Message = "No app selected." };
            }

            var state = app.StateFlags;
            if (!string.IsNullOrEmpty(app.ManifestPath) && File.Exists(app.ManifestPath))
            {
                var data = ReadKeyValueFile(app.ManifestPath);
                if (data.ContainsKey("StateFlags"))
                {
                    int.TryParse(data["StateFlags"], out state);
                }
            }

            var downloading = Directory.Exists(Path.Combine(app.LibraryPath, "downloading", app.AppId));
            var complete = !downloading && ((state & 4) == 4);
            var message = app.Name + " | " + (downloading ? "downloading" : "ready") + " | state " + state;
            return new CompletionResult { Complete = complete, Message = message };
        }

        private static IEnumerable<string> GetLibraries(string steamRoot)
        {
            var libraries = new List<string>();
            var defaultLibrary = Path.Combine(steamRoot, "steamapps");
            if (Directory.Exists(defaultLibrary))
            {
                libraries.Add(defaultLibrary);
            }

            var libraryFile = Path.Combine(defaultLibrary, "libraryfolders.vdf");
            if (File.Exists(libraryFile))
            {
                foreach (var line in File.ReadAllLines(libraryFile))
                {
                    var match = Regex.Match(line, "^\\s*\"path\"\\s*\"([^\"]+)\"\\s*$");
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value.Replace(@"\\", @"\");
                        var steamApps = Path.Combine(path, "steamapps");
                        if (Directory.Exists(steamApps))
                        {
                            libraries.Add(steamApps);
                        }
                    }
                }
            }

            return libraries.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ReadKeyValueFile(string path)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var regex = new Regex("^\\s*\"([^\"]+)\"\\s*\"([^\"]*)\"\\s*$");
            foreach (var line in File.ReadAllLines(path))
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    data[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }

            return data;
        }
    }

    internal static class PowerAction
    {
        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        public static void Run(string action)
        {
            if (action == "Test message")
            {
                MessageBox.Show("Test action worked. No power action was executed.", "ShutdownSteam", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (action == "Shutdown")
            {
                Process.Start("shutdown.exe", "/s /t 60 /c \"Steam download completed. ShutdownSteam will shut down in 60 seconds.\"");
            }
            else if (action == "Restart")
            {
                Process.Start("shutdown.exe", "/r /t 60 /c \"Steam download completed. ShutdownSteam will restart in 60 seconds.\"");
            }
            else if (action == "Hibernate")
            {
                Process.Start("shutdown.exe", "/h");
            }
            else if (action == "Lock")
            {
                LockWorkStation();
            }
        }
    }
}
