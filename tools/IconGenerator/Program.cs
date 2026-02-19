using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconGenerator;

/// <summary>
/// Generates app.ico files for MX4 Game Haptics.
/// </summary>
class Program
{
	static void Main(string[] args)
	{
		var baseDir = AppDomain.CurrentDomain.BaseDirectory;
		var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));

		var serviceIcon = Path.Combine(projectRoot, "MX4HapticService", "app.ico");
		var configuratorIcon = Path.Combine(projectRoot, "HapticConfigurator", "app.ico");

		Console.WriteLine("Generating icons...");

		GenerateIcon(serviceIcon);
		Console.WriteLine($"Created: {serviceIcon}");

		GenerateIcon(configuratorIcon);
		Console.WriteLine($"Created: {configuratorIcon}");

		Console.WriteLine("Done!");
	}

	/// <summary>
	/// Generates a multi-size .ico file with haptic wave design.
	/// </summary>
	static void GenerateIcon(string outputPath)
	{
		var sizes = new[] { 16, 32, 48, 256 };

		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		// ICO header
		bw.Write((short)0);           // Reserved
		bw.Write((short)1);           // Type: ICO
		bw.Write((short)sizes.Length); // Number of images

		// Collect image data first
		var imageData = new List<byte[]>();

		foreach (var size in sizes)
		{
			var pngData = CreateIconImage(size);
			imageData.Add(pngData);
		}

		// Calculate offsets (header = 6 bytes, each directory entry = 16 bytes)
		int offset = 6 + (16 * sizes.Length);

		// Write directory entries
		for (int i = 0; i < sizes.Length; i++)
		{
			var size = sizes[i];
			var data = imageData[i];

			bw.Write((byte)(size >= 256 ? 0 : size));  // Width (0 = 256)
			bw.Write((byte)(size >= 256 ? 0 : size));  // Height (0 = 256)
			bw.Write((byte)0);                          // Color palette
			bw.Write((byte)0);                          // Reserved
			bw.Write((short)1);                         // Color planes
			bw.Write((short)32);                        // Bits per pixel
			bw.Write(data.Length);                      // Image size
			bw.Write(offset);                           // Image offset

			offset += data.Length;
		}

		// Write image data
		foreach (var data in imageData)
		{
			bw.Write(data);
		}

		// Ensure directory exists
		var dir = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}

		File.WriteAllBytes(outputPath, ms.ToArray());
	}

	/// <summary>
	/// Creates a single icon image as PNG bytes.
	/// </summary>
	static byte[] CreateIconImage(int size)
	{
		using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using var g = Graphics.FromImage(bmp);

		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.Clear(Color.Transparent);

		var accent = Color.FromArgb(0, 212, 170);  // Turquoise
		var white = Color.White;

		// Draw filled circle
		using (var brush = new SolidBrush(accent))
		{
			g.FillEllipse(brush, 1, 1, size - 2, size - 2);
		}

		// Draw wave pattern (vibration symbol)
		var penWidth = Math.Max(1.5f, size / 10f);
		using (var pen = new Pen(white, penWidth))
		{
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;

			var centerX = size / 2f;
			var centerY = size / 2f;
			var waveWidth = size / 3f;
			var waveHeight = size / 5f;

			// Draw ~~ wave pattern
			if (size >= 32)
			{
				// Larger sizes: draw proper wave
				var points = new List<PointF>();
				for (int i = 0; i <= 20; i++)
				{
					var t = i / 20f;
					var x = centerX - waveWidth + (waveWidth * 2 * t);
					var y = centerY + (float)Math.Sin(t * Math.PI * 2) * waveHeight;
					points.Add(new PointF(x, y));
				}
				g.DrawLines(pen, points.ToArray());
			}
			else
			{
				// Small sizes: simple arcs
				var arcSize = size / 3f;
				g.DrawArc(pen, centerX - arcSize, centerY - arcSize / 2, arcSize, arcSize, 180, 180);
				g.DrawArc(pen, centerX, centerY - arcSize / 2, arcSize, arcSize, 0, 180);
			}
		}

		// Convert to PNG
		using var pngStream = new MemoryStream();
		bmp.Save(pngStream, ImageFormat.Png);
		return pngStream.ToArray();
	}
}
