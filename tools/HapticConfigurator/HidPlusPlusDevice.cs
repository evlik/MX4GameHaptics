using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace HapticConfigurator
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
		private const ushort LogitechVendorId = 0x046D;
		private const byte ShortReportId = 0x10;
		private const byte LongReportId = 0x11;

		// Known Haptic Feature IDs
		private const ushort FeatureHapticOld = 0x19B0;   // Original haptic feature
		private const ushort FeatureHapticNew = 0x0B4E;   // MX Master 4 haptic feature

		// Function indices for haptic control
		private const byte FuncWriteHapticLevel = 0x02;
		private const byte FuncPlayWaveform = 0x04;

		// Bolt Receiver PIDs
		private static readonly ushort[] BoltReceiverPids = { 0xC548, 0xC547, 0xC545 };

		private IntPtr _deviceHandle = IntPtr.Zero;
		private byte _deviceIndex = 0xFF;
		private byte _hapticFeatureIndex = 0;
		private ushort _hapticFeatureId = 0;
		private bool _useLongReports = false;
		private bool _initialized = false;
		private string _connectionMode = "Unknown";

		public bool IsConnected => _initialized && _deviceHandle != IntPtr.Zero;
		public byte DeviceIndex => _deviceIndex;
		public byte HapticFeatureIndex => _hapticFeatureIndex;
		public ushort HapticFeatureId => _hapticFeatureId;
		public bool UseLongReports => _useLongReports;
		public string ConnectionMode => _connectionMode;

		public bool Connect()
		{
			try
			{
				var devices = HidApi.EnumerateDevicesWithInfo(LogitechVendorId);

				// Sort: prioritize Bolt receivers first, then direct connections
				var sortedDevices = devices
					.OrderBy(d => BoltReceiverPids.Contains(d.ProductId) ? 0 : 1)
					.ThenBy(d => d.Path)
					.ToList();

				foreach (var device in sortedDevices)
				{
					var handle = HidApi.OpenDevice(device.Path);
					if (handle == IntPtr.Zero) continue;

					var isBoltReceiver = BoltReceiverPids.Contains(device.ProductId);

					// Device indices to try:
					// - 0xFF for direct Bluetooth connection
					// - 0x01-0x06 for devices paired to receiver
					var indicesToTry = isBoltReceiver
						? new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }
						: new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

					foreach (var devIdx in indicesToTry)
					{
						var pingResult = TryPing(handle, devIdx);
						if (pingResult == null) continue;

						// Try new MX Master 4 haptic feature first (0x0B4E)
						var (featureIdx, useLong) = GetFeatureIndexWithFallback(handle, FeatureHapticNew, devIdx);
						if (featureIdx > 0)
						{
							_deviceHandle = handle;
							_deviceIndex = devIdx;
							_hapticFeatureIndex = featureIdx;
							_hapticFeatureId = FeatureHapticNew;
							_useLongReports = useLong;
							_connectionMode = isBoltReceiver ? $"Bolt Receiver (PID:{device.ProductId:X4})" : "Bluetooth";
							_initialized = true;
							return true;
						}

						// Try old haptic feature (0x19B0)
						(featureIdx, useLong) = GetFeatureIndexWithFallback(handle, FeatureHapticOld, devIdx);
						if (featureIdx > 0)
						{
							_deviceHandle = handle;
							_deviceIndex = devIdx;
							_hapticFeatureIndex = featureIdx;
							_hapticFeatureId = FeatureHapticOld;
							_useLongReports = useLong;
							_connectionMode = isBoltReceiver ? $"Bolt Receiver (PID:{device.ProductId:X4})" : "Bluetooth";
							_initialized = true;
							return true;
						}

						// Enumerate all features and look for haptic-related ones
						var features = EnumerateFeatures(handle, devIdx, useLong);
						foreach (var (fid, fidx) in features)
						{
							if (IsHapticRelated(fid))
							{
								_deviceHandle = handle;
								_deviceIndex = devIdx;
								_hapticFeatureIndex = fidx;
								_hapticFeatureId = fid;
								_useLongReports = useLong;
								_connectionMode = isBoltReceiver ? $"Bolt Receiver (PID:{device.ProductId:X4})" : "Bluetooth";
								_initialized = true;
								return true;
							}
						}
					}
					HidApi.CloseDevice(handle);
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		public bool SetHapticLevel(byte level)
		{
			if (!IsConnected) return false;
			try
			{
				byte[] packet = _useLongReports ? new byte[20] : new byte[7];
				packet[0] = _useLongReports ? LongReportId : ShortReportId;
				packet[1] = _deviceIndex;
				packet[2] = _hapticFeatureIndex;
				packet[3] = (byte)((FuncWriteHapticLevel << 4) | 0x01);
				packet[4] = level == 0 ? (byte)0x00 : (byte)0x01;
				packet[5] = level == 0 ? (byte)0x32 : Math.Min(level, (byte)100);
				return HidApi.WriteReport(_deviceHandle, packet);
			}
			catch { return false; }
		}

		public bool PlayWaveform(byte waveformId)
		{
			if (!IsConnected) return false;
			try
			{
				byte[] packet = _useLongReports ? new byte[20] : new byte[7];
				packet[0] = _useLongReports ? LongReportId : ShortReportId;
				packet[1] = _deviceIndex;
				packet[2] = _hapticFeatureIndex;
				packet[3] = (byte)((FuncPlayWaveform << 4) | 0x01);
				packet[4] = waveformId;
				return HidApi.WriteReport(_deviceHandle, packet);
			}
			catch { return false; }
		}

		public bool PlayWaveformWithLevel(byte waveformId, byte level)
		{
			SetHapticLevel(level);
			return PlayWaveform(waveformId);
		}

		private (byte, byte)? TryPing(IntPtr handle, byte deviceIndex)
		{
			var longPacket = new byte[20];
			longPacket[0] = LongReportId;
			longPacket[1] = deviceIndex;
			longPacket[2] = 0x00;
			longPacket[3] = 0x11;
			longPacket[6] = 0xAA;

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 6 && response[2] != 0x8F)
					return (response[4], response[5]);
			}

			var shortPacket = new byte[7];
			shortPacket[0] = ShortReportId;
			shortPacket[1] = deviceIndex;
			shortPacket[3] = 0x11;
			shortPacket[6] = 0xAA;

			if (HidApi.WriteReport(handle, shortPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response.Length >= 6 && response[2] != 0x8F)
					return (response[4], response[5]);
			}
			return null;
		}

		private (byte featureIndex, bool useLong) GetFeatureIndexWithFallback(IntPtr handle, ushort featureId, byte deviceIndex)
		{
			var longPacket = new byte[20];
			longPacket[0] = LongReportId;
			longPacket[1] = deviceIndex;
			longPacket[3] = 0x01;
			longPacket[4] = (byte)(featureId >> 8);
			longPacket[5] = (byte)(featureId & 0xFF);

			if (HidApi.WriteReport(handle, longPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response.Length >= 5 && response[2] != 0x8F && response[4] != 0)
					return (response[4], true);
			}

			var shortPacket = new byte[7];
			shortPacket[0] = ShortReportId;
			shortPacket[1] = deviceIndex;
			shortPacket[3] = 0x01;
			shortPacket[4] = (byte)(featureId >> 8);
			shortPacket[5] = (byte)(featureId & 0xFF);

			if (HidApi.WriteReport(handle, shortPacket))
			{
				var response = HidApi.ReadReport(handle, 20, 500);
				if (response != null && response.Length >= 5 && response[2] != 0x8F && response[4] != 0)
					return (response[4], false);
			}
			return (0, false);
		}

		private List<(ushort, byte)> EnumerateFeatures(IntPtr handle, byte deviceIndex, bool useLong)
		{
			var result = new List<(ushort, byte)>();
			var (featureSetIdx, _) = GetFeatureIndexWithFallback(handle, 0x0001, deviceIndex);
			if (featureSetIdx == 0) return result;

			byte[] packet = useLong ? new byte[20] : new byte[7];
			packet[0] = useLong ? LongReportId : ShortReportId;
			packet[1] = deviceIndex;
			packet[2] = featureSetIdx;
			packet[3] = 0x01;

			if (!HidApi.WriteReport(handle, packet)) return result;
			var response = HidApi.ReadReport(handle, 20, 500);
			if (response == null || response[2] == 0x8F) return result;

			byte featureCount = response[4];
			for (byte i = 0; i < Math.Min(featureCount, (byte)50); i++)
			{
				packet[3] = 0x11;
				packet[4] = i;
				packet[5] = 0;
				if (!HidApi.WriteReport(handle, packet)) continue;
				response = HidApi.ReadReport(handle, 20, 300);
				if (response != null && response[2] != 0x8F)
				{
					ushort fid = (ushort)((response[4] << 8) | response[5]);
					if (fid != 0) result.Add((fid, i));
				}
			}
			return result;
		}

		private bool IsHapticRelated(ushort featureId) => featureId is 0x19B0 or 0x0B4E or 0x2110 or 0x2111 or 0x2250;

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
	/// Device information for enumeration.
	/// </summary>
	public struct HidDeviceInfo
	{
		public string Path;
		public ushort VendorId;
		public ushort ProductId;
	}

	internal static class HidApi
	{
		[DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
		[DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_SetOutputReport(IntPtr handle, byte[] data, uint length);
		[DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetInputReport(IntPtr handle, byte[] data, uint length);
		[DllImport("setupapi.dll", SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);
		[DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);
		[DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, ref NativeOverlapped overlapped);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(IntPtr handle, byte[] buffer, uint bytesToRead, out uint bytesRead, ref NativeOverlapped overlapped);
		[DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
		[DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetOverlappedResult(IntPtr hFile, ref NativeOverlapped lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);
		[DllImport("kernel32.dll")] private static extern bool CancelIo(IntPtr hFile);
		[DllImport("hid.dll")] private static extern bool HidD_GetAttributes(IntPtr handle, ref HIDD_ATTRIBUTES attributes);

		[StructLayout(LayoutKind.Sequential)] private struct SP_DEVICE_INTERFACE_DATA { public uint cbSize; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }
		[StructLayout(LayoutKind.Sequential)] private struct HIDD_ATTRIBUTES { public uint Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

		private const uint DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
		private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
		private const uint FILE_SHARE_READ = 0x01, FILE_SHARE_WRITE = 0x02, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
		private static bool _useOverlappedIO = false;

		public static List<HidDeviceInfo> EnumerateDevicesWithInfo(ushort vendorId)
		{
			var result = new List<HidDeviceInfo>();
			HidD_GetHidGuid(out Guid hidGuid);
			var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (deviceInfoSet == IntPtr.Zero) return result;

			try
			{
				var interfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
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
										result.Add(new HidDeviceInfo
										{
											Path = devicePath,
											VendorId = attrs.VendorID,
											ProductId = attrs.ProductID
										});
									}
									CloseHandle(handle);
								}
							}
						}
					}
					finally { Marshal.FreeHGlobal(detailData); }
				}
			}
			finally { SetupDiDestroyDeviceInfoList(deviceInfoSet); }
			return result;
		}

		public static List<string> EnumerateDevices(ushort vendorId)
		{
			var result = new List<string>();
			HidD_GetHidGuid(out Guid hidGuid);
			var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (deviceInfoSet == IntPtr.Zero) return result;

			try
			{
				var interfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
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
										result.Add(devicePath);
									CloseHandle(handle);
								}
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
			_useOverlappedIO = false;
			var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
			if (handle != new IntPtr(-1)) { _useOverlappedIO = true; return handle; }
			handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			if (handle != new IntPtr(-1)) return handle;
			handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
			return handle == new IntPtr(-1) ? IntPtr.Zero : handle;
		}

		public static void CloseDevice(IntPtr handle) { if (handle != IntPtr.Zero) CloseHandle(handle); }

		public static bool WriteReport(IntPtr handle, byte[] data)
		{
			if (HidD_SetOutputReport(handle, data, (uint)data.Length)) return true;
			if (!_useOverlappedIO) return false;

			var hEvent = CreateEvent(IntPtr.Zero, true, false, null);
			if (hEvent == IntPtr.Zero) return false;
			try
			{
				var overlapped = new NativeOverlapped { EventHandle = hEvent };
				var result = WriteFile(handle, data, (uint)data.Length, out _, ref overlapped);
				if (!result && Marshal.GetLastWin32Error() == 997)
				{
					if (WaitForSingleObject(hEvent, 1000) == 0) { GetOverlappedResult(handle, ref overlapped, out _, false); return true; }
					CancelIo(handle); return false;
				}
				return result;
			}
			finally { CloseHandle(hEvent); }
		}

		public static byte[]? ReadReport(IntPtr handle, int size, int timeoutMs)
		{
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
						if (!result && Marshal.GetLastWin32Error() == 997)
						{
							if (WaitForSingleObject(hEvent, (uint)timeoutMs) == 0 && GetOverlappedResult(handle, ref overlapped, out bytesRead, false) && bytesRead > 0)
								return buffer.Take((int)bytesRead).ToArray();
							CancelIo(handle);
						}
						else if (result && bytesRead > 0) return buffer.Take((int)bytesRead).ToArray();
					}
					finally { CloseHandle(hEvent); }
				}
			}

			var fallbackBuffer = new byte[Math.Max(size, 21)];
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				fallbackBuffer[0] = 0x10;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length) && (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0))
					return fallbackBuffer;
				fallbackBuffer[0] = 0x11;
				Array.Clear(fallbackBuffer, 1, fallbackBuffer.Length - 1);
				if (HidD_GetInputReport(handle, fallbackBuffer, (uint)fallbackBuffer.Length) && (fallbackBuffer[1] != 0 || fallbackBuffer[2] != 0 || fallbackBuffer[3] != 0))
					return fallbackBuffer;
				Thread.Sleep(5);
			}
			return null;
		}
	}
}
