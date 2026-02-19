namespace Loupedeck.LogiHapticPlugin
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;

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
	/// Each zone has its own interval curve that maps across its intensity range.
	/// </summary>
	public class WaveformZone
	{
		/// <summary>
		/// Waveform name to use in this zone.
		/// </summary>
		public String Waveform { get; set; } = "subtle_collision";

		/// <summary>
		/// Start of intensity range (inclusive, 0-255).
		/// </summary>
		public Int32 IntensityMin { get; set; } = 0;

		/// <summary>
		/// End of intensity range (exclusive, 0-256).
		/// </summary>
		public Int32 IntensityMax { get; set; } = 256;

		/// <summary>
		/// Minimum interval in 0.1ms units (used at IntensityMax - fast pulses).
		/// Hardware limit: LRA needs ~10-15ms to actuate.
		/// Default: 150 = 15ms
		/// </summary>
		public Int32 IntervalMinTenths { get; set; } = 150;

		/// <summary>
		/// Maximum interval in 0.1ms units (used at IntensityMin - slow pulses).
		/// Perception limit: >200ms feels like separate clicks.
		/// Default: 2000 = 200ms
		/// </summary>
		public Int32 IntervalMaxTenths { get; set; } = 2000;

		/// <summary>
		/// Curve points for interval interpolation. Position/Value are normalized 0.0-1.0.
		/// Default is linear: (0,1) to (1,0) - from max interval to min interval.
		/// Add more points for custom curves. Points should be sorted by Position.
		/// </summary>
		public List<CurvePoint> IntervalCurve { get; set; } = new List<CurvePoint>
		{
			new CurvePoint { Position = 0.0, Value = 1.0 },
			new CurvePoint { Position = 1.0, Value = 0.0 }
		};

		/// <summary>
		/// Calculates interval at a given normalized position using the curve.
		/// </summary>
		/// <param name="normalizedPosition">Position in zone (0.0 to 1.0).</param>
		/// <returns>Interval in 0.1ms units.</returns>
		public Int32 CalculateIntervalAtPosition(Double normalizedPosition)
		{
			if (this.IntervalCurve == null || this.IntervalCurve.Count < 2)
			{
				// Fallback to linear interpolation
				var t = Math.Clamp(normalizedPosition, 0.0, 1.0);
				return this.IntervalMaxTenths - (Int32)((this.IntervalMaxTenths - this.IntervalMinTenths) * t);
			}

			// Ensure curve is sorted by position
			var sortedCurve = this.IntervalCurve.OrderBy(p => p.Position).ToList();

			// Clamp position
			var pos = Math.Clamp(normalizedPosition, 0.0, 1.0);

			// Find the two points to interpolate between
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

			// Linear interpolation between the two points
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

			// Convert normalized value to actual interval
			return this.IntervalMinTenths +
				   (Int32)((this.IntervalMaxTenths - this.IntervalMinTenths) * normalizedValue);
		}
	}

	/// <summary>
	/// Configuration for haptic feedback mapping.
	/// Stored in %APPDATA%\MX4GameHaptics\haptic-config.json
	/// </summary>
	public class HapticConfig
	{
		#region Legacy Fields (Backward Compatibility)

		/// <summary>
		/// Legacy: Waveform to use for light intensity. Migrated to WaveformZones.
		/// </summary>
		[Obsolete("Use WaveformZones instead")]
		public String WaveformLight { get; set; }

		/// <summary>
		/// Legacy: Waveform to use for medium intensity. Migrated to WaveformZones.
		/// </summary>
		[Obsolete("Use WaveformZones instead")]
		public String WaveformMedium { get; set; }

		/// <summary>
		/// Legacy: Waveform to use for strong intensity. Migrated to WaveformZones.
		/// </summary>
		[Obsolete("Use WaveformZones instead")]
		public String WaveformStrong { get; set; }

		/// <summary>
		/// Legacy: Threshold between light and medium intensity. Migrated to WaveformZones.
		/// </summary>
		[Obsolete("Use WaveformZones instead")]
		public Int32? ThresholdLightMedium { get; set; }

		/// <summary>
		/// Legacy: Threshold between medium and strong intensity. Migrated to WaveformZones.
		/// </summary>
		[Obsolete("Use WaveformZones instead")]
		public Int32? ThresholdMediumStrong { get; set; }

		#endregion

		#region Waveform Zones (New System)

		/// <summary>
		/// Dynamic list of waveform zones (1-8 zones supported).
		/// Each zone defines a waveform, its intensity range, and its own interval curve.
		/// </summary>
		public List<WaveformZone> WaveformZones { get; set; } = new List<WaveformZone>
		{
			new WaveformZone
			{
				Waveform = "subtle_collision",
				IntensityMin = 0,
				IntensityMax = 85,
				IntervalMinTenths = 200,   // 20ms at intensity 85
				IntervalMaxTenths = 3000   // 300ms at intensity 0
			},
			new WaveformZone
			{
				Waveform = "damp_collision",
				IntensityMin = 85,
				IntensityMax = 170,
				IntervalMinTenths = 150,   // 15ms at intensity 170
				IntervalMaxTenths = 2000   // 200ms at intensity 85
			},
			new WaveformZone
			{
				Waveform = "wave",
				IntensityMin = 170,
				IntensityMax = 256,
				IntervalMinTenths = 100,   // 10ms at intensity 255
				IntervalMaxTenths = 1000   // 100ms at intensity 170
			}
		};

		#endregion

		#region Simple Mode (Direct HID++ Intensity)

		/// <summary>
		/// Enable simple mode: single waveform + direct HID++ intensity + frequency.
		/// When enabled, uses direct intensity control instead of PDM waveform switching.
		/// </summary>
		public Boolean EnableSimpleMode { get; set; } = true;

		/// <summary>
		/// Waveform to use in simple mode. Recommended: "wave", "knock", "damp_collision".
		/// </summary>
		public String SimpleWaveform { get; set; } = "wave";

		/// <summary>
		/// Intensity scaling factor (0.5-2.0). Higher = stronger haptics.
		/// </summary>
		public Double IntensityScale { get; set; } = 1.0;

		/// <summary>
		/// Minimum haptic level (0-100). Applied when motor intensity > 0.
		/// </summary>
		public Int32 MinHapticLevel { get; set; } = 10;

		/// <summary>
		/// Maximum haptic level (0-100). Applied at max motor intensity.
		/// </summary>
		public Int32 MaxHapticLevel { get; set; } = 100;

		/// <summary>
		/// Minimum pulse interval in ms (at max intensity - fast pulses).
		/// </summary>
		public Int32 SimpleIntervalMinMs { get; set; } = 15;

		/// <summary>
		/// Maximum pulse interval in ms (at min intensity - slow pulses).
		/// </summary>
		public Int32 SimpleIntervalMaxMs { get; set; } = 150;

		/// <summary>
		/// Use fixed interval (SimpleIntervalMinMs) instead of variable.
		/// </summary>
		public Boolean UseFixedInterval { get; set; } = false;

		#endregion

		#region PDM Settings (Legacy/Advanced)

		/// <summary>
		/// Pulse interval in 0.1ms units (legacy, used when dynamic intensity is disabled).
		/// </summary>
		public Int32 PulseIntervalMs { get; set; } = 30;

		/// <summary>
		/// Enable dynamic intensity via Pulse Density Modulation.
		/// When enabled, pulse interval varies based on motor intensity.
		/// </summary>
		public Boolean EnableDynamicIntensity { get; set; } = true;

		/// <summary>
		/// Maximum interval in 0.1ms units (used at intensity 0).
		/// Perception limit: >200ms feels like separate clicks.
		/// Default: 2000 = 200ms = 5 pulses/sec (barely connected)
		/// </summary>
		public Int32 MaxIntervalTenths { get; set; } = 2000;

		/// <summary>
		/// Mid interval in 0.1ms units (used at MidIntensity point).
		/// Inflection point of the 3-point curve.
		/// Default: 600 = 60ms
		/// </summary>
		public Int32 MidIntervalTenths { get; set; } = 600;

		/// <summary>
		/// Minimum interval in 0.1ms units (used at intensity 255).
		/// Hardware limit: LRA needs ~10-15ms to actuate.
		/// Default: 150 = 15ms = 66 pulses/sec (near flutter threshold)
		/// </summary>
		public Int32 MinIntervalTenths { get; set; } = 150;

		/// <summary>
		/// Intensity value at which MidIntervalTenths is applied.
		/// This is the inflection point of the 3-point interval curve.
		/// Default: 128 (middle of 0-255 range)
		/// </summary>
		public Int32 MidIntensity { get; set; } = 128;

		#endregion

		/// <summary>
		/// Path to the configuration file.
		/// </summary>
		public static String ConfigPath => Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"MX4GameHaptics",
			"haptic-config.json");

		/// <summary>
		/// Named pipe name for communication between configurator and plugin.
		/// </summary>
		public const String PipeName = "MX4HapticConfig";

		/// <summary>
		/// Available waveforms.
		/// </summary>
		public static readonly String[] AvailableWaveforms = new[]
		{
			"sharp_collision",
			"sharp_state_change",
			"knock",
			"damp_collision",
			"mad",
			"ringing",
			"subtle_collision",
			"completed",
			"jingle",
			"damp_state_change",
			"firework",
			"happy_alert",
			"wave",
			"angry_alert",
			"square"
		};

		/// <summary>
		/// Loads configuration from file, or returns defaults if not found.
		/// Handles migration from legacy format to new WaveformZones format.
		/// </summary>
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
						return config;
					}
				}
			}
			catch
			{
				// Return defaults on any error
			}
			return new HapticConfig();
		}

		/// <summary>
		/// Migrates legacy 3-waveform format to new WaveformZones list.
		/// </summary>
		private void MigrateFromLegacyFormat()
		{
			// Check if we have legacy data and no WaveformZones
			if (this.WaveformZones == null || this.WaveformZones.Count == 0)
			{
				this.WaveformZones = new List<WaveformZone>();
			}

			// If legacy fields are set and WaveformZones is default, migrate
#pragma warning disable CS0618 // Obsolete warning
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
						IntensityMax = threshold1
					},
					new WaveformZone
					{
						Waveform = this.WaveformMedium ?? "damp_collision",
						IntensityMin = threshold1,
						IntensityMax = threshold2
					},
					new WaveformZone
					{
						Waveform = this.WaveformStrong ?? "wave",
						IntensityMin = threshold2,
						IntensityMax = 256
					}
				};

				// Clear legacy fields after migration
				this.WaveformLight = null;
				this.WaveformMedium = null;
				this.WaveformStrong = null;
				this.ThresholdLightMedium = null;
				this.ThresholdMediumStrong = null;

				PluginLog.Info("Migrated legacy config to WaveformZones format");
			}
#pragma warning restore CS0618
		}

		/// <summary>
		/// Saves configuration to file.
		/// </summary>
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
			catch (Exception ex)
			{
				PluginLog.Warning(ex, "Failed to save haptic config");
			}
		}

		/// <summary>
		/// Selects waveform based on intensity value using WaveformZones.
		/// Iterates through zones to find matching intensity range.
		/// </summary>
		/// <param name="intensity">Motor intensity (0-255).</param>
		/// <returns>Waveform name to use.</returns>
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

			// Fallback to last zone's waveform if no match found
			return this.WaveformZones.Last().Waveform;
		}

		/// <summary>
		/// Calculates dynamic pulse interval based on motor intensity using zone-specific curve.
		/// Each zone has its own interval curve for custom interpolation.
		/// </summary>
		/// <param name="intensity">Motor intensity (0-255).</param>
		/// <returns>Interval in 0.1ms units.</returns>
		public Int32 CalculateDynamicInterval(Byte intensity)
		{
			if (!this.EnableDynamicIntensity || intensity == 0)
			{
				return this.PulseIntervalMs;
			}

			// Find the active zone for this intensity
			var zone = this.WaveformZones?.FirstOrDefault(z =>
				intensity >= z.IntensityMin && intensity < z.IntensityMax);

			if (zone == null)
			{
				// Fallback to last zone if no match
				zone = this.WaveformZones?.LastOrDefault();
				if (zone == null)
				{
					return this.PulseIntervalMs;
				}
			}

			// Calculate normalized position within the zone (0.0 to 1.0)
			var range = zone.IntensityMax - zone.IntensityMin;
			if (range <= 0)
			{
				return zone.IntervalMaxTenths;
			}

			var normalized = (intensity - zone.IntensityMin) / (Double)range;

			// Use the zone's curve for interpolation
			return zone.CalculateIntervalAtPosition(normalized);
		}

		#region Simple Mode Helpers

		/// <summary>
		/// Calculates haptic level (0-100) from motor intensity (0-255) for Simple Mode.
		/// Maps motor intensity to MinHapticLevel-MaxHapticLevel range with scaling.
		/// </summary>
		/// <param name="motorIntensity">Motor intensity (0-255).</param>
		/// <returns>Haptic level (0-100).</returns>
		public Byte CalculateSimpleHapticLevel(Byte motorIntensity)
		{
			if (motorIntensity == 0)
			{
				return 0;
			}

			// Normalize motor intensity to 0.0-1.0
			var normalized = motorIntensity / 255.0;

			// Apply scaling
			normalized *= this.IntensityScale;

			// Map to MinHapticLevel-MaxHapticLevel range
			var level = this.MinHapticLevel + (this.MaxHapticLevel - this.MinHapticLevel) * normalized;

			return (Byte)Math.Clamp((Int32)level, 0, 100);
		}

		/// <summary>
		/// Calculates pulse interval in ms from motor intensity (0-255) for Simple Mode.
		/// High intensity = short interval (fast pulses), low intensity = long interval.
		/// </summary>
		/// <param name="motorIntensity">Motor intensity (0-255).</param>
		/// <returns>Interval in milliseconds.</returns>
		public Int32 CalculateSimpleInterval(Byte motorIntensity)
		{
			// Fixed interval mode - always return SimpleIntervalMinMs
			if (this.UseFixedInterval)
			{
				return this.SimpleIntervalMinMs;
			}

			if (motorIntensity == 0)
			{
				return this.SimpleIntervalMaxMs;
			}

			// Normalize motor intensity to 0.0-1.0
			var normalized = motorIntensity / 255.0;

			// Inverse: high intensity = low interval (fast), low intensity = high interval (slow)
			var interval = this.SimpleIntervalMaxMs - (this.SimpleIntervalMaxMs - this.SimpleIntervalMinMs) * normalized;

			return Math.Clamp((Int32)interval, this.SimpleIntervalMinMs, this.SimpleIntervalMaxMs);
		}

		#endregion
	}
}
