namespace Loupedeck.LogiHapticPlugin
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Threading;

	/// <summary>
	/// Haptic waveform IDs from Solaar/HID++ reverse engineering.
	/// </summary>
	public enum HapticWaveform : Byte
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

	/// <summary>
	/// Direct HID++ communication with Logitech MX Master 4 for haptic control.
	/// Supports both Bluetooth and Bolt receiver connections.
	/// </summary>
	public class HidPlusPlusDevice : IDisposable
	{
		// Logitech Vendor ID
		private const UInt16 LogitechVendorId = 0x046D;

		// HID++ Report IDs
		private const Byte ShortReportId = 0x10;  // 7 bytes
		private const Byte LongReportId = 0x11;   // 20 bytes

		// HID++ Feature ID for haptic control
		private const UInt16 FeatureHaptic = 0x19B0;  // Confirmed via Bluetooth

		// HID++ Haptic Function indices
		private const Byte FuncWriteHapticLevel = 0x02;  // SET INTENSITY 0-100
		private const Byte FuncPlayWaveform = 0x04;

		// Device state
		private IntPtr _deviceHandle = IntPtr.Zero;
		private Byte _deviceIndex = 0xFF;  // 0xFF for direct Bluetooth
		private Byte _hapticFeatureIndex = 0;
		private Boolean _useLongReports = false;  // True for Bluetooth
		private Boolean _initialized = false;

		/// <summary>
		/// Gets whether the device is connected and ready.
		/// </summary>
		public Boolean IsConnected => this._initialized && this._deviceHandle != IntPtr.Zero;

		/// <summary>
		/// Attempts to connect to the MX Master 4.
		/// </summary>
		public Boolean Connect()
		{
			try
			{
				var devices = HidApi.EnumerateDevices(LogitechVendorId);
				PluginLog.Info($"Found {devices.Count} Logitech HID devices");

				// Try each device with all possible device indices
				foreach (var path in devices)
				{
					var handle = HidApi.OpenDevice(path);
					if (handle == IntPtr.Zero)
					{
						continue;
					}

					// Try device indices: 0xFF (direct), then 1-6 (receiver slots)
					foreach (var devIdx in new Byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 })
					{
						// Try Long report ping first (Bluetooth)
						var pingResult = this.TryPing(handle, devIdx);
						if (pingResult == null)
						{
							continue;
						}

						// Try to find haptic feature
						var (featureIdx, useLong) = this.GetFeatureIndexWithFallback(handle, FeatureHaptic, devIdx);
						if (featureIdx > 0)
						{
							this._deviceHandle = handle;
							this._deviceIndex = devIdx;
							this._hapticFeatureIndex = featureIdx;
							this._useLongReports = useLong;
							this._initialized = true;
							PluginLog.Info($"HID++ connected: idx={devIdx}, feature={featureIdx}, long={useLong}");
							return true;
						}

						// If ping worked but feature not found, try enumeration
						var features = this.EnumerateFeatures(handle, devIdx, useLong);
						foreach (var (fid, fidx) in features)
						{
							if (this.IsHapticRelated(fid))
							{
								this._deviceHandle = handle;
								this._deviceIndex = devIdx;
								this._hapticFeatureIndex = fidx;
								this._useLongReports = useLong;
								this._initialized = true;
								PluginLog.Info($"HID++ connected via enum: idx={devIdx}, feature=0x{fid:X4}@{fidx}, long={useLong}");
								return true;
							}
						}
					}

					HidApi.CloseDevice(handle);
				}

				PluginLog.Warning("MX Master 4 not found via HID++");
				return false;
			}
			catch (Exception ex)
			{
				PluginLog.Error(ex, "Failed to connect via HID++");
				return false;
			}
		}

		/// <summary>
		/// Sets the haptic intensity level (0-100).
		/// </summary>
		public Boolean SetHapticLevel(Byte level)
		{
			if (!this.IsConnected)
			{
				return false;
			}

			try
			{
				Byte[] packet;
				if (this._useLongReports)
				{
					packet = new Byte[20];
					packet[0] = LongReportId;
				}
				else
				{
					packet = new Byte[7];
					packet[0] = ShortReportId;
				}

				packet[1] = this._deviceIndex;
				packet[2] = this._hapticFeatureIndex;
				packet[3] = (Byte)((FuncWriteHapticLevel << 4) | 0x01);

				if (level == 0)
				{
					packet[4] = 0x00;
					packet[5] = 0x32;
				}
				else
				{
					packet[4] = 0x01;
					packet[5] = Math.Min(level, (Byte)100);
				}

				return HidApi.WriteReport(this._deviceHandle, packet);
			}
			catch (Exception ex)
			{
				PluginLog.Error(ex, "Failed to set haptic level");
				return false;
			}
		}

		/// <summary>
		/// Plays a haptic waveform directly via HID++.
		/// </summary>
		public Boolean PlayWaveform(Byte waveformId)
		{
			if (!this.IsConnected)
			{
				return false;
			}

			try
			{
				Byte[] packet;
				if (this._useLongReports)
				{
					packet = new Byte[20];
					packet[0] = LongReportId;
				}
				else
				{
					packet = new Byte[7];
					packet[0] = ShortReportId;
				}

				packet[1] = this._deviceIndex;
				packet[2] = this._hapticFeatureIndex;
				packet[3] = (Byte)((FuncPlayWaveform << 4) | 0x01);
				packet[4] = waveformId;

				return HidApi.WriteReport(this._deviceHandle, packet);
			}
			catch (Exception ex)
			{
				PluginLog.Error(ex, "Failed to play waveform");
				return false;
			}
		}

		/// <summary>
		/// Legacy method - triggers haptic using PlayWaveform.
		/// </summary>
		public Boolean TriggerHaptic(Byte patternId) => this.PlayWaveform(patternId);

		/// <summary>
		/// Sets intensity level and then plays a waveform.
		/// </summary>
		public Boolean PlayWaveformWithIntensity(Byte waveformId, Byte intensity)
		{
			this.SetHapticLevel(intensity);
			return this.PlayWaveform(waveformId);
		}

		/// <summary>
		/// Legacy compatibility.
		/// </summary>
		public Boolean TriggerHapticWithIntensity(Byte patternId, Byte intensity)
			=> this.PlayWaveformWithIntensity(patternId, intensity);

		#region HID Communication

		private (Byte major, Byte minor)? TryPing(IntPtr handle, Byte deviceIndex)
		{
			// Try Long Report first (Bluetooth)
			var longPacket = new Byte[20];
			longPacket[0] = LongReportId;
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;  // IRoot
			longPacket[3] = 0x11;  // Ping function
			longPacket[4] = 0x00;
			longPacket[5] = 0x00;
			longPacket[6] = 0xAA;  // Ping data

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 6 && response[2] != 0x8F)
				{
					return (response[4], response[5]);
				}
			}

			// Try Short Report (Bolt receiver)
			var shortPacket = new Byte[7];
			shortPacket[0] = ShortReportId;
			shortPacket[1] = deviceIndex;
			shortPacket[2] = 0x00;
			shortPacket[3] = 0x11;
			shortPacket[4] = 0x00;
			shortPacket[5] = 0x00;
			shortPacket[6] = 0xAA;

			if (HidApi.WriteReport(handle, shortPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 6 && response[2] != 0x8F)
				{
					return (response[4], response[5]);
				}
			}

			return null;
		}

		private (Byte featureIndex, Boolean useLong) GetFeatureIndexWithFallback(IntPtr handle, UInt16 featureId, Byte deviceIndex)
		{
			// Try Long Report first
			var longPacket = new Byte[20];
			longPacket[0] = LongReportId;
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;  // IRoot
			longPacket[3] = 0x01;  // GetFeature
			longPacket[4] = (Byte)(featureId >> 8);
			longPacket[5] = (Byte)(featureId & 0xFF);

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response.Length >= 5 && response[2] != 0x8F && response[4] != 0)
				{
					return (response[4], true);
				}
			}

			// Fallback to Short Report
			var shortPacket = new Byte[7];
			shortPacket[0] = ShortReportId;
			shortPacket[1] = deviceIndex;
			shortPacket[2] = 0x00;
			shortPacket[3] = 0x01;
			shortPacket[4] = (Byte)(featureId >> 8);
			shortPacket[5] = (Byte)(featureId & 0xFF);
			shortPacket[6] = 0;

			if (HidApi.WriteReport(handle, shortPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response.Length >= 5 && response[2] != 0x8F && response[4] != 0)
				{
					return (response[4], false);
				}
			}

			return (0, false);
		}

		private List<(UInt16 featureId, Byte featureIndex)> EnumerateFeatures(IntPtr handle, Byte deviceIndex, Boolean useLong)
		{
			var result = new List<(UInt16, Byte)>();

			// Get IFeatureSet index
			var (featureSetIdx, _) = this.GetFeatureIndexWithFallback(handle, 0x0001, deviceIndex);
			if (featureSetIdx == 0)
			{
				return result;
			}

			// Get feature count
			Byte[] packet;
			if (useLong)
			{
				packet = new Byte[20];
				packet[0] = LongReportId;
			}
			else
			{
				packet = new Byte[7];
				packet[0] = ShortReportId;
			}

			packet[1] = deviceIndex;
			packet[2] = featureSetIdx;
			packet[3] = 0x01;  // GetCount function
			packet[4] = 0;
			packet[5] = 0;

			if (!HidApi.WriteReport(handle, packet))
			{
				return result;
			}

			var response = HidApi.ReadReport(handle, 20, 500);
			if (response == null || response[2] == 0x8F)
			{
				return result;
			}

			Byte featureCount = response[4];

			// Enumerate features
			for (Byte i = 0; i < Math.Min(featureCount, (Byte)40); i++)
			{
				packet[2] = featureSetIdx;
				packet[3] = 0x11;  // GetFeatureID function
				packet[4] = i;
				packet[5] = 0;

				if (!HidApi.WriteReport(handle, packet))
				{
					continue;
				}

				response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response[2] != 0x8F)
				{
					UInt16 fid = (UInt16)((response[4] << 8) | response[5]);
					if (fid != 0)
					{
						result.Add((fid, i));
					}
				}
			}

			return result;
		}

		private Boolean IsHapticRelated(UInt16 featureId)
		{
			return featureId switch
			{
				0x19B0 => true,  // HAPTIC
				0x0B4E => true,  // Alternate haptic ID
				0x2110 => true,  // SMART_SHIFT
				0x2111 => true,  // SMART_SHIFT_V2
				0x2250 => true,  // SMARTSHIFT
				_ => false
			};
		}

		#endregion

		public void Dispose()
		{
			if (this._deviceHandle != IntPtr.Zero)
			{
				try { this.SetHapticLevel(0); } catch { }
				HidApi.CloseDevice(this._deviceHandle);
				this._deviceHandle = IntPtr.Zero;
			}
			this._initialized = false;
		}
	}

	/// <summary>
	/// Windows HID API wrapper with overlapped I/O support.
	/// </summary>
	internal static class HidApi
	{
		[DllImport("hid.dll")]
		private static extern void HidD_GetHidGuid(out Guid hidGuid);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern Boolean HidD_SetOutputReport(IntPtr handle, Byte[] data, UInt32 length);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern Boolean HidD_GetInputReport(IntPtr handle, Byte[] data, UInt32 length);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, UInt32 flags);

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, UInt32 memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern Boolean SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, UInt32 deviceInterfaceDetailDataSize, out UInt32 requiredSize, IntPtr deviceInfoData);

		[DllImport("setupapi.dll")]
		private static extern Boolean SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr CreateFile(String fileName, UInt32 desiredAccess, UInt32 shareMode, IntPtr securityAttributes, UInt32 creationDisposition, UInt32 flagsAndAttributes, IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean WriteFile(IntPtr handle, Byte[] buffer, UInt32 bytesToWrite, out UInt32 bytesWritten, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean ReadFile(IntPtr handle, Byte[] buffer, UInt32 bytesToRead, out UInt32 bytesRead, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll")]
		private static extern Boolean CloseHandle(IntPtr handle);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, Boolean bManualReset, Boolean bInitialState, String lpName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern Boolean GetOverlappedResult(IntPtr hFile, ref NativeOverlapped lpOverlapped, out UInt32 lpNumberOfBytesTransferred, Boolean bWait);

		[DllImport("kernel32.dll")]
		private static extern Boolean CancelIo(IntPtr hFile);

		[DllImport("hid.dll")]
		private static extern Boolean HidD_GetAttributes(IntPtr handle, ref HIDD_ATTRIBUTES attributes);

		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVICE_INTERFACE_DATA
		{
			public UInt32 cbSize;
			public Guid InterfaceClassGuid;
			public UInt32 Flags;
			public IntPtr Reserved;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDD_ATTRIBUTES
		{
			public UInt32 Size;
			public UInt16 VendorID;
			public UInt16 ProductID;
			public UInt16 VersionNumber;
		}

		private const UInt32 DIGCF_PRESENT = 0x02;
		private const UInt32 DIGCF_DEVICEINTERFACE = 0x10;
		private const UInt32 GENERIC_READ = 0x80000000;
		private const UInt32 GENERIC_WRITE = 0x40000000;
		private const UInt32 FILE_SHARE_READ = 0x01;
		private const UInt32 FILE_SHARE_WRITE = 0x02;
		private const UInt32 OPEN_EXISTING = 3;
		private const UInt32 FILE_FLAG_OVERLAPPED = 0x40000000;

		private static Boolean _useOverlappedIO = false;

		public static List<String> EnumerateDevices(UInt16 vendorId)
		{
			var result = new List<String>();

			HidD_GetHidGuid(out Guid hidGuid);
			var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (deviceInfoSet == IntPtr.Zero)
			{
				return result;
			}

			try
			{
				var interfaceData = new SP_DEVICE_INTERFACE_DATA();
				interfaceData.cbSize = (UInt32)Marshal.SizeOf(interfaceData);

				UInt32 index = 0;
				while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index++, ref interfaceData))
				{
					SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out UInt32 requiredSize, IntPtr.Zero);
					var detailData = Marshal.AllocHGlobal((Int32)requiredSize);
					try
					{
						Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
						if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out _, IntPtr.Zero))
						{
							var devicePath = Marshal.PtrToStringAuto(detailData + 4);
							var handle = CreateFile(devicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
							if (handle != IntPtr.Zero && handle != new IntPtr(-1))
							{
								var attrs = new HIDD_ATTRIBUTES { Size = (UInt32)Marshal.SizeOf<HIDD_ATTRIBUTES>() };
								if (HidD_GetAttributes(handle, ref attrs) && attrs.VendorID == vendorId)
								{
									result.Add(devicePath);
								}
								CloseHandle(handle);
							}
						}
					}
					finally
					{
						Marshal.FreeHGlobal(detailData);
					}
				}
			}
			finally
			{
				SetupDiDestroyDeviceInfoList(deviceInfoSet);
			}

			return result;
		}

		public static IntPtr OpenDevice(String path)
		{
			_useOverlappedIO = false;

			// Try with overlapped I/O
			var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
			if (handle != new IntPtr(-1))
			{
				_useOverlappedIO = true;
				return handle;
			}

			// Try without overlapped
			handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle != new IntPtr(-1))
			{
				return handle;
			}

			// Try with no access (for HidD_ functions)
			handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			return handle == new IntPtr(-1) ? IntPtr.Zero : handle;
		}

		public static void CloseDevice(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				CloseHandle(handle);
			}
		}

		public static Boolean WriteReport(IntPtr handle, Byte[] data)
		{
			// Try HidD_SetOutputReport first
			if (HidD_SetOutputReport(handle, data, (UInt32)data.Length))
			{
				return true;
			}

			// Fallback to overlapped WriteFile
			if (!_useOverlappedIO)
			{
				return false;
			}

			var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
			if (hEvent == IntPtr.Zero)
			{
				return false;
			}

			try
			{
				var overlapped = new NativeOverlapped { EventHandle = hEvent };
				var result = WriteFile(handle, data, (UInt32)data.Length, out _, ref overlapped);
				var err = Marshal.GetLastWin32Error();

				if (!result && err == 997)  // ERROR_IO_PENDING
				{
					var waitResult = WaitForSingleObject(hEvent, 1000);
					if (waitResult == 0)
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

		public static Byte[] ReadReport(IntPtr handle, Int32 size, Int32 timeoutMs)
		{
			// Try overlapped read
			if (_useOverlappedIO)
			{
				var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
				if (hEvent != IntPtr.Zero)
				{
					try
					{
						var buffer = new Byte[Math.Max(size, 21)];
						var overlapped = new NativeOverlapped { EventHandle = hEvent };

						var result = ReadFile(handle, buffer, (UInt32)buffer.Length, out UInt32 bytesRead, ref overlapped);
						var err = Marshal.GetLastWin32Error();

						if (!result && err == 997)
						{
							var waitResult = WaitForSingleObject(hEvent, (UInt32)timeoutMs);
							if (waitResult == 0)
							{
								if (GetOverlappedResult(handle, ref overlapped, out bytesRead, false) && bytesRead > 0)
								{
									return buffer.Take((Int32)bytesRead).ToArray();
								}
							}
							CancelIo(handle);
						}
						else if (result && bytesRead > 0)
						{
							return buffer.Take((Int32)bytesRead).ToArray();
						}
					}
					finally
					{
						CloseHandle(hEvent);
					}
				}
			}

			// Fallback to HidD_GetInputReport with polling
			var fallbackBuffer = new Byte[Math.Max(size, 21)];
			var sw = System.Diagnostics.Stopwatch.StartNew();

			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				fallbackBuffer[0] = 0x10;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (UInt32)fallbackBuffer.Length))
				{
					if (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0)
					{
						return fallbackBuffer;
					}
				}

				fallbackBuffer[0] = 0x11;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (UInt32)fallbackBuffer.Length))
				{
					if (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0)
					{
						return fallbackBuffer;
					}
				}

				Thread.Sleep(5);
			}

			return null;
		}
	}
}
