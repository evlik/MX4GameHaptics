using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HapticTesterUI
{
	/// <summary>
	/// Haptic waveform IDs from HID++ reverse engineering.
	/// </summary>
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

	/// <summary>
	/// Direct HID++ communication with Logitech MX Master 4 for haptic control.
	/// Supports both Bluetooth and Bolt receiver connections.
	/// </summary>
	public class HidPlusPlusDevice : IDisposable
	{
		// Logitech Vendor ID
		private const ushort LogitechVendorId = 0x046D;

		// HID++ Report IDs
		private const byte ShortReportId = 0x10;  // 7 bytes
		private const byte LongReportId = 0x11;   // 20 bytes

		// HID++ Feature ID for haptic control
		private const ushort FeatureHaptic = 0x19B0;  // Confirmed via Bluetooth

		// HID++ Haptic Function indices
		private const byte FuncWriteHapticLevel = 0x02;  // SET INTENSITY 0-100
		private const byte FuncPlayWaveform = 0x04;

		// Device state
		private IntPtr _deviceHandle = IntPtr.Zero;
		private byte _deviceIndex = 0xFF;  // 0xFF for direct Bluetooth
		private byte _hapticFeatureIndex = 0;
		private bool _useLongReports = false;  // True for Bluetooth
		private bool _initialized = false;

		/// <summary>
		/// Gets whether the device is connected and ready.
		/// </summary>
		public bool IsConnected => _initialized && _deviceHandle != IntPtr.Zero;

		/// <summary>
		/// Gets the device index (0xFF for Bluetooth, 1-6 for receiver slots).
		/// </summary>
		public byte DeviceIndex => _deviceIndex;

		/// <summary>
		/// Gets the haptic feature index on the device.
		/// </summary>
		public byte HapticFeatureIndex => _hapticFeatureIndex;

		/// <summary>
		/// Gets whether the device uses long reports (Bluetooth).
		/// </summary>
		public bool UseLongReports => _useLongReports;

		/// <summary>
		/// Attempts to connect to the MX Master 4.
		/// </summary>
		public bool Connect()
		{
			try
			{
				var devices = HidApi.EnumerateDevices(LogitechVendorId);
				Debug.WriteLine($"Found {devices.Count} Logitech HID devices");

				// Try each device with all possible device indices
				foreach (var path in devices)
				{
					var handle = HidApi.OpenDevice(path);
					if (handle == IntPtr.Zero)
					{
						continue;
					}

					// Try device indices: 0xFF (direct), then 1-6 (receiver slots)
					foreach (var devIdx in new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 })
					{
						// Try Long report ping first (Bluetooth)
						var pingResult = TryPing(handle, devIdx);
						if (pingResult == null)
						{
							continue;
						}

						// Try to find haptic feature
						var (featureIdx, useLong) = GetFeatureIndexWithFallback(handle, FeatureHaptic, devIdx);
						if (featureIdx > 0)
						{
							_deviceHandle = handle;
							_deviceIndex = devIdx;
							_hapticFeatureIndex = featureIdx;
							_useLongReports = useLong;
							_initialized = true;
							Debug.WriteLine($"HID++ connected: idx={devIdx}, feature={featureIdx}, long={useLong}");
							return true;
						}

						// If ping worked but feature not found, try enumeration
						var features = EnumerateFeatures(handle, devIdx, useLong);
						foreach (var (fid, fidx) in features)
						{
							if (IsHapticRelated(fid))
							{
								_deviceHandle = handle;
								_deviceIndex = devIdx;
								_hapticFeatureIndex = fidx;
								_useLongReports = useLong;
								_initialized = true;
								Debug.WriteLine($"HID++ connected via enum: idx={devIdx}, feature=0x{fid:X4}@{fidx}, long={useLong}");
								return true;
							}
						}
					}

					HidApi.CloseDevice(handle);
				}

				Debug.WriteLine("MX Master 4 not found via HID++");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to connect via HID++: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Sets the haptic intensity level (0-100).
		/// </summary>
		public bool SetHapticLevel(byte level)
		{
			if (!IsConnected)
			{
				return false;
			}

			try
			{
				byte[] packet;
				if (_useLongReports)
				{
					packet = new byte[20];
					packet[0] = LongReportId;
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

				return HidApi.WriteReport(_deviceHandle, packet);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to set haptic level: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Plays a haptic waveform directly via HID++.
		/// </summary>
		public bool PlayWaveform(byte waveformId)
		{
			if (!IsConnected)
			{
				return false;
			}

			try
			{
				byte[] packet;
				if (_useLongReports)
				{
					packet = new byte[20];
					packet[0] = LongReportId;
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

				return HidApi.WriteReport(_deviceHandle, packet);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to play waveform: {ex.Message}");
				return false;
			}
		}

		#region HID Communication

		private (byte major, byte minor)? TryPing(IntPtr handle, byte deviceIndex)
		{
			// Try Long Report first (Bluetooth)
			var longPacket = new byte[20];
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
			var shortPacket = new byte[7];
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

		private (byte featureIndex, bool useLong) GetFeatureIndexWithFallback(IntPtr handle, ushort featureId, byte deviceIndex)
		{
			// Try Long Report first
			var longPacket = new byte[20];
			longPacket[0] = LongReportId;
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;  // IRoot
			longPacket[3] = 0x01;  // GetFeature
			longPacket[4] = (byte)(featureId >> 8);
			longPacket[5] = (byte)(featureId & 0xFF);

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response.Length >= 5 && response[2] != 0x8F && response[4] != 0)
				{
					return (response[4], true);
				}
			}

			// Fallback to Short Report
			var shortPacket = new byte[7];
			shortPacket[0] = ShortReportId;
			shortPacket[1] = deviceIndex;
			shortPacket[2] = 0x00;
			shortPacket[3] = 0x01;
			shortPacket[4] = (byte)(featureId >> 8);
			shortPacket[5] = (byte)(featureId & 0xFF);
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

		private List<(ushort featureId, byte featureIndex)> EnumerateFeatures(IntPtr handle, byte deviceIndex, bool useLong)
		{
			var result = new List<(ushort, byte)>();

			// Get IFeatureSet index
			var (featureSetIdx, _) = GetFeatureIndexWithFallback(handle, 0x0001, deviceIndex);
			if (featureSetIdx == 0)
			{
				return result;
			}

			// Get feature count
			byte[] packet;
			if (useLong)
			{
				packet = new byte[20];
				packet[0] = LongReportId;
			}
			else
			{
				packet = new byte[7];
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

			byte featureCount = response[4];

			// Enumerate features
			for (byte i = 0; i < Math.Min(featureCount, (byte)50); i++)
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
					ushort fid = (ushort)((response[4] << 8) | response[5]);
					if (fid != 0)
					{
						result.Add((fid, i));
					}
				}
			}

			return result;
		}

		private bool IsHapticRelated(ushort featureId)
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
			if (_deviceHandle != IntPtr.Zero)
			{
				try { SetHapticLevel(0); } catch { }
				HidApi.CloseDevice(_deviceHandle);
				_deviceHandle = IntPtr.Zero;
			}
			_initialized = false;
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
		private static extern bool HidD_SetOutputReport(IntPtr handle, byte[] data, uint length);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetInputReport(IntPtr handle, byte[] data, uint length);

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
		private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool ReadFile(IntPtr handle, byte[] buffer, uint bytesToRead, out uint bytesRead, ref NativeOverlapped overlapped);

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr handle);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetOverlappedResult(IntPtr hFile, ref NativeOverlapped lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

		[DllImport("kernel32.dll")]
		private static extern bool CancelIo(IntPtr hFile);

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

		private const uint DIGCF_PRESENT = 0x02;
		private const uint DIGCF_DEVICEINTERFACE = 0x10;
		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const uint FILE_SHARE_READ = 0x01;
		private const uint FILE_SHARE_WRITE = 0x02;
		private const uint OPEN_EXISTING = 3;
		private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

		private static bool _useOverlappedIO = false;

		public static List<string> EnumerateDevices(ushort vendorId)
		{
			var result = new List<string>();

			HidD_GetHidGuid(out Guid hidGuid);
			var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (deviceInfoSet == IntPtr.Zero)
			{
				return result;
			}

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
							if (devicePath != null)
							{
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

		public static IntPtr OpenDevice(string path)
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

		public static bool WriteReport(IntPtr handle, byte[] data)
		{
			// Try HidD_SetOutputReport first
			if (HidD_SetOutputReport(handle, data, (uint)data.Length))
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
				var result = WriteFile(handle, data, (uint)data.Length, out _, ref overlapped);
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

		public static byte[]? ReadReport(IntPtr handle, int size, int timeoutMs)
		{
			// Try overlapped read
			if (_useOverlappedIO)
			{
				var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
				if (hEvent != IntPtr.Zero)
				{
					try
					{
						var buffer = new byte[Math.Max(size, 21)];
						var overlapped = new NativeOverlapped { EventHandle = hEvent };

						var result = ReadFile(handle, buffer, (uint)buffer.Length, out uint bytesRead, ref overlapped);
						var err = Marshal.GetLastWin32Error();

						if (!result && err == 997)
						{
							var waitResult = WaitForSingleObject(hEvent, (uint)timeoutMs);
							if (waitResult == 0)
							{
								if (GetOverlappedResult(handle, ref overlapped, out bytesRead, false) && bytesRead > 0)
								{
									return buffer.Take((int)bytesRead).ToArray();
								}
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

			// Fallback to HidD_GetInputReport with polling
			var fallbackBuffer = new byte[Math.Max(size, 21)];
			var sw = Stopwatch.StartNew();

			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				fallbackBuffer[0] = 0x10;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length))
				{
					if (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0)
					{
						return fallbackBuffer;
					}
				}

				fallbackBuffer[0] = 0x11;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length))
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
