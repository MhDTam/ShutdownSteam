using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ShutdownSteam;

internal static class ArtworkTests
{
    private static int checks;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        checks++; Console.WriteLine("PASS: " + name);
    }
    private static void Picture(string path, int width, int height, Color color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var bitmap = new Bitmap(width, height))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(color); bitmap.Save(path, ImageFormat.Png);
        }
    }
    private static int Main(string[] args)
    {
        string root = Path.Combine(args[0], "artwork-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string cacheFolder = Path.Combine(root, "appcache", "librarycache");
            string modern = Path.Combine(cacheFolder, "730", "0123456789abcdef0123456789abcdef01234567.jpg");
            Picture(Path.Combine(cacheFolder, "730", "header.jpg"), 160, 50, Color.Red);
            Picture(modern, 32, 32, Color.Lime);
            Picture(Path.Combine(cacheFolder, "570_header.jpg"), 160, 50, Color.Blue);
            Directory.CreateDirectory(Path.Combine(cacheFolder, "400"));
            File.WriteAllText(Path.Combine(cacheFolder, "400", "broken.jpg"), "not an image");
            using (var cache = new SteamArtworkCache())
            {
                var image = cache.Get(root, "730") as Bitmap;
                Assert(image != null && image.Width == image.Height && image.GetPixel(10, 10).G > 200, "Modern cache prefers square game icon over header");
                Assert(object.ReferenceEquals(image, cache.Get(root, "730")), "Repeated lookup reuses thumbnail");
                Assert(image.Width <= 64 && image.Height <= 64, "Thumbnail memory is bounded");
                var legacy = cache.Get(root, "570");
                Assert(legacy != null && legacy.Width > legacy.Height, "Legacy cache header fallback loads");
                Assert(cache.Get(root, "999999") == null, "Missing artwork uses fallback");
                Assert(cache.Get(root, "400") == null, "Corrupt artwork uses fallback");
                Assert(cache.Get(root, "../730") == null, "Non-numeric app ID is rejected");
                Assert(cache.Get("", "730") == null, "Empty Steam root uses fallback");
                using (var file = new FileStream(modern, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Assert(file.Length > 0, "Loaded images do not lock Steam files");
                Picture(Path.Combine(cacheFolder, "999999_icon.jpg"), 32, 32, Color.Gold);
                Assert(cache.Get(root, "999999") == null, "Missing result is cached");
                cache.Clear();
                Assert(cache.Get(root, "999999") != null, "Refresh discovers newly cached artwork");
            }
            Console.WriteLine(checks + " checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
