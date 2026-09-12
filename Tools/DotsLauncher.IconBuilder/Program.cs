using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

string outputDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "DotsLauncher", "Assets"));
Directory.CreateDirectory(outputDirectory);
const int size = 256;
using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
using (var canvas = Graphics.FromImage(bitmap))
{
    canvas.SmoothingMode = SmoothingMode.AntiAlias;
    canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
    canvas.Clear(Color.Transparent);

    var northWest = new[] { new Point(128, 14), new Point(14, 128), new Point(128, 128) };
    var northEast = new[] { new Point(128, 14), new Point(128, 128), new Point(242, 128) };
    var southEast = new[] { new Point(242, 128), new Point(128, 128), new Point(128, 242) };
    var southWest = new[] { new Point(128, 242), new Point(128, 128), new Point(14, 128) };

    using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
    using (var path = new GraphicsPath())
    {
        path.AddPolygon(new[] { new Point(128, 21), new Point(249, 128), new Point(128, 249), new Point(7, 128) });
        canvas.FillPath(shadow, path);
    }

    DrawFace(canvas, northWest, Color.FromArgb(239, 117, 113));
    DrawFace(canvas, northEast, Color.FromArgb(99, 145, 237));
    DrawFace(canvas, southEast, Color.FromArgb(242, 193, 83));
    DrawFace(canvas, southWest, Color.FromArgb(77, 194, 168));

    using var outline = new Pen(Color.FromArgb(22, 29, 34), 7) { LineJoin = LineJoin.Round };
    canvas.DrawPolygon(outline, northWest);
    canvas.DrawPolygon(outline, northEast);
    canvas.DrawPolygon(outline, southEast);
    canvas.DrawPolygon(outline, southWest);

    DrawPips(canvas, new[] { (91, 76), (68, 99) });
    DrawPips(canvas, new[] { (165, 76), (188, 99) });
    DrawPips(canvas, new[] { (188, 165), (165, 188) });
    DrawPips(canvas, new[] { (68, 165), (91, 188) });
}

string pngPath = Path.Combine(outputDirectory, "DotsLauncher.png");
bitmap.Save(pngPath, ImageFormat.Png);
byte[] png = File.ReadAllBytes(pngPath);
using var ico = File.Create(Path.Combine(outputDirectory, "DotsLauncher.ico"));
ico.Write(new byte[] { 0, 0, 1, 0, 1, 0 });
ico.WriteByte(0); // 256px width encoded as zero
ico.WriteByte(0); // 256px height encoded as zero
ico.WriteByte(0); // palette
ico.WriteByte(0); // reserved
ico.Write(new byte[] { 1, 0, 32, 0 }); // one plane, 32bpp
ico.Write(BitConverter.GetBytes(png.Length));
ico.Write(BitConverter.GetBytes(22));
ico.Write(png);

static void DrawFace(Graphics canvas, Point[] points, Color color)
{
    using var brush = new SolidBrush(color);
    canvas.FillPolygon(brush, points);
}

static void DrawPips(Graphics canvas, IEnumerable<(int X, int Y)> positions)
{
    using var pip = new SolidBrush(Color.FromArgb(210, 18, 27, 31));
    using var highlight = new SolidBrush(Color.FromArgb(45, 255, 255, 255));
    foreach (var (x, y) in positions)
    {
        canvas.FillEllipse(pip, x - 10, y - 10, 20, 20);
        canvas.FillEllipse(highlight, x - 6, y - 6, 5, 5);
    }
}
