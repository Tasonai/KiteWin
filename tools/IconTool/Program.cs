using SkiaSharp;
using Svg.Skia;

// IconTool <AppIcon.svg> <assets-dir> [review.png]
//   writes <assets-dir>\AppIcon.ico (16, 20, 24, 32, 40, 48, 64, 256 px) and AppIcon.png (256 px),
//   and a review sheet: every size on the light and dark taskbar and Start colors.
//   A hand-tuned drawing for one size sits next to the SVG as AppIcon-<size>.svg (AppIcon-16.svg)
//   and replaces the scaled-down main drawing at that size.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: IconTool <AppIcon.svg> <assets-dir> [review.png]");
    return 1;
}

var svgs = new List<SKSvg>();
SKPicture? Load(string path)
{
    var svg = new SKSvg();
    svgs.Add(svg);
    return svg.Load(path);
}

if (Load(args[0]) is not { } picture)
{
    Console.Error.WriteLine($"could not read {args[0]}");
    return 1;
}

// Windows asks for these sizes: 16 to 48 for title bars, the taskbar, and Explorer at 100 to 300 percent, 256 for Start and large icons.
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
var pictures = new Dictionary<int, SKPicture>();
foreach (var size in sizes)
{
    var own = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, $"{Path.GetFileNameWithoutExtension(args[0])}-{size}.svg");
    if (!File.Exists(own)) { pictures[size] = picture; continue; }
    if (Load(own) is not { } tuned)
    {
        Console.Error.WriteLine($"could not read {own}");
        return 1;
    }
    pictures[size] = tuned;
    Console.WriteLine($"{size} px from {Path.GetFileName(own)}");
}
var pngs = sizes.Select(size => Render(pictures[size], size)).ToList();

Directory.CreateDirectory(args[1]);
WriteIco(Path.Combine(args[1], "AppIcon.ico"), sizes, pngs);
File.WriteAllBytes(Path.Combine(args[1], "AppIcon.png"), pngs[^1]);
Console.WriteLine($"wrote {Path.Combine(args[1], "AppIcon.ico")} and AppIcon.png");

if (args.Length > 2)
{
    WriteReview(args[2], pictures, sizes);
    Console.WriteLine($"wrote {args[2]}");
}
foreach (var svg in svgs) svg.Dispose();
return 0;

static byte[] Render(SKPicture picture, int size)
{
    using var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(bitmap))
    {
        canvas.Clear(SKColors.Transparent);
        var bounds = picture.CullRect;
        canvas.Scale(size / bounds.Width, size / bounds.Height);
        canvas.DrawPicture(picture);
    }
    using var image = SKImage.FromBitmap(bitmap);
    return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}

// An .ico with PNG entries: a 6 byte header, a 16 byte entry per size, then the PNGs.
static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    using var file = File.Create(path);
    using var w = new BinaryWriter(file);
    w.Write((ushort)0);
    w.Write((ushort)1);
    w.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write(pngs[i].Length);
        w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs) w.Write(png);
}

static void WriteReview(string path, Dictionary<int, SKPicture> pictures, int[] sizes)
{
    // Rows: Windows light taskbar, dark taskbar, light Start, dark Start.
    (string Name, SKColor Color)[] backgrounds =
    [
        ("light taskbar", new SKColor(0xEE, 0xF1, 0xF6)),
        ("dark taskbar", new SKColor(0x1C, 0x1C, 0x1C)),
        ("light Start", new SKColor(0xF3, 0xF3, 0xF3)),
        ("dark Start", new SKColor(0x2B, 0x2B, 0x2B)),
    ];
    const int cell = 280, label = 28;
    using var bitmap = new SKBitmap(cell * sizes.Length, (cell + label) * backgrounds.Length);
    using var canvas = new SKCanvas(bitmap);
    using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 16);
    for (var row = 0; row < backgrounds.Length; row++)
    {
        var top = row * (cell + label);
        using var fill = new SKPaint { Color = backgrounds[row].Color };
        canvas.DrawRect(0, top, bitmap.Width, cell + label, fill);
        using var text = new SKPaint { Color = row % 2 == 0 ? SKColors.Black : SKColors.White, IsAntialias = true };
        canvas.DrawText(backgrounds[row].Name, 8, top + 20, SKTextAlign.Left, font, text);
        for (var col = 0; col < sizes.Length; col++)
        {
            using var image = SKImage.FromEncodedData(Render(pictures[sizes[col]], sizes[col]));
            // Small sizes are drawn at 4x with no smoothing, so you see their real pixels.
            var shown = sizes[col] <= 64 ? sizes[col] * 4 : sizes[col];
            var x = col * cell + (cell - shown) / 2f;
            var y = top + label + (cell - shown) / 2f;
            canvas.DrawImage(image, new SKRect(x, y, x + shown, y + shown), new SKSamplingOptions(SKFilterMode.Nearest));
        }
    }
    using var output = SKImage.FromBitmap(bitmap);
    File.WriteAllBytes(path, output.Encode(SKEncodedImageFormat.Png, 100).ToArray());
}
