using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Test app to send vibration to all XInput controllers and verify which one responds.
/// </summary>
class Program
{
	[StructLayout(LayoutKind.Sequential)]
	struct XINPUT_VIBRATION
	{
		public ushort wLeftMotorSpeed;
		public ushort wRightMotorSpeed;
	}

	[DllImport("xinput1_4.dll")]
	static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

	[DllImport("xinput1_4.dll")]
	static extern uint XInputGetState(uint dwUserIndex, byte[] pState);

	const uint ERROR_SUCCESS = 0;
	const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

	static void Main(string[] args)
	{
		Console.WriteLine("=== ViGEm Controller Test ===\n");

		// Check which controllers are connected
		Console.WriteLine("Scanning XInput controllers (0-3):\n");

		byte[] state = new byte[16];
		for (uint i = 0; i < 4; i++)
		{
			uint result = XInputGetState(i, state);
			string status = result == ERROR_SUCCESS ? "CONNECTED" : "not connected";
			Console.WriteLine($"  Controller {i}: {status}");
		}

		Console.WriteLine("\nSending vibration to ALL connected controllers...");
		Console.WriteLine("Check if your mouse vibrates!\n");

		// Send vibration to all controllers
		var vibration = new XINPUT_VIBRATION
		{
			wLeftMotorSpeed = 65535,
			wRightMotorSpeed = 65535
		};

		for (uint i = 0; i < 4; i++)
		{
			uint result = XInputSetState(i, ref vibration);
			if (result == ERROR_SUCCESS)
			{
				Console.WriteLine($"  Controller {i}: Vibration SENT (max intensity)");
			}
		}

		Console.WriteLine("\nWaiting 2 seconds...");
		Thread.Sleep(2000);

		// Stop vibration
		vibration.wLeftMotorSpeed = 0;
		vibration.wRightMotorSpeed = 0;

		for (uint i = 0; i < 4; i++)
		{
			XInputSetState(i, ref vibration);
		}

		Console.WriteLine("Vibration stopped.\n");
		Console.WriteLine("Did the mouse vibrate? Check the plugin log!");
	}
}
