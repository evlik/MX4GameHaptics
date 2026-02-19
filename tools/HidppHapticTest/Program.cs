using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace HidppHapticTest
{
	/// <summary>
	/// Simple test tool for MX Master 4 haptic control via HID++.
	/// Tests direct communication bypassing Loupedeck.
	/// </summary>
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("╔════════════════════════════════════════════╗");
			Console.WriteLine("║   MX Master 4 HID++ Haptic Tester          ║");
			Console.WriteLine("║   Feature 0x19B0 Direct Control            ║");
			Console.WriteLine("╚════════════════════════════════════════════╝");
			Console.WriteLine();

			bool autoTest = args.Contains("--test");

			var device = new HidPlusPlusDevice();

			Console.Write("Connecting to MX Master 4... ");
			if (!device.Connect())
			{
				Console.WriteLine("FAILED!");
				Console.WriteLine();
				Console.WriteLine("Possible reasons:");
				Console.WriteLine("  - Mouse not connected");
				Console.WriteLine("  - Logi Options+ has exclusive access");
				Console.WriteLine("  - Wrong Product ID");
				Console.WriteLine();
				Console.WriteLine("Try closing Logi Options+ and retry.");
				if (!autoTest)
				{
					Console.WriteLine("Press any key to exit...");
					Console.ReadKey();
				}
				return;
			}

			Console.WriteLine("OK!");
			Console.WriteLine($"  Device Index: {device.DeviceIndex} (0xFF=direct, 1-6=receiver slot)");
			Console.WriteLine($"  Haptic Feature Index: {device.HapticFeatureIndex}");
			Console.WriteLine($"  Long Reports: {device.UseLongReports}");
			Console.WriteLine();

			// Auto-test mode for non-interactive testing
			if (autoTest)
			{
				Console.WriteLine("=== AUTO TEST MODE ===\n");
				device.DebugMode = true;

				Console.WriteLine("Test 1: Set haptic level to 75%...");
				var result1 = device.SetHapticLevel(75);
				Console.WriteLine($"  Result: {result1}");
				Thread.Sleep(100);

				Console.WriteLine("\nTest 2: Play Wave waveform...");
				var result2 = device.PlayWaveform((byte)HapticWaveform.Wave);
				Console.WriteLine($"  Result: {result2}");
				Thread.Sleep(500);

				Console.WriteLine("\nTest 3: Play Knock waveform...");
				var result3 = device.PlayWaveform((byte)HapticWaveform.Knock);
				Console.WriteLine($"  Result: {result3}");
				Thread.Sleep(500);

				Console.WriteLine("\nTest 4: Play SharpCollision waveform...");
				var result4 = device.PlayWaveform((byte)HapticWaveform.SharpCollision);
				Console.WriteLine($"  Result: {result4}");
				Thread.Sleep(500);

				Console.WriteLine("\nTest 5: Set haptic level to 0% (disable)...");
				var result5 = device.SetHapticLevel(0);
				Console.WriteLine($"  Result: {result5}");

				Console.WriteLine("\n=== AUTO TEST COMPLETE ===");
				Console.WriteLine($"Results: {(result1 && result2 && result3 && result4 && result5 ? "ALL PASSED" : "SOME FAILED")}");

				device.Dispose();
				return;
			}

			while (true)
			{
				Console.WriteLine("Commands:");
				Console.WriteLine("  1 - Test single waveform");
				Console.WriteLine("  2 - Test all waveforms");
				Console.WriteLine("  3 - Test intensity levels");
				Console.WriteLine("  4 - Intensity sweep (smooth test)");
				Console.WriteLine("  5 - Rapid fire test");
				Console.WriteLine("  6 - Set custom level + waveform");
				Console.WriteLine("  7 - Query feature info (debug)");
				Console.WriteLine("  8 - Scan all HID devices (detailed)");
				Console.WriteLine($"  D - Toggle debug mode [{(device.DebugMode ? "ON" : "OFF")}]");
				Console.WriteLine($"  W - Toggle WriteMethod [{(HidApi.UseSetOutputReport ? "HidD_SetOutputReport" : "WriteFile")}]");
				Console.WriteLine("  R - Reconnect with current settings");
				Console.WriteLine("  Q - Quit");
				Console.WriteLine();
				Console.Write("> ");

				var key = Console.ReadKey();
				Console.WriteLine();
				Console.WriteLine();

				switch (key.KeyChar)
				{
					case '1':
						TestSingleWaveform(device);
						break;
					case '2':
						TestAllWaveforms(device);
						break;
					case '3':
						TestIntensityLevels(device);
						break;
					case '4':
						TestIntensitySweep(device);
						break;
					case '5':
						TestRapidFire(device);
						break;
					case '6':
						TestCustom(device);
						break;
					case '7':
						QueryFeatureInfo(device);
						break;
					case '8':
						ScanAllDevices();
						break;
					case 'd':
					case 'D':
						device.DebugMode = !device.DebugMode;
						Console.WriteLine($"Debug mode: {(device.DebugMode ? "ON" : "OFF")}");
						break;
					case 'w':
					case 'W':
						HidApi.UseSetOutputReport = !HidApi.UseSetOutputReport;
						Console.WriteLine($"Write method: {(HidApi.UseSetOutputReport ? "HidD_SetOutputReport" : "WriteFile")}");
						break;
					case 'r':
					case 'R':
						device.Dispose();
						Console.WriteLine("\nReconnecting...\n");
						if (!device.Connect())
						{
							Console.WriteLine("Reconnection failed!");
						}
						break;
					case 'q':
					case 'Q':
						device.Dispose();
						return;
				}

				Console.WriteLine();
			}
		}

		static void TestSingleWaveform(HidPlusPlusDevice device)
		{
			Console.WriteLine("Available waveforms:");
			var waveforms = Enum.GetValues<HapticWaveform>();
			for (int i = 0; i < waveforms.Length; i++)
			{
				Console.WriteLine($"  {i,2}: {waveforms[i]}");
			}
			Console.Write("Enter waveform number: ");
			if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 0 && idx < waveforms.Length)
			{
				var wf = waveforms[idx];
				Console.WriteLine($"Playing {wf}...");
				device.SetHapticLevel(75);
				device.PlayWaveform((byte)wf);
				Console.WriteLine("Done!");
			}
		}

		static void TestAllWaveforms(HidPlusPlusDevice device)
		{
			Console.WriteLine("Playing all waveforms with 500ms delay...");
			Console.WriteLine("Press any key to stop.");
			Console.WriteLine();

			device.SetHapticLevel(75);

			foreach (var wf in Enum.GetValues<HapticWaveform>())
			{
				if (Console.KeyAvailable)
				{
					Console.ReadKey(true);
					break;
				}

				Console.WriteLine($"  {wf}");
				device.PlayWaveform((byte)wf);
				Thread.Sleep(500);
			}

			Console.WriteLine("Done!");
		}

		static void TestIntensityLevels(HidPlusPlusDevice device)
		{
			Console.WriteLine("Testing intensity levels: 0, 25, 50, 75, 100");
			Console.WriteLine("Using WAVE waveform...");
			Console.WriteLine();

			foreach (var level in new byte[] { 0, 25, 50, 75, 100 })
			{
				Console.WriteLine($"  Level: {level}%");
				device.SetHapticLevel(level);
				Thread.Sleep(100);
				device.PlayWaveform((byte)HapticWaveform.Wave);
				Thread.Sleep(500);
			}

			Console.WriteLine("Done!");
		}

		static void TestIntensitySweep(HidPlusPlusDevice device)
		{
			Console.WriteLine("Intensity sweep: 0% -> 100% -> 0%");
			Console.WriteLine("This should feel SMOOTH if HID++ works correctly!");
			Console.WriteLine("Press any key to stop.");
			Console.WriteLine();

			int direction = 1;
			int level = 0;
			int step = 2;

			while (!Console.KeyAvailable)
			{
				device.SetHapticLevel((byte)level);
				device.PlayWaveform((byte)HapticWaveform.Wave);

				// Visual feedback
				int bars = level / 5;
				Console.Write($"\r  Level: {level,3}% [");
				Console.Write(new string('█', bars));
				Console.Write(new string('░', 20 - bars));
				Console.Write("]");

				level += direction * step;
				if (level >= 100) { level = 100; direction = -1; }
				if (level <= 0) { level = 0; direction = 1; }

				Thread.Sleep(30);
			}

			Console.ReadKey(true);
			device.SetHapticLevel(0);
			Console.WriteLine();
			Console.WriteLine("Stopped!");
		}

		static void TestRapidFire(HidPlusPlusDevice device)
		{
			Console.WriteLine("Rapid fire test - 50 pulses at 20ms interval");
			Console.WriteLine("This tests maximum throughput...");
			Console.WriteLine();

			device.SetHapticLevel(100);

			var start = DateTime.Now;
			for (int i = 0; i < 50; i++)
			{
				device.PlayWaveform((byte)HapticWaveform.Knock);
				Thread.Sleep(20);
			}
			var elapsed = (DateTime.Now - start).TotalMilliseconds;

			device.SetHapticLevel(0);

			Console.WriteLine($"Sent 50 pulses in {elapsed:F0}ms");
			Console.WriteLine($"Average: {elapsed / 50:F1}ms per pulse");
			Console.WriteLine("Done!");
		}

		static void TestCustom(HidPlusPlusDevice device)
		{
			Console.Write("Enter intensity (0-100): ");
			if (!int.TryParse(Console.ReadLine(), out int level) || level < 0 || level > 100)
			{
				Console.WriteLine("Invalid level!");
				return;
			}

			Console.WriteLine("Available waveforms:");
			var waveforms = Enum.GetValues<HapticWaveform>();
			for (int i = 0; i < waveforms.Length; i++)
			{
				Console.WriteLine($"  {i,2}: {waveforms[i]}");
			}
			Console.Write("Enter waveform number: ");
			if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= waveforms.Length)
			{
				Console.WriteLine("Invalid waveform!");
				return;
			}

			var wf = waveforms[idx];
			Console.WriteLine($"Setting level to {level}% and playing {wf}...");
			device.SetHapticLevel((byte)level);
			device.PlayWaveform((byte)wf);
			Console.WriteLine("Done!");
		}

		static void QueryFeatureInfo(HidPlusPlusDevice device)
		{
			Console.WriteLine("Querying device features...");
			Console.WriteLine();

			// Query IFeatureSet to get count
			device.QueryFeatures();
		}

		static void ScanAllDevices()
		{
			Console.WriteLine("Scanning ALL HID devices for Logitech...\n");

			var devices = HidApi.EnumerateDevices(0x046D);  // Logitech VID

			// Sort to prioritize mi_02 (HID++ interface on Bolt)
			var sortedDevices = devices.OrderBy(d =>
			{
				if (d.Contains("mi_02")) return 0;  // HID++ interface first
				if (d.Contains("mi_01")) return 1;  // Mouse interface second
				if (d.Contains("mi_03")) return 2;  // DJ interface third
				return 3;
			}).ToList();

			Console.WriteLine($"Found {devices.Count} Logitech devices:\n");

			for (int i = 0; i < sortedDevices.Count; i++)
			{
				var path = sortedDevices[i];
				Console.WriteLine($"=== Device {i} ===");

				// Shorter path display
				var shortPath = path.Substring(path.IndexOf("vid_"));
				if (shortPath.Length > 60) shortPath = shortPath.Substring(0, 60) + "...";
				Console.WriteLine($"Path: {shortPath}");

				// Extract interface info
				var miMatch = System.Text.RegularExpressions.Regex.Match(path.ToLower(), @"mi_(\d+)(&col(\d+))?");
				if (miMatch.Success)
				{
					var iface = miMatch.Groups[1].Value;
					var col = miMatch.Groups[3].Success ? miMatch.Groups[3].Value : "-";
					Console.WriteLine($"Interface: {iface}, Collection: {col}");

					// Known interfaces
					switch (iface)
					{
						case "00": Console.WriteLine("     (Keyboard interface)"); break;
						case "01": Console.WriteLine("     (Mouse interface)"); break;
						case "02": Console.WriteLine("     (HID++ / Device interface) <<<"); break;
						case "03": Console.WriteLine("     (DJ / Pairing interface)"); break;
					}
				}

				// Extract PID
				var pidMatch = System.Text.RegularExpressions.Regex.Match(path.ToLower(), @"pid_([0-9a-f]{4})");
				if (pidMatch.Success)
				{
					var pid = pidMatch.Groups[1].Value.ToUpper();
					switch (pid)
					{
						case "C548": Console.WriteLine("     PID=0xC548 (Bolt Receiver)"); break;
						case "B037": Console.WriteLine("     PID=0xB037 (MX Master 4 USB)"); break;
						case "4099": Console.WriteLine("     PID=0x4099 (MX Master 4 Wireless)"); break;
						case "C52B": Console.WriteLine("     PID=0xC52B (Unifying Receiver)"); break;
					}
				}

				// Try to open
				var handle = HidApi.OpenDevice(path);
				if (handle == IntPtr.Zero)
				{
					Console.WriteLine("Status: CANNOT OPEN");
					Console.WriteLine();
					continue;
				}

				string ioMode = HidApi.UseOverlappedIO ? "Overlapped" : (HidApi.UseSetOutputReport ? "HidD_" : "WriteFile");
				Console.WriteLine($"Status: OPEN (IO={ioMode})");

				HidApi.IsHidPlusPlusInterface(handle, out string info);
				Console.WriteLine($"Caps: {info}");

				// Try a simple ping to device index 1
				Console.WriteLine("Trying IRoot ping...");
				foreach (byte idx in new byte[] { 0x01, 0x02, 0xFF })
				{
					var result = TryPing(handle, idx);
					Console.WriteLine($"  Index {idx}: {result}");
					if (result.StartsWith("OK"))
					{
						Console.WriteLine($"\n  *** SUCCESS! Device responds on index {idx} ***\n");
					}
				}

				HidApi.CloseDevice(handle);
				Console.WriteLine();
			}
		}

		static string TryPing(IntPtr handle, byte deviceIndex)
		{
			// First, try to enumerate ALL features to see what's available
			var enumResult = EnumerateFeatures(handle, deviceIndex);
			if (enumResult.Contains("features:"))
			{
				return enumResult;
			}

			// If enumeration failed, try specific haptic-related feature IDs
			ushort[] hapticFeatureIds = {
				0x19B0,  // HAPTIC (scroll wheel haptics)
				0x8123,  // FORCE_FEEDBACK
				0x2130,  // RATCHET_WHEEL_CONTROL (MagSpeed)
				0x2150,  // THUMB_WHEEL
				0x2250,  // SMARTSHIFT
				0x8070,  // COLOR_LED_EFFECTS
			};

			foreach (var featureId in hapticFeatureIds)
			{
				var packet = new byte[7];
				packet[0] = 0x10;
				packet[1] = deviceIndex;
				packet[2] = 0x00;  // IRoot
				packet[3] = 0x01;  // GetFeatureID, SW_ID 1
				packet[4] = (byte)(featureId >> 8);
				packet[5] = (byte)(featureId & 0xFF);
				packet[6] = 0x00;

				if (!HidApi.WriteReport(handle, packet))
					continue;

				var response = HidApi.ReadReport(handle, 20, 300);
				if (response == null || response[2] == 0x8F)
					continue;

				if (response.Length >= 5 && response[4] > 0)
				{
					return $"OK - Feature 0x{featureId:X4} at index {response[4]}";
				}
			}

			return enumResult;  // Return whatever we got from enumeration
		}

		static string EnumerateFeatures(IntPtr handle, byte deviceIndex)
		{
			// First get IFeatureSet (0x0001) index
			var packet = new byte[7];
			packet[0] = 0x10;
			packet[1] = deviceIndex;
			packet[2] = 0x00;  // IRoot
			packet[3] = 0x01;  // GetFeatureID
			packet[4] = 0x00;  // IFeatureSet = 0x0001
			packet[5] = 0x01;
			packet[6] = 0x00;

			if (!HidApi.WriteReport(handle, packet))
				return "Cannot get IFeatureSet";

			var response = HidApi.ReadReport(handle, 20, 300);
			if (response == null || response[2] == 0x8F || response[4] == 0)
				return "IFeatureSet not found";

			byte featureSetIndex = response[4];

			// Get feature count
			packet[2] = featureSetIndex;
			packet[3] = 0x01;  // GetCount function
			packet[4] = 0x00;
			packet[5] = 0x00;

			if (!HidApi.WriteReport(handle, packet))
				return "Cannot get feature count";

			response = HidApi.ReadReport(handle, 20, 300);
			if (response == null || response[2] == 0x8F)
				return "Cannot read feature count";

			byte featureCount = response[4];
			var features = new System.Text.StringBuilder();
			features.AppendLine($"Device has {featureCount} features:");

			// Enumerate features (function 1 = GetFeatureID by index)
			for (byte i = 0; i < Math.Min(featureCount, (byte)20); i++)
			{
				packet[2] = featureSetIndex;
				packet[3] = 0x11;  // GetFeatureID function (1)
				packet[4] = i;
				packet[5] = 0x00;

				if (!HidApi.WriteReport(handle, packet))
					continue;

				response = HidApi.ReadReport(handle, 20, 200);
				if (response == null || response[2] == 0x8F)
					continue;

				ushort fid = (ushort)((response[4] << 8) | response[5]);
				string fname = GetFeatureName(fid);
				features.AppendLine($"  [{i}] 0x{fid:X4} {fname}");
			}

			return features.ToString();
		}

		static string GetFeatureName(ushort id) => id switch
		{
			0x0000 => "IRoot",
			0x0001 => "IFeatureSet",
			0x0003 => "DeviceInfo",
			0x0005 => "DeviceType",
			0x0007 => "DeviceFriendlyName",
			0x0020 => "ConfigChange",
			0x0021 => "Crypto",
			0x00C2 => "DFUControl",
			0x00C3 => "DFUControl3",
			0x1000 => "BatteryStatus",
			0x1001 => "BatteryVoltage",
			0x1004 => "UnifiedBattery",
			0x1814 => "ChangeHost",
			0x1815 => "HostList",
			0x1830 => "PowerModes",
			0x1861 => "XYStats",
			0x1891 => "MKeys",
			0x1982 => "BacklightControl",
			0x1990 => "Illumination",
			0x19B0 => "HAPTIC <<<",
			0x1B00 => "ReprogramKeys",
			0x1B04 => "ReprogramKeysV4",
			0x1BC0 => "ReportHID",
			0x1D4B => "WirelessDeviceStatus",
			0x1E00 => "HiReportMode",
			0x1E02 => "AdjustDPI",
			0x1F03 => "AGB",
			0x1F20 => "LED",
			0x2001 => "LeftRightSwap",
			0x2100 => "VerticalScrolling",
			0x2110 => "SmartShiftEnhanced",
			0x2121 => "HiResScrolling",
			0x2130 => "RATCHET_WHEEL <<<",
			0x2150 => "THUMB_WHEEL",
			0x2201 => "AdjustDPI2",
			0x2250 => "SMARTSHIFT <<<",
			0x40A3 => "FnInversion",
			0x4100 => "Encryption",
			0x4521 => "DisableKeys",
			0x4522 => "DisableKeysByUsage",
			0x4530 => "DualPlatform",
			0x4540 => "Keyboard",
			0x4600 => "CrownControl",
			0x6010 => "TouchpadFW",
			0x6011 => "TouchpadSW",
			0x6012 => "TouchpadWin8FW",
			0x6100 => "TouchpadRaw",
			0x6500 => "GestureControl",
			0x6501 => "Gestures2",
			0x8010 => "GKey",
			0x8020 => "MKey",
			0x8030 => "MacroRecord",
			0x8040 => "Brightness",
			0x8060 => "ReportRate",
			0x8070 => "ColorLedEffects",
			0x8071 => "RGBEffects",
			0x8080 => "PerKeyLighting",
			0x8081 => "PerKeyLightingV2",
			0x8090 => "ModeStat",
			0x8100 => "OnboardMemory",
			0x8110 => "MouseButtonSpy",
			0x8120 => "LatencyMonitor",
			0x8123 => "FORCE_FEEDBACK <<<",
			_ => ""
		};
	}

	#region HID++ Implementation (standalone copy)

	public enum HapticWaveform : byte
	{
		SharpStateChange = 0x00,
		DampStateChange = 0x01,
		SharpCollision = 0x02,
		DampCollision = 0x03,
		SubtleCollision = 0x04,
		HappyAlert = 0x05,
		AngryAlert = 0x06,
		Completed = 0x07,
		Square = 0x08,
		Wave = 0x09,
		Firework = 0x0A,
		Mad = 0x0B,
		Knock = 0x0C,
		Jingle = 0x0D,
		Ringing = 0x0E,
		WhisperCollision = 0x1B
	}

	public class HidPlusPlusDevice : IDisposable
	{
		private const ushort LogitechVendorId = 0x046D;
		private const byte ShortReportId = 0x10;
		private const ushort FeatureHaptic = 0x19B0;  // MX Master 4 haptic feature (confirmed via Bluetooth)
		private const byte FuncWriteHapticLevel = 0x02;
		private const byte FuncPlayWaveform = 0x04;

		private IntPtr _deviceHandle = IntPtr.Zero;
		private byte _deviceIndex = 0xFF;
		private byte _hapticFeatureIndex = 0;
		private bool _useLongReports = false;  // True for Bluetooth, False for Bolt

		public byte DeviceIndex => _deviceIndex;
		public byte HapticFeatureIndex => _hapticFeatureIndex;
		public bool UseLongReports => _useLongReports;
		public bool IsConnected => _deviceHandle != IntPtr.Zero && _hapticFeatureIndex > 0;

		public bool Connect()
		{
			var devices = HidApi.EnumerateDevices(LogitechVendorId);
			Console.WriteLine($"Found {devices.Count} Logitech HID devices");

			// First pass: Show info about all devices and identify HID++ interfaces
			var hidppDevices = new List<(string path, string info)>();

			Console.WriteLine("\n--- Device Analysis ---");
			for (int i = 0; i < devices.Count; i++)
			{
				var path = devices[i];
				Console.WriteLine($"\n[{i}] {path}");

				var handle = HidApi.OpenDevice(path);
				if (handle == IntPtr.Zero)
				{
					Console.WriteLine("    Cannot open (exclusive access?)");
					continue;
				}

				bool isHidpp = HidApi.IsHidPlusPlusInterface(handle, out string info);
				Console.WriteLine($"    {info}");

				if (isHidpp)
				{
					Console.WriteLine("    >>> Potential HID++ interface!");
					hidppDevices.Add((path, info));
				}

				HidApi.CloseDevice(handle);
			}

			Console.WriteLine($"\n--- Found {hidppDevices.Count} potential HID++ interfaces ---\n");

			// Try HID++ interfaces first (sorted by output report size - larger is better)
			var sortedDevices = hidppDevices
				.OrderByDescending(d => d.info.Contains("Out=20") || d.info.Contains("Out=21"))
				.ThenByDescending(d => d.info.Contains("Page=FF"))
				.Select(d => d.path)
				.ToList();

			// Add remaining devices as fallback
			foreach (var path in devices)
			{
				if (!sortedDevices.Contains(path))
					sortedDevices.Add(path);
			}

			// Try each device
			foreach (var path in sortedDevices)
			{
				Console.WriteLine($"Trying: {path}");

				var handle = HidApi.OpenDevice(path);
				if (handle == IntPtr.Zero)
				{
					Console.WriteLine("  Cannot open");
					continue;
				}

				HidApi.IsHidPlusPlusInterface(handle, out string info);
				Console.WriteLine($"  {info}");

				// Try device indices 0xFF (direct), then 1-6 (receiver slots)
				byte[] indicesToTry = { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

				foreach (var devIdx in indicesToTry)
				{
					Console.Write($"  Trying index {devIdx}... ");

					// First try Ping to verify protocol
					var pingResult = TryPingDevice(handle, devIdx);
					if (pingResult != null)
					{
						Console.Write($"[Proto v{pingResult.Value.major}.{pingResult.Value.minor}] ");
					}
					var (featureIdx, debugInfo) = GetFeatureIndexDebug(handle, FeatureHaptic, devIdx);
					if (featureIdx > 0)
					{
						_deviceHandle = handle;
						_deviceIndex = devIdx;
						_hapticFeatureIndex = featureIdx;
						_useLongReports = (pingResult != null && debugInfo.Contains("Long"));
						Console.WriteLine($"SUCCESS! Feature index {featureIdx} (LongReports={_useLongReports})");
						return true;
					}
					Console.WriteLine(debugInfo);

					// If we got a successful ping but feature not found, enumerate features
					bool shouldEnumerate = debugInfo.StartsWith("ERR 0x09") ||
						(pingResult != null && debugInfo.StartsWith("Not found"));

					if (shouldEnumerate)
					{
						Console.WriteLine($"\n  >>> Device {devIdx} responds but HAPTIC not found.");
						Console.WriteLine("  >>> Enumerating all features...");
						var features = EnumerateAllFeatures(handle, devIdx);
						if (features.Count > 0)
						{
							Console.WriteLine($"  Found {features.Count} features:");
							foreach (var (fid, fidx) in features)
							{
								var fname = GetFeatureNameStatic(fid);
								var marker = IsHapticRelated(fid) ? " <<<" : "";
								Console.WriteLine($"    [{fidx:X2}] 0x{fid:X4} {fname}{marker}");
							}

							// Look for haptic-related features
							foreach (var (fid, fidx) in features)
							{
								if (IsHapticRelated(fid))
								{
									Console.WriteLine($"\n  Found haptic-related feature 0x{fid:X4} at index {fidx}!");
									_deviceHandle = handle;
									_deviceIndex = devIdx;
									_hapticFeatureIndex = fidx;
									// If we got here via ping with Long reports, remember that
									_useLongReports = (pingResult != null);
									return true;
								}
							}
						}
						Console.WriteLine();
					}
				}

				HidApi.CloseDevice(handle);
			}

			// If nothing found, let user try manual selection
			Console.WriteLine("\nNo haptic device found automatically.");
			Console.Write("Would you like to try manual mode? (y/n): ");
			var response = Console.ReadLine();
			if (response?.ToLower().StartsWith("y") == true)
			{
				return TryManualConnect(devices);
			}

			return false;
		}

		private bool TryManualConnect(List<string> devices)
		{
			Console.WriteLine("\nSelect device:");
			for (int i = 0; i < devices.Count; i++)
			{
				Console.WriteLine($"  {i}: {devices[i]}");
			}
			Console.Write("Enter number: ");
			if (!int.TryParse(Console.ReadLine(), out int devNum) || devNum < 0 || devNum >= devices.Count)
				return false;

			var handle = HidApi.OpenDevice(devices[devNum]);
			if (handle == IntPtr.Zero)
			{
				Console.WriteLine("Cannot open device");
				return false;
			}

			Console.Write("Enter device index (0-255, default 1): ");
			var input = Console.ReadLine();
			byte devIdx = string.IsNullOrEmpty(input) ? (byte)1 : byte.Parse(input);

			Console.Write("Enter feature index (default 1): ");
			input = Console.ReadLine();
			byte featIdx = string.IsNullOrEmpty(input) ? (byte)1 : byte.Parse(input);

			_deviceHandle = handle;
			_deviceIndex = devIdx;
			_hapticFeatureIndex = featIdx;

			Console.WriteLine($"Manual mode: Device index {devIdx}, Feature index {featIdx}");
			return true;
		}

		private byte GetFeatureIndex(IntPtr handle, ushort featureId, byte deviceIndex = 0xFF)
		{
			var (idx, _) = GetFeatureIndexDebug(handle, featureId, deviceIndex);
			return idx;
		}

		private (byte featureIndex, string debug) GetFeatureIndexDebug(IntPtr handle, ushort featureId, byte deviceIndex = 0xFF)
		{
			try
			{
				// Try Long Report first (20 bytes) - works for Bluetooth
				var longPacket = new byte[20];
				longPacket[0] = 0x11;  // Long Report ID
				longPacket[1] = deviceIndex;
				longPacket[2] = 0x00;  // IRoot
				longPacket[3] = 0x01;  // GetFeature function (0 << 4 | SW_ID)
				longPacket[4] = (byte)(featureId >> 8);
				longPacket[5] = (byte)(featureId & 0xFF);
				longPacket[6] = 0;

				if (HidApi.WriteReport(handle, longPacket))
				{
					var response = HidApi.ReadReport(handle, 20, 500);
					if (response != null && response.Length >= 5)
					{
						var hex = BitConverter.ToString(response.Take(7).ToArray());
						if (response[2] == 0x8F)
						{
							byte errCode = response.Length > 5 ? response[5] : (byte)0;
							return (0, $"LongErr 0x{errCode:X2} [{hex}]");
						}
						if (response[4] != 0)
							return (response[4], $"LongFound! [{hex}]");  // Include "Long" in debug to indicate Long reports work
						return (0, $"LongNotFound [{hex}]");  // Include "Long" to indicate this device responds to Long reports
					}
				}

				// Fallback to Short Report (7 bytes) - for Bolt receiver
				var shortPacket = new byte[7];
				shortPacket[0] = ShortReportId;
				shortPacket[1] = deviceIndex;
				shortPacket[2] = 0x00;  // IRoot
				shortPacket[3] = 0x01;  // GetFeature function (0 << 4 | SW_ID)
				shortPacket[4] = (byte)(featureId >> 8);
				shortPacket[5] = (byte)(featureId & 0xFF);
				shortPacket[6] = 0;

				bool writeOk = HidApi.WriteReport(handle, shortPacket);
				int lastErr = Marshal.GetLastWin32Error();

				if (!writeOk)
					return (0, $"Write err={lastErr}");

				var shortResponse = HidApi.ReadReport(handle, 20, 500);

				if (shortResponse == null)
				{
					lastErr = Marshal.GetLastWin32Error();
					return (0, $"No resp err={lastErr}");
				}

				var shortHex = BitConverter.ToString(shortResponse.Take(7).ToArray());

				if (shortResponse[2] == 0x8F)
				{
					byte errCode = shortResponse.Length > 5 ? shortResponse[5] : (byte)0;
					return (0, $"ERR 0x{errCode:X2} [{shortHex}]");
				}

				if (shortResponse[1] != deviceIndex)
					return (0, $"Wrong idx [{shortHex}]");

				if (shortResponse[4] != 0)
					return (shortResponse[4], $"Found! [{shortHex}]");

				return (0, $"Not found [{shortHex}]");
			}
			catch (Exception ex)
			{
				return (0, $"Ex: {ex.Message}");
			}
		}

		public bool DebugMode { get; set; } = false;

		public bool SetHapticLevel(byte level)
		{
			if (!IsConnected) return false;

			byte[] packet;
			if (_useLongReports)
			{
				packet = new byte[20];
				packet[0] = 0x11;  // Long Report
			}
			else
			{
				packet = new byte[7];
				packet[0] = ShortReportId;
			}

			packet[1] = _deviceIndex;
			packet[2] = _hapticFeatureIndex;
			packet[3] = (byte)((FuncWriteHapticLevel << 4) | 0x01);

			if (level == 0)
			{
				packet[4] = 0x00;
				packet[5] = 0x32;
			}
			else
			{
				packet[4] = 0x01;
				packet[5] = Math.Min(level, (byte)100);
			}

			if (DebugMode)
				Console.WriteLine($"  TX SetLevel: {BitConverter.ToString(packet)}");

			var result = HidApi.WriteReport(_deviceHandle, packet);

			if (DebugMode)
				Console.WriteLine($"  Result: {result}");

			return result;
		}

		public bool PlayWaveform(byte waveformId)
		{
			if (!IsConnected) return false;

			byte[] packet;
			if (_useLongReports)
			{
				packet = new byte[20];
				packet[0] = 0x11;  // Long Report
			}
			else
			{
				packet = new byte[7];
				packet[0] = ShortReportId;
			}

			packet[1] = _deviceIndex;
			packet[2] = _hapticFeatureIndex;
			packet[3] = (byte)((FuncPlayWaveform << 4) | 0x01);
			packet[4] = waveformId;

			if (DebugMode)
				Console.WriteLine($"  TX PlayWaveform: {BitConverter.ToString(packet)}");

			var result = HidApi.WriteReport(_deviceHandle, packet);

			if (DebugMode)
				Console.WriteLine($"  Result: {result}");

			return result;
		}

		public void QueryFeatures()
		{
			Console.WriteLine($"Device Index: {_deviceIndex}");
			Console.WriteLine($"Current Haptic Feature Index: {_hapticFeatureIndex}");
			Console.WriteLine();

			// Query IFeatureSet (feature 0x0001) to get feature count
			Console.WriteLine("Querying IFeatureSet (0x0001)...");
			var featureSetIdx = QueryFeatureIndexRaw(0x0001);
			Console.WriteLine($"  IFeatureSet index: {featureSetIdx}");

			if (featureSetIdx > 0)
			{
				// Get feature count using IFeatureSet.GetCount (function 0)
				var packet = new byte[7];
				packet[0] = ShortReportId;
				packet[1] = _deviceIndex;
				packet[2] = featureSetIdx;
				packet[3] = 0x01;  // Function 0, SW_ID 1
				Console.WriteLine($"  TX: {BitConverter.ToString(packet)}");

				if (HidApi.WriteReport(_deviceHandle, packet))
				{
					var response = HidApi.ReadReport(_deviceHandle, 20, 1000);
					if (response != null)
					{
						Console.WriteLine($"  RX: {BitConverter.ToString(response)}");
						Console.WriteLine($"  Feature count: {response[4]}");
					}
				}
			}

			Console.WriteLine();
			Console.WriteLine("Querying HAPTIC feature (0x19B0)...");
			var hapticIdx = QueryFeatureIndexRaw(0x19B0);
			Console.WriteLine($"  HAPTIC index: {hapticIdx}");

			if (hapticIdx > 0)
			{
				// Try GetCapabilities (function 0)
				Console.WriteLine();
				Console.WriteLine("Querying HAPTIC capabilities (function 0)...");
				var packet = new byte[7];
				packet[0] = ShortReportId;
				packet[1] = _deviceIndex;
				packet[2] = hapticIdx;
				packet[3] = 0x01;  // Function 0, SW_ID 1
				Console.WriteLine($"  TX: {BitConverter.ToString(packet)}");

				if (HidApi.WriteReport(_deviceHandle, packet))
				{
					var response = HidApi.ReadReport(_deviceHandle, 20, 1000);
					if (response != null)
					{
						Console.WriteLine($"  RX: {BitConverter.ToString(response)}");
						// Parse capabilities
						if (response.Length >= 8)
						{
							Console.WriteLine($"  Supported waveforms bitmask: {response[4]:X2} {response[5]:X2} {response[6]:X2} {response[7]:X2}");
						}
					}
				}
			}
		}

		/// <summary>
		/// Ping device using IRoot.Ping (function 1) to get protocol version.
		/// Also tries HID++ 1.0 version request.
		/// </summary>
		private (byte major, byte minor)? TryPingDevice(IntPtr handle, byte deviceIndex)
		{
			Console.Write("[Ping ");

			// Try Long Report (0x11, 20 bytes) first - Bolt may require this!
			var longPacket = new byte[20];
			longPacket[0] = 0x11;  // Long Report ID
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;  // IRoot
			longPacket[3] = 0x11;  // Function 1 (Ping), SW_ID 1
			longPacket[4] = 0x00;
			longPacket[5] = 0x00;
			longPacket[6] = 0xAA;  // Ping data

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 7)
				{
					Console.Write($"Long:{BitConverter.ToString(response.Take(7).ToArray())} ");
					if (response[2] != 0x8F)
					{
						Console.Write($"v{response[4]}.{response[5]}] ");
						return (response[4], response[5]);
					}
				}
			}

			// Try HID++ 2.0 ping with Short report
			var packet = new byte[7];
			packet[0] = ShortReportId;
			packet[1] = deviceIndex;
			packet[2] = 0x00;  // IRoot
			packet[3] = 0x11;  // Function 1 (Ping), SW_ID 1
			packet[4] = 0x00;
			packet[5] = 0x00;
			packet[6] = 0xAA;  // Ping data

			if (HidApi.WriteReport(handle, packet))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 7)
				{
					Console.Write($"Short:{BitConverter.ToString(response.Take(7).ToArray())} ");
					if (response[2] != 0x8F)
					{
						Console.Write($"v{response[4]}.{response[5]}] ");
						return (response[4], response[5]);
					}
				}
			}

			// Try HID++ 1.0 - read firmware version register (0x81, sub 0x01)
			packet[0] = ShortReportId;
			packet[1] = deviceIndex;
			packet[2] = 0x81;  // Register: Firmware Info
			packet[3] = 0x01;  // Sub-register: Firmware entity 1
			packet[4] = 0x00;
			packet[5] = 0x00;
			packet[6] = 0x00;

			if (HidApi.WriteReport(handle, packet))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 7)
				{
					Console.Write($"FW:{BitConverter.ToString(response.Take(7).ToArray())} ");
					if (response[2] != 0x8F)
					{
						Console.Write($"HID++1.0!] ");
						return (1, 0);  // HID++ 1.0
					}
				}
			}

			// Try Bolt receiver pairing info (0xB5, subId 0x20+n for device n)
			if (deviceIndex >= 1 && deviceIndex <= 6)
			{
				packet[0] = ShortReportId;
				packet[1] = 0xFF;  // Receiver itself
				packet[2] = 0xB5;  // Bolt pairing info register
				packet[3] = (byte)(0x20 + deviceIndex);  // Device info sub-register
				packet[4] = 0x00;
				packet[5] = 0x00;
				packet[6] = 0x00;

				if (HidApi.WriteReport(handle, packet))
				{
					var response = HidApi.ReadReport(handle, 20, 300);
					if (response != null && response.Length >= 7)
					{
						Console.Write($"BOLT:{BitConverter.ToString(response.Take(7).ToArray())} ");
						if (response[2] != 0x8F && response[4] != 0)
						{
							Console.Write("PAIRED!] ");
							return (2, 0);  // HID++ 2.0 via Bolt
						}
					}
				}
			}

			Console.Write("FAIL] ");
			return null;
		}

		private static bool IsHapticRelated(ushort featureId)
		{
			return featureId switch
			{
				0x0B4E => true,  // MX Master 4 HAPTIC <<<
				0x19B0 => true,  // HAPTIC (other devices)
				0x2110 => true,  // SMART_SHIFT
				0x2111 => true,  // SMART_SHIFT_V2
				0x2130 => true,  // LORES_SCROLLING (ratchet)
				0x2250 => true,  // SMARTSHIFT (legacy)
				0x8123 => true,  // FORCE_FEEDBACK
				_ => false
			};
		}

		private static string GetFeatureNameStatic(ushort id) => id switch
		{
			0x0000 => "IRoot",
			0x0001 => "IFeatureSet",
			0x0003 => "DeviceInfo",
			0x0005 => "DeviceType",
			0x0007 => "DeviceFriendlyName",
			0x0020 => "ConfigChange",
			0x0B4E => "MX4_HAPTIC <<<",  // MX Master 4 haptic!
			0x1000 => "BatteryStatus",
			0x1004 => "UnifiedBattery",
			0x1814 => "ChangeHost",
			0x1815 => "HostList",
			0x1982 => "BacklightControl",
			0x19B0 => "HAPTIC",
			0x1B04 => "ReprogramKeysV4",
			0x1D4B => "WirelessDeviceStatus",
			0x2110 => "SMART_SHIFT",
			0x2111 => "SMART_SHIFT_V2",
			0x2121 => "HiResScrolling",
			0x2130 => "LORES_SCROLLING",
			0x2150 => "THUMB_WHEEL",
			0x2201 => "AdjustDPI2",
			0x2250 => "SMARTSHIFT",
			0x4530 => "DualPlatform",
			0x6501 => "Gestures2",
			0x8060 => "ReportRate",
			0x8070 => "ColorLedEffects",
			0x8100 => "OnboardMemory",
			0x8123 => "FORCE_FEEDBACK",
			_ => ""
		};

		private List<(ushort featureId, byte featureIndex)> EnumerateAllFeatures(IntPtr handle, byte deviceIndex)
		{
			var result = new List<(ushort, byte)>();

			// Try Long report first (for Bluetooth), then Short report
			var longPacket = new byte[20];
			var shortPacket = new byte[7];
			byte[] response = null;
			byte featureSetIndex = 0;

			// Query IFeatureSet (0x0001) with Long report first
			longPacket[0] = 0x11;  // Long Report
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;  // IRoot
			longPacket[3] = 0x01;  // GetFeatureID
			longPacket[4] = 0x00;  // IFeatureSet = 0x0001
			longPacket[5] = 0x01;

			Console.WriteLine($"    [Enum] Query IFeatureSet (Long) TX: {BitConverter.ToString(longPacket.Take(8).ToArray())}");

			if (HidApi.WriteReport(handle, longPacket))
			{
				response = HidApi.ReadReport(handle, 20, 500);
				if (response != null)
				{
					Console.WriteLine($"    [Enum] RX: {BitConverter.ToString(response.Take(8).ToArray())}");
					if (response[2] != 0x8F && response[4] != 0)
					{
						featureSetIndex = response[4];
					}
				}
			}

			// Fallback to Short report
			if (featureSetIndex == 0)
			{
				shortPacket[0] = ShortReportId;
				shortPacket[1] = deviceIndex;
				shortPacket[2] = 0x00;
				shortPacket[3] = 0x01;
				shortPacket[4] = 0x00;
				shortPacket[5] = 0x01;
				shortPacket[6] = 0x00;

				Console.WriteLine($"    [Enum] Query IFeatureSet (Short) TX: {BitConverter.ToString(shortPacket)}");

				if (HidApi.WriteReport(handle, shortPacket))
				{
					response = HidApi.ReadReport(handle, 20, 500);
					if (response != null)
					{
						Console.WriteLine($"    [Enum] RX: {BitConverter.ToString(response.Take(8).ToArray())}");
						if (response[2] == 0x8F)
						{
							Console.WriteLine($"    [Enum] Error response 0x{response[5]:X2}");
							return result;
						}
						if (response[4] != 0)
							featureSetIndex = response[4];
					}
				}
			}

			if (featureSetIndex == 0)
			{
				Console.WriteLine("    [Enum] IFeatureSet not found");
				return result;
			}

			Console.WriteLine($"    [Enum] IFeatureSet at index {featureSetIndex}");

			// Get feature count using IFeatureSet.GetCount (function 0)
			// Use Long report first
			longPacket[2] = featureSetIndex;
			longPacket[3] = 0x01;  // Function 0, SW_ID 1
			longPacket[4] = 0x00;
			longPacket[5] = 0x00;

			byte featureCount = 0;
			if (HidApi.WriteReport(handle, longPacket))
			{
				response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response[2] != 0x8F)
				{
					featureCount = response[4];
				}
			}

			if (featureCount == 0)
			{
				// Try Short report
				shortPacket[2] = featureSetIndex;
				shortPacket[3] = 0x01;
				shortPacket[4] = 0x00;
				shortPacket[5] = 0x00;

				if (HidApi.WriteReport(handle, shortPacket))
				{
					response = HidApi.ReadReport(handle, 20, 500);
					if (response != null && response[2] != 0x8F)
						featureCount = response[4];
				}
			}

			Console.WriteLine($"    [Enum] Feature count: {featureCount}");

			// Enumerate features using IFeatureSet.GetFeatureID (function 1)
			for (byte i = 0; i < Math.Min(featureCount, (byte)40); i++)
			{
				// Try Long report first
				longPacket[2] = featureSetIndex;
				longPacket[3] = 0x11;  // Function 1, SW_ID 1
				longPacket[4] = i;
				longPacket[5] = 0x00;

				bool gotFeature = false;
				if (HidApi.WriteReport(handle, longPacket))
				{
					response = HidApi.ReadReport(handle, 20, 300);
					if (response != null && response[2] != 0x8F)
					{
						ushort fid = (ushort)((response[4] << 8) | response[5]);
						if (fid != 0)
						{
							result.Add((fid, i));
							gotFeature = true;
						}
					}
				}

				if (!gotFeature)
				{
					// Fallback to Short report
					shortPacket[2] = featureSetIndex;
					shortPacket[3] = 0x11;
					shortPacket[4] = i;
					shortPacket[5] = 0x00;

					if (HidApi.WriteReport(handle, shortPacket))
					{
						response = HidApi.ReadReport(handle, 20, 300);
						if (response != null && response[2] != 0x8F)
						{
							ushort fid = (ushort)((response[4] << 8) | response[5]);
							if (fid != 0)
								result.Add((fid, i));
						}
					}
				}
			}

			return result;
		}

		private byte QueryFeatureIndexRaw(ushort featureId)
		{
			var packet = new byte[7];
			packet[0] = ShortReportId;
			packet[1] = _deviceIndex;
			packet[2] = 0x00;  // IRoot
			packet[3] = 0x01;  // GetFeature (function 0), SW_ID 1
			packet[4] = (byte)(featureId >> 8);
			packet[5] = (byte)(featureId & 0xFF);

			Console.WriteLine($"  TX: {BitConverter.ToString(packet)}");

			if (!HidApi.WriteReport(_deviceHandle, packet))
			{
				Console.WriteLine("  Write failed!");
				return 0;
			}

			var response = HidApi.ReadReport(_deviceHandle, 20, 1000);
			if (response != null)
			{
				Console.WriteLine($"  RX: {BitConverter.ToString(response)}");
				return response[4];
			}

			Console.WriteLine("  No response!");
			return 0;
		}

		public void Dispose()
		{
			if (_deviceHandle != IntPtr.Zero)
			{
				try { SetHapticLevel(0); } catch { }
				HidApi.CloseDevice(_deviceHandle);
				_deviceHandle = IntPtr.Zero;
			}
			_deviceIndex = 0xFF;
			_hapticFeatureIndex = 0;
		}
	}

	internal static class HidApi
	{
		[DllImport("hid.dll")]
		private static extern void HidD_GetHidGuid(out Guid hidGuid);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_SetOutputReport(IntPtr handle, byte[] data, uint length);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetInputReport(IntPtr handle, byte[] data, uint length);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

		[DllImport("setupapi.dll")]
		private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool ReadFile(IntPtr handle, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool ReadFile(IntPtr handle, byte[] buffer, uint bytesToRead, out uint bytesRead, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr handle);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetOverlappedResult(IntPtr hFile, ref NativeOverlapped lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

		[DllImport("kernel32.dll")]
		private static extern bool CancelIo(IntPtr hFile);

		private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

		[DllImport("hid.dll")]
		private static extern bool HidD_GetAttributes(IntPtr handle, ref HIDD_ATTRIBUTES attributes);

		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVICE_INTERFACE_DATA
		{
			public uint cbSize;
			public Guid InterfaceClassGuid;
			public uint Flags;
			public IntPtr Reserved;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDD_ATTRIBUTES
		{
			public uint Size;
			public ushort VendorID;
			public ushort ProductID;
			public ushort VersionNumber;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct HIDP_CAPS
		{
			public ushort Usage;
			public ushort UsagePage;
			public ushort InputReportByteLength;
			public ushort OutputReportByteLength;
			public ushort FeatureReportByteLength;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
			public ushort[] Reserved;
			public ushort NumberLinkCollectionNodes;
			public ushort NumberInputButtonCaps;
			public ushort NumberInputValueCaps;
			public ushort NumberInputDataIndices;
			public ushort NumberOutputButtonCaps;
			public ushort NumberOutputValueCaps;
			public ushort NumberOutputDataIndices;
			public ushort NumberFeatureButtonCaps;
			public ushort NumberFeatureValueCaps;
			public ushort NumberFeatureDataIndices;
		}

		private const uint DIGCF_PRESENT = 0x02;
		private const uint DIGCF_DEVICEINTERFACE = 0x10;
		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const uint FILE_SHARE_READ = 0x01;
		private const uint FILE_SHARE_WRITE = 0x02;
		private const uint OPEN_EXISTING = 3;

		/// <summary>
		/// Gets HID capabilities for a device handle.
		/// </summary>
		public static HIDP_CAPS? GetDeviceCaps(IntPtr handle)
		{
			if (!HidD_GetPreparsedData(handle, out IntPtr preparsedData))
				return null;

			try
			{
				var caps = new HIDP_CAPS();
				if (HidP_GetCaps(preparsedData, out caps) == 0) // HIDP_STATUS_SUCCESS
				{
					return caps;
				}
				return null;
			}
			finally
			{
				HidD_FreePreparsedData(preparsedData);
			}
		}

		/// <summary>
		/// Checks if device supports HID++ (vendor-specific usage page, correct report sizes).
		/// </summary>
		public static bool IsHidPlusPlusInterface(IntPtr handle, out string info)
		{
			info = "";
			var caps = GetDeviceCaps(handle);
			if (!caps.HasValue)
			{
				// Can't get caps - but device is open, might still work
				// Check if path contains mi_02 (typical HID++ interface on Bolt)
				info = "Cannot get caps (might still work for HID++)";
				return true;  // Return true to try it anyway
			}

			var c = caps.Value;
			info = $"Usage={c.Usage:X4}, Page={c.UsagePage:X4}, In={c.InputReportByteLength}, Out={c.OutputReportByteLength}";

			// HID++ criteria:
			// - Vendor-specific usage page (0xFF00 to 0xFFFF) OR usage page 1 (Generic Desktop)
			// - Input report size: 7 (short), 8 (short+1), 20 (long), 21 (long+1), or 64 (DJ)
			// - Output report size matches

			bool vendorPage = c.UsagePage >= 0xFF00;
			bool hasShortReport = c.InputReportByteLength == 7 || c.InputReportByteLength == 8;
			bool hasLongReport = c.InputReportByteLength == 20 || c.InputReportByteLength == 21;
			bool hasDjReport = c.InputReportByteLength == 64 || c.InputReportByteLength == 65;

			// Accept if vendor page OR if report sizes match HID++ patterns
			return vendorPage || hasShortReport || hasLongReport || hasDjReport;
		}

		public static List<string> EnumerateDevices(ushort vendorId)
		{
			var result = new List<string>();

			HidD_GetHidGuid(out Guid hidGuid);
			var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (deviceInfoSet == IntPtr.Zero) return result;

			try
			{
				var interfaceData = new SP_DEVICE_INTERFACE_DATA();
				interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

				uint index = 0;
				while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index++, ref interfaceData))
				{
					SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
					var detailData = Marshal.AllocHGlobal((int)requiredSize);
					try
					{
						Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
						if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out _, IntPtr.Zero))
						{
							var devicePath = Marshal.PtrToStringAuto(detailData + 4);
							var handle = CreateFile(devicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
							if (handle != IntPtr.Zero && handle != new IntPtr(-1))
							{
								var attrs = new HIDD_ATTRIBUTES { Size = (uint)Marshal.SizeOf<HIDD_ATTRIBUTES>() };
								if (HidD_GetAttributes(handle, ref attrs) && attrs.VendorID == vendorId)
								{
									result.Add(devicePath);
								}
								CloseHandle(handle);
							}
						}
					}
					finally { Marshal.FreeHGlobal(detailData); }
				}
			}
			finally { SetupDiDestroyDeviceInfoList(deviceInfoSet); }

			return result;
		}

		public static IntPtr OpenDevice(string path)
		{
			// Reset flags for each device
			UseSetOutputReport = false;
			UseOverlappedIO = false;

			// Try with overlapped I/O for async read/write
			var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
			if (handle != new IntPtr(-1))
			{
				UseOverlappedIO = true;
				UseSetOutputReport = false;  // Explicitly use overlapped I/O
				return handle;
			}

			// Try without overlapped
			handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle != new IntPtr(-1))
			{
				UseOverlappedIO = false;
				UseSetOutputReport = false;  // Use WriteFile/ReadFile
				return handle;
			}

			// For system devices (mouse/keyboard), Windows has exclusive access
			// Try opening with NO access - we can still use HidD_SetOutputReport/HidD_GetInputReport
			handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle != new IntPtr(-1))
			{
				Console.WriteLine("    [Opened with restricted access - using HidD_ functions]");
				UseSetOutputReport = true;
				UseOverlappedIO = false;
				return handle;
			}

			return IntPtr.Zero;
		}

		public static bool UseOverlappedIO { get; set; } = false;

		public static void CloseDevice(IntPtr handle)
		{
			if (handle != IntPtr.Zero) CloseHandle(handle);
		}

		public static bool UseSetOutputReport { get; set; } = false;

		public static bool WriteReport(IntPtr handle, byte[] data)
		{
			// For HID devices, HidD_SetOutputReport is more reliable
			// Try it first, then fall back to WriteFile
			bool result = HidD_SetOutputReport(handle, data, (uint)data.Length);
			if (result) return true;

			// If SetOutputReport failed and we have overlapped I/O, try WriteFile
			if (!UseOverlappedIO) return false;

			var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
			if (hEvent == IntPtr.Zero) return false;

			try
			{
				var overlapped = new NativeOverlapped { EventHandle = hEvent };
				result = WriteFile(handle, data, (uint)data.Length, out _, ref overlapped);
				int err = Marshal.GetLastWin32Error();

				if (!result && err == 997) // ERROR_IO_PENDING
				{
					uint waitResult = WaitForSingleObject(hEvent, 1000);
					if (waitResult == 0) // WAIT_OBJECT_0
					{
						GetOverlappedResult(handle, ref overlapped, out _, false);
						return true;
					}
					CancelIo(handle);
					return false;
				}

				return result;
			}
			finally
			{
				CloseHandle(hEvent);
			}
		}

		public static byte[] ReadReport(IntPtr handle, int size, int timeoutMs)
		{
			// Try overlapped read first - this is the proper way to get HID++ responses
			if (UseOverlappedIO)
			{
				var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
				if (hEvent != IntPtr.Zero)
				{
					try
					{
						var buffer = new byte[Math.Max(size, 21)];
						var overlapped = new NativeOverlapped { EventHandle = hEvent };

						bool result = ReadFile(handle, buffer, (uint)buffer.Length, out uint bytesRead, ref overlapped);
						int err = Marshal.GetLastWin32Error();

						if (!result && err == 997) // ERROR_IO_PENDING
						{
							uint waitResult = WaitForSingleObject(hEvent, (uint)timeoutMs);
							if (waitResult == 0) // WAIT_OBJECT_0
							{
								if (GetOverlappedResult(handle, ref overlapped, out bytesRead, false) && bytesRead > 0)
									return buffer.Take((int)bytesRead).ToArray();
							}
							CancelIo(handle);
						}
						else if (result && bytesRead > 0)
						{
							return buffer.Take((int)bytesRead).ToArray();
						}
					}
					finally
					{
						CloseHandle(hEvent);
					}
				}
			}

			// Fallback: Try HidD_GetInputReport with polling (less reliable for HID++)
			var fallbackBuffer = new byte[Math.Max(size, 21)];
			var sw = System.Diagnostics.Stopwatch.StartNew();

			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				fallbackBuffer[0] = 0x10;  // Short report
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length))
				{
					// Check if we got actual data (not just zeros or stale buffer)
					if (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0)
						return fallbackBuffer;
				}

				fallbackBuffer[0] = 0x11;  // Long report
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length))
				{
					if (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0)
						return fallbackBuffer;
				}

				Thread.Sleep(5);
			}
			return null;
		}
	}

	#endregion
}
