using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MX4HapticService
{
	/// <summary>
	/// A point on the interval curve. Position and Value are normalized (0.0 to 1.0).
	/// </summary>
	public class CurvePoint
	{
		/// <summary>
		/// Position in the intensity range (0.0 = IntensityMin, 1.0 = IntensityMax).
		/// </summary>
		public Double Position { get; set; } = 0.0;

		/// <summary>
		/// Value in the interval range (0.0 = IntervalMinTenths, 1.0 = IntervalMaxTenths).
		/// </summary>
		public Double Value { get; set; } = 1.0;
	}

	/// <summary>
	/// Defines a waveform zone with its intensity range and interval settings.
	/// </summary>
	public class WaveformZone
	{
		public String Waveform { get; set; } = "subtle_collision";
		public Int32 IntensityMin { get; set; } = 0;
		public Int32 IntensityMax { get; set; } = 256;
		public Int32 IntervalMinTenths { get; set; } = 150;
		public Int32 IntervalMaxTenths { get; set; } = 2000;

		/// <summary>
		/// Curve points for interval interpolation. Position/Value are normalized 0.0-1.0.
		/// </summary>
		public List<CurvePoint> IntervalCurve { get; set; } = new List<CurvePoint>
		{
			new CurvePoint { Position = 0.0, Value = 1.0 },
			new CurvePoint { Position = 1.0, Value = 0.0 }
		};

		/// <summary>
		/// Calculates interval at a given normalized position using the curve.
		/// </summary>
		public Int32 CalculateIntervalAtPosition(Double normalizedPosition)
		{
			if (this.IntervalCurve == null || this.IntervalCurve.Count < 2)
			{
				var t = Math.Clamp(normalizedPosition, 0.0, 1.0);
				return this.IntervalMaxTenths - (Int32)((this.IntervalMaxTenths - this.IntervalMinTenths) * t);
			}

			var sortedCurve = this.IntervalCurve.OrderBy(p => p.Position).ToList();
			var pos = Math.Clamp(normalizedPosition, 0.0, 1.0);

			CurvePoint p1 = sortedCurve[0];
			CurvePoint p2 = sortedCurve[sortedCurve.Count - 1];

			for (var i = 0; i < sortedCurve.Count - 1; i++)
			{
				if (pos >= sortedCurve[i].Position && pos <= sortedCurve[i + 1].Position)
				{
					p1 = sortedCurve[i];
					p2 = sortedCurve[i + 1];
					break;
				}
			}

			Double segmentLength = p2.Position - p1.Position;
			Double normalizedValue;

			if (segmentLength <= 0)
			{
				normalizedValue = p1.Value;
			}
			else
			{
				var t = (pos - p1.Position) / segmentLength;
				normalizedValue = p1.Value + (p2.Value - p1.Value) * t;
			}

			return this.IntervalMinTenths +
				   (Int32)((this.IntervalMaxTenths - this.IntervalMinTenths) * normalizedValue);
		}
	}

	/// <summary>
	/// Configuration for haptic feedback mapping.
	/// </summary>
	public class HapticConfig
	{
		#region Legacy Fields

		[Obsolete("Use WaveformZones instead")]
		public String WaveformLight { get; set; }

		[Obsolete("Use WaveformZones instead")]
		public String WaveformMedium { get; set; }

		[Obsolete("Use WaveformZones instead")]
		public String WaveformStrong { get; set; }

		[Obsolete("Use WaveformZones instead")]
		public Int32? ThresholdLightMedium { get; set; }

		[Obsolete("Use WaveformZones instead")]
		public Int32? ThresholdMediumStrong { get; set; }

		#endregion

		#region Simple Mode (Direct HID++ Intensity)

		/// <summary>
		/// Enable simple mode: single waveform + direct HID++ intensity + frequency.
		/// </summary>
		public Boolean EnableSimpleMode { get; set; } = true;

		/// <summary>
		/// Waveform to use in simple mode.
		/// </summary>
		public String SimpleWaveform { get; set; } = "wave";

		/// <summary>
		/// Intensity scaling factor (0.5-2.0).
		/// </summary>
		public Double IntensityScale { get; set; } = 1.0;

		/// <summary>
		/// Minimum haptic level (0-100).
		/// </summary>
		public Int32 MinHapticLevel { get; set; } = 10;

		/// <summary>
		/// Maximum haptic level (0-100).
		/// </summary>
		public Int32 MaxHapticLevel { get; set; } = 100;

		/// <summary>
		/// Minimum pulse interval in ms (at max intensity).
		/// </summary>
		public Int32 SimpleIntervalMinMs { get; set; } = 15;

		/// <summary>
		/// Maximum pulse interval in ms (at min intensity).
		/// </summary>
		public Int32 SimpleIntervalMaxMs { get; set; } = 150;

		/// <summary>
		/// Use fixed interval (SimpleIntervalMinMs) instead of variable.
		/// </summary>
		public Boolean UseFixedInterval { get; set; } = false;

		#endregion

		#region PDM Mode (Advanced)

		/// <summary>
		/// Dynamic list of waveform zones (1-8 zones supported).
		/// </summary>
		public List<WaveformZone> WaveformZones { get; set; } = new List<WaveformZone>
		{
			new WaveformZone
			{
				Waveform = "subtle_collision",
				IntensityMin = 0,
				IntensityMax = 85,
				IntervalMinTenths = 200,
				IntervalMaxTenths = 3000
			},
			new WaveformZone
			{
				Waveform = "damp_collision",
				IntensityMin = 85,
				IntensityMax = 170,
				IntervalMinTenths = 150,
				IntervalMaxTenths = 2000
			},
			new WaveformZone
			{
				Waveform = "wave",
				IntensityMin = 170,
				IntensityMax = 256,
				IntervalMinTenths = 100,
				IntervalMaxTenths = 1000
			}
		};

		public Int32 PulseIntervalMs { get; set; } = 30;
		public Boolean EnableDynamicIntensity { get; set; } = true;

		#endregion

		// Legacy fields kept for backward compatibility
		public Int32 MaxIntervalTenths { get; set; } = 2000;
		public Int32 MidIntervalTenths { get; set; } = 600;
		public Int32 MinIntervalTenths { get; set; } = 150;
		public Int32 MidIntensity { get; set; } = 128;

		public static String ConfigPath => Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"MX4GameHaptics",
			"haptic-config.json");

		public const String PipeName = "MX4HapticConfig";

		public static readonly String[] AvailableWaveforms = new[]
		{
			"sharp_collision", "sharp_state_change", "knock", "damp_collision",
			"mad", "ringing", "subtle_collision", "completed", "jingle",
			"damp_state_change", "firework", "happy_alert", "wave", "angry_alert", "square"
		};

		public static HapticConfig Load()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					var json = File.ReadAllText(ConfigPath);
					var config = JsonSerializer.Deserialize<HapticConfig>(json);
					if (config != null)
					{
						config.MigrateFromLegacyFormat();
						config.EnsureCurvesExist();
						return config;
					}
				}
			}
			catch { }
			return new HapticConfig();
		}

		private void MigrateFromLegacyFormat()
		{
			if (this.WaveformZones == null || this.WaveformZones.Count == 0)
			{
				this.WaveformZones = new List<WaveformZone>();
			}

#pragma warning disable CS0618
			if (!String.IsNullOrEmpty(this.WaveformLight) &&
				this.ThresholdLightMedium.HasValue &&
				this.ThresholdMediumStrong.HasValue)
			{
				var threshold1 = this.ThresholdLightMedium.Value;
				var threshold2 = this.ThresholdMediumStrong.Value;

				this.WaveformZones = new List<WaveformZone>
				{
					new WaveformZone
					{
						Waveform = this.WaveformLight ?? "subtle_collision",
						IntensityMin = 0,
						IntensityMax = threshold1,
						IntervalMinTenths = 200,
						IntervalMaxTenths = 3000
					},
					new WaveformZone
					{
						Waveform = this.WaveformMedium ?? "damp_collision",
						IntensityMin = threshold1,
						IntensityMax = threshold2,
						IntervalMinTenths = 150,
						IntervalMaxTenths = 2000
					},
					new WaveformZone
					{
						Waveform = this.WaveformStrong ?? "wave",
						IntensityMin = threshold2,
						IntensityMax = 256,
						IntervalMinTenths = 100,
						IntervalMaxTenths = 1000
					}
				};

				this.WaveformLight = null;
				this.WaveformMedium = null;
				this.WaveformStrong = null;
				this.ThresholdLightMedium = null;
				this.ThresholdMediumStrong = null;
			}
#pragma warning restore CS0618
		}

		private void EnsureCurvesExist()
		{
			foreach (var zone in this.WaveformZones)
			{
				if (zone.IntervalCurve == null || zone.IntervalCurve.Count < 2)
				{
					zone.IntervalCurve = new List<CurvePoint>
					{
						new CurvePoint { Position = 0.0, Value = 1.0 },
						new CurvePoint { Position = 1.0, Value = 0.0 }
					};
				}
			}
		}

		public void Save()
		{
			try
			{
				var directory = Path.GetDirectoryName(ConfigPath);
				if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				var options = new JsonSerializerOptions { WriteIndented = true };
				var json = JsonSerializer.Serialize(this, options);
				File.WriteAllText(ConfigPath, json);
			}
			catch { }
		}

		public String SelectWaveform(Byte intensity)
		{
			if (this.WaveformZones == null || this.WaveformZones.Count == 0)
			{
				return "wave";
			}

			foreach (var zone in this.WaveformZones)
			{
				if (intensity >= zone.IntensityMin && intensity < zone.IntensityMax)
				{
					return zone.Waveform;
				}
			}

			return this.WaveformZones.Last().Waveform;
		}

		public Int32 CalculateDynamicInterval(Byte intensity)
		{
			if (!this.EnableDynamicIntensity || intensity == 0)
			{
				return this.PulseIntervalMs;
			}

			var zone = this.WaveformZones?.FirstOrDefault(z =>
				intensity >= z.IntensityMin && intensity < z.IntensityMax);

			if (zone == null)
			{
				zone = this.WaveformZones?.LastOrDefault();
				if (zone == null)
				{
					return this.PulseIntervalMs;
				}
			}

			var range = zone.IntensityMax - zone.IntensityMin;
			if (range <= 0)
			{
				return zone.IntervalMaxTenths;
			}

			var normalized = (intensity - zone.IntensityMin) / (Double)range;
			return zone.CalculateIntervalAtPosition(normalized);
		}

		#region Simple Mode Helpers

		/// <summary>
		/// Calculates haptic level (0-100) from motor intensity (0-255) for Simple Mode.
		/// </summary>
		public Byte CalculateSimpleHapticLevel(Byte motorIntensity)
		{
			if (motorIntensity == 0) return 0;

			var normalized = motorIntensity / 255.0 * this.IntensityScale;
			var level = this.MinHapticLevel + (this.MaxHapticLevel - this.MinHapticLevel) * normalized;
			return (Byte)Math.Clamp((Int32)level, 0, 100);
		}

		/// <summary>
		/// Calculates pulse interval in ms from motor intensity (0-255) for Simple Mode.
		/// </summary>
		public Int32 CalculateSimpleInterval(Byte motorIntensity)
		{
			// Fixed interval mode - always return SimpleIntervalMinMs
			if (this.UseFixedInterval)
			{
				return this.SimpleIntervalMinMs;
			}

			if (motorIntensity == 0) return this.SimpleIntervalMaxMs;

			var normalized = motorIntensity / 255.0;
			var interval = this.SimpleIntervalMaxMs - (this.SimpleIntervalMaxMs - this.SimpleIntervalMinMs) * normalized;
			return Math.Clamp((Int32)interval, this.SimpleIntervalMinMs, this.SimpleIntervalMaxMs);
		}

		#endregion
	}
}
