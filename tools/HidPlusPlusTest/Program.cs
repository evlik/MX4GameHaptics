using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Direct HID++ test for MX Master 4 haptic control.
/// </summary>
class Program
{
	const ushort LogitechVendorId = 0x046D;
	const ushort FeatureHaptic = 0x0B4E;

	static void Main(string[] args)
	{
		Console.WriteLine("=== HID++ Direct Haptic Test v2 ===\n");

		// First, try to stop Logi plugin service temporarily
		Console.WriteLine("Note: If Logi Options+ is blocking, try closing it.\n");

		Console.WriteLine("Searching for Logitech devices...\n");

		var devices = EnumerateLogitechDevices();

		if (devices.Count == 0)
		{
			Console.WriteLine("No Logitech HID devices found!");
			return;
		}

		Console.WriteLine($"Found {devices.Count} Logitech device(s).\n");

		Console.WriteLine("Trying to find haptic interface...\n");

		foreach (var (path, pid) in devices)
		{
			Console.WriteLine($"PID: 0x{pid:X4} - {path.Substring(0, Math.Min(50, path.Length))}...");

			var handle = OpenDevice(path);
			if (handle == IntPtr.Zero)
			{
				Console.WriteLine("  -> Cannot open");
				continue;
			}

			// Get preparsed data to understand report sizes
			if (HidD_GetPreparsedData(handle, out IntPtr preparsedData))
			{
				HidP_GetCaps(preparsedData, out HIDP_CAPS caps);
				Console.WriteLine($"  -> InputLen:{caps.InputReportByteLength}, OutputLen:{caps.OutputReportByteLength}, FeatureLen:{caps.FeatureReportByteLength}");
				HidD_FreePreparsedData(preparsedData);

				// Try with correct output report size
				if (caps.OutputReportByteLength >= 7)
				{
					byte hapticIndex = GetFeatureIndex(handle, FeatureHaptic, (int)caps.OutputReportByteLength);

					if (hapticIndex != 0)
					{
						Console.WriteLine($"  -> HAPTIC FOUND at index {hapticIndex}!");

						// Досліджуємо ВСІ функції feature 0x0B4E
						Console.WriteLine("\n=== Досліджуємо функції Haptic Feature ===\n");

						for (byte func = 0; func < 8; func++)
						{
							Console.WriteLine($"Function {func}:");
							var response = CallFunction(handle, hapticIndex, func, new byte[] { 0, 0, 0 }, (int)caps.OutputReportByteLength);
							if (response != null)
							{
								Console.WriteLine($"  Response: {BitConverter.ToString(response)}");
							}
							else
							{
								Console.WriteLine($"  No response or error");
							}
							Thread.Sleep(100);
						}

						// Спробуємо різні параметри для функції 0 (можливо це SetMotor?)
						Console.WriteLine("\n=== Тест Function 0 з різними параметрами ===\n");

						// Можливо параметри: intensity, duration, frequency?
						byte[][] testParams = new byte[][]
						{
							new byte[] { 255, 0, 0 },      // Max перший параметр
							new byte[] { 0, 255, 0 },      // Max другий параметр
							new byte[] { 0, 0, 255 },      // Max третій параметр
							new byte[] { 255, 255, 0 },    // Max перші два
							new byte[] { 255, 100, 50 },   // Комбінація
							new byte[] { 128, 128, 128 },  // Середні значення
						};

						foreach (var param in testParams)
						{
							Console.WriteLine($"Params [{param[0]}, {param[1]}, {param[2]}]:");
							var response = CallFunction(handle, hapticIndex, 0, param, (int)caps.OutputReportByteLength);
							if (response != null)
							{
								Console.WriteLine($"  Response: {BitConverter.ToString(response)}");
							}
							Console.WriteLine("  Відчув щось? (чекаємо 1 сек)");
							Thread.Sleep(1000);
						}

						// Тепер спробуємо функцію 1 (play pattern) з параметрами інтенсивності
						Console.WriteLine("\n=== Pattern 0 з різною інтенсивністю ===\n");
						for (byte intensity = 50; intensity <= 255; intensity += 50)
						{
							Console.WriteLine($"Pattern 0, Intensity byte: {intensity}");
							var packet = new byte[(int)caps.OutputReportByteLength];
							packet[0] = 0x10;
							packet[1] = 0xFF;
							packet[2] = hapticIndex;
							packet[3] = 0x11;  // Function 1 | SW ID 1
							packet[4] = 0;     // Pattern 0
							packet[5] = intensity;  // Можливо інтенсивність?
							HidD_SetOutputReport(handle, packet, packet.Length);
							Thread.Sleep(500);
						}

						Console.WriteLine("\nДослідження завершено!");
						CloseDevice(handle);
						return;
					}
				}
			}

			CloseDevice(handle);
		}

		Console.WriteLine("\nHaptic feature not found on any device.");
	}

	static byte[] CallFunction(IntPtr handle, byte featureIndex, byte functionId, byte[] parameters, int reportSize)
	{
		byte[] packet = new byte[reportSize];
		packet[0] = 0x10;  // Short report
		packet[1] = 0xFF;  // Device index
		packet[2] = featureIndex;
		packet[3] = (byte)((functionId << 4) | 0x01);  // Function ID | SW ID

		// Copy parameters
		for (int i = 0; i < parameters.Length && i + 4 < reportSize; i++)
		{
			packet[4 + i] = parameters[i];
		}

		if (!HidD_SetOutputReport(handle, packet, packet.Length))
		{
			if (!WriteFile(handle, packet, (uint)packet.Length, out _, IntPtr.Zero))
			{
				return null;
			}
		}

		Thread.Sleep(50);

		// Read response
		byte[] response = new byte[reportSize];
		if (HidD_GetInputReport(handle, response, response.Length))
		{
			return response;
		}

		if (ReadFile(handle, response, (uint)response.Length, out uint bytesRead, IntPtr.Zero) && bytesRead > 0)
		{
			return response;
		}

		return null;
	}

	static byte GetFeatureIndex(IntPtr handle, ushort featureId, int reportSize)
	{
		// Build proper sized buffer
		byte[] packet = new byte[reportSize];
		packet[0] = 0x10;  // Short report ID
		packet[1] = 0xFF;  // Device index (0xFF for direct)
		packet[2] = 0x00;  // IRoot feature index
		packet[3] = 0x01;  // GetFeature function | SW ID
		packet[4] = (byte)(featureId >> 8);
		packet[5] = (byte)(featureId & 0xFF);

		// Try SetOutputReport
		if (!HidD_SetOutputReport(handle, packet, packet.Length))
		{
			// Fallback to WriteFile
			if (!WriteFile(handle, packet, (uint)packet.Length, out _, IntPtr.Zero))
			{
				Console.WriteLine("  -> Write failed");
				return 0;
			}
		}

		Thread.Sleep(50);

		// Read response
		byte[] response = new byte[reportSize];
		if (HidD_GetInputReport(handle, response, response.Length))
		{
			Console.WriteLine($"  -> Response: {BitConverter.ToString(response.Take(10).ToArray())}");
			if (response[4] != 0)
			{
				return response[4];
			}
		}

		// Try ReadFile
		if (ReadFile(handle, response, (uint)response.Length, out uint bytesRead, IntPtr.Zero) && bytesRead > 4)
		{
			Console.WriteLine($"  -> ReadFile response: {BitConverter.ToString(response.Take(10).ToArray())}");
			return response[4];
		}

		return 0;
	}

	static bool SendHaptic(IntPtr handle, byte featureIndex, byte pattern, int reportSize)
	{
		byte[] packet = new byte[reportSize];
		packet[0] = 0x10;  // Short report
		packet[1] = 0xFF;  // Device index
		packet[2] = featureIndex;
		packet[3] = 0x01;  // Function 0 | SW ID 1
		packet[4] = pattern;

		// Try HidD_SetOutputReport first (more reliable)
		if (HidD_SetOutputReport(handle, packet, packet.Length))
		{
			return true;
		}

		// Fallback to WriteFile
		return WriteFile(handle, packet, (uint)packet.Length, out _, IntPtr.Zero);
	}

	#region HID API

	[DllImport("hid.dll")]
	static extern void HidD_GetHidGuid(out Guid hidGuid);

	[DllImport("hid.dll")]
	static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsedData);

	[DllImport("hid.dll")]
	static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

	[DllImport("hid.dll")]
	static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

	[DllImport("hid.dll")]
	static extern bool HidD_SetOutputReport(IntPtr handle, byte[] data, int length);

	[DllImport("hid.dll")]
	static extern bool HidD_GetInputReport(IntPtr handle, byte[] data, int length);

	[DllImport("hid.dll")]
	static extern bool HidD_GetAttributes(IntPtr handle, ref HIDD_ATTRIBUTES attributes);

	[DllImport("setupapi.dll", SetLastError = true)]
	static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

	[DllImport("setupapi.dll", SetLastError = true)]
	static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

	[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
	static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

	[DllImport("setupapi.dll")]
	static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

	[DllImport("kernel32.dll")]
	static extern bool WriteFile(IntPtr handle, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

	[DllImport("kernel32.dll")]
	static extern bool ReadFile(IntPtr handle, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

	[DllImport("kernel32.dll")]
	static extern bool CloseHandle(IntPtr handle);

	[StructLayout(LayoutKind.Sequential)]
	struct SP_DEVICE_INTERFACE_DATA
	{
		public uint cbSize;
		public Guid InterfaceClassGuid;
		public uint Flags;
		public IntPtr Reserved;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct HIDD_ATTRIBUTES
	{
		public uint Size;
		public ushort VendorID;
		public ushort ProductID;
		public ushort VersionNumber;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct HIDP_CAPS
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

	const uint DIGCF_PRESENT = 0x02;
	const uint DIGCF_DEVICEINTERFACE = 0x10;
	const uint GENERIC_READ = 0x80000000;
	const uint GENERIC_WRITE = 0x40000000;
	const uint FILE_SHARE_READ = 0x01;
	const uint FILE_SHARE_WRITE = 0x02;
	const uint OPEN_EXISTING = 3;

	static List<(string path, ushort pid)> EnumerateLogitechDevices()
	{
		var result = new List<(string, ushort)>();
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
							if (HidD_GetAttributes(handle, ref attrs) && attrs.VendorID == LogitechVendorId)
							{
								result.Add((devicePath, attrs.ProductID));
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

	static IntPtr OpenDevice(string path)
	{
		var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
		return handle == new IntPtr(-1) ? IntPtr.Zero : handle;
	}

	static void CloseDevice(IntPtr handle)
	{
		if (handle != IntPtr.Zero) CloseHandle(handle);
	}

	#endregion
}
