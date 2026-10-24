using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;

namespace ShutdownSteam
{
    internal sealed class SteamArtworkCache : IDisposable
    {
        private readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public Image Get(string steamRoot, string appId)
        {
            if (string.IsNullOrWhiteSpace(steamRoot) || !Regex.IsMatch(appId ?? "", "^[0-9]+$")) return null;
            string key = steamRoot + "|" + appId;
            Bitmap cached;
            if (images.TryGetValue(key, out cached)) return cached;
            if (images.Count >= 256) Clear();
            Bitmap best = null;
            try
            {
                string cache = Path.Combine(steamRoot, "appcache", "librarycache");
                string folder = Path.Combine(cache, appId);
                var files = new List<string>();
                if (Directory.Exists(folder)) files.AddRange(Directory.GetFiles(folder));
                if (Directory.Exists(cache)) files.AddRange(Directory.GetFiles(cache, appId + "_*"));
                int bestScore = -1;
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file).ToLowerInvariant();
                    if (name.Contains("blur") || name.Contains("logo") || name.EndsWith(".json")) continue;
                    int intScoreWidth, intScoreHeight;
                    Bitmap candidate = LoadThumbnail(file, out intScoreWidth, out intScoreHeight);
                    if (candidate == null) continue;
                    int score = intScoreWidth == intScoreHeight ? 100 : name.Contains("header") ? 80 : name.Contains("capsule") ? 70 : name.Contains("600x900") ? 60 : 10;
                    if (name.Contains("icon")) score += 20;
                    if (score > bestScore) { if (best != null) best.Dispose(); best = candidate; bestScore = score; }
                    else candidate.Dispose();
                    if (score >= 100) break;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            images[key] = best;
            return best;
        }

        private static Bitmap LoadThumbnail(string file, out int width, out int height)
        {
            width = height = 0;
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var source = Image.FromStream(stream, false, true))
                {
                    width = source.Width; height = source.Height;
                    double scale = Math.Min(64.0 / width, 64.0 / height);
                    var result = new Bitmap(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
                    using (var graphics = Graphics.FromImage(result))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, 0, 0, result.Width, result.Height);
                    }
                    return result;
                }
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (OutOfMemoryException) { return null; }
        }

        public void Clear()
        {
            foreach (var bitmap in images.Values) if (bitmap != null) bitmap.Dispose();
            images.Clear();
        }
        public void Dispose() { Clear(); }
    }
}
