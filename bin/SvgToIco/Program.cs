using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Svg;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SvgToIco <input.svg> <output.ico> [size1,size2,...]");
    Console.Error.WriteLine("  Sizes default to: 16,32,48,256");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Can also convert to PNG:");
    Console.Error.WriteLine("  SvgToIco <input.svg> <output.png> [size]");
    return 1;
}

var inputPath = args[0];
var outputPath = args[1];
var outputExt = Path.GetExtension(outputPath).ToLowerInvariant();

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

var svgDoc = SvgDocument.Open(inputPath);

if (outputExt == ".png")
{
    var size = args.Length >= 3 ? int.Parse(args[2]) : 256;
    using var bitmap = RenderSvg(svgDoc, size);
    bitmap.Save(outputPath, ImageFormat.Png);
    Console.WriteLine($"Saved {size}x{size} PNG: {outputPath}");
}
else if (outputExt == ".ico")
{
    var sizes = args.Length >= 3
        ? args[2].Split(',').Select(int.Parse).ToArray()
        : new[] { 16, 32, 48, 256 };

    using var output = File.Create(outputPath);
    WriteIco(output, svgDoc, sizes);
    Console.WriteLine($"Saved ICO ({string.Join(",", sizes)}): {outputPath}");
}
else
{
    Console.Error.WriteLine($"Unsupported output format: {outputExt}");
    return 1;
}

return 0;

static Bitmap RenderSvg(SvgDocument svg, int size)
{
    svg.Width = size;
    svg.Height = size;
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    bitmap.SetResolution(96, 96);
    using var g = Graphics.FromImage(bitmap);
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    svg.Draw(g);
    return bitmap;
}

static void WriteIco(Stream output, SvgDocument svg, int[] sizes)
{
    // Render each size to PNG bytes
    var pngEntries = new List<byte[]>();
    foreach (var size in sizes)
    {
        using var bitmap = RenderSvg(svg, size);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        pngEntries.Add(ms.ToArray());
    }

    using var writer = new BinaryWriter(output);

    // ICO header: reserved(2) + type(2) + count(2)
    writer.Write((short)0);         // reserved
    writer.Write((short)1);         // type: 1 = ICO
    writer.Write((short)sizes.Length);

    // Calculate offset: header(6) + entries(16 each)
    var dataOffset = 6 + sizes.Length * 16;

    // Directory entries
    for (var i = 0; i < sizes.Length; i++)
    {
        var size = sizes[i];
        var pngData = pngEntries[i];

        writer.Write((byte)(size >= 256 ? 0 : size)); // width (0 = 256)
        writer.Write((byte)(size >= 256 ? 0 : size)); // height
        writer.Write((byte)0);     // color palette
        writer.Write((byte)0);     // reserved
        writer.Write((short)1);    // color planes
        writer.Write((short)32);   // bits per pixel
        writer.Write(pngData.Length);  // data size
        writer.Write(dataOffset);      // data offset

        dataOffset += pngData.Length;
    }

    // Image data
    foreach (var pngData in pngEntries)
    {
        writer.Write(pngData);
    }
}
