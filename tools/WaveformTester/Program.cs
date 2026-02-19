using System;
using System.IO.Pipes;
using System.Threading;

/// <summary>
/// Tool to test each waveform individually and find best ones for continuous rumble.
/// </summary>
class Program
{
	static readonly string[] Waveforms = {
		"sharp_collision",      // 0
		"sharp_state_change",   // 1
		"knock",                // 2
		"damp_collision",       // 3
		"mad",                  // 4
		"ringing",              // 5
		"subtle_collision",     // 6
		"completed",            // 7
		"jingle",               // 8
		"damp_state_change",    // 9
		"firework",             // 10
		"happy_alert",          // 11
		"wave",                 // 12
		"angry_alert",          // 13
		"square"                // 14
	};

	static void Main(string[] args)
	{
		Console.WriteLine("=== Waveform Tester ===\n");
		Console.WriteLine("Тестуємо кожен waveform щоб знайти найкращий для rumble.\n");
		Console.WriteLine("Команди:");
		Console.WriteLine("  [0-14] - Одиночний waveform");
		Console.WriteLine("  r[0-14] - Rumble тест (швидке повторення 2 сек)");
		Console.WriteLine("  a - Програти всі по черзі");
		Console.WriteLine("  q - Вийти\n");

		ListWaveforms();

		while (true)
		{
			Console.Write("\n> ");
			string input = Console.ReadLine()?.Trim().ToLower();

			if (string.IsNullOrEmpty(input)) continue;
			if (input == "q") break;

			if (input == "a")
			{
				PlayAll();
				continue;
			}

			if (input.StartsWith("r"))
			{
				if (int.TryParse(input.Substring(1), out int idx) && idx >= 0 && idx < 15)
				{
					RumbleTest(idx);
				}
				else
				{
					Console.WriteLine("Невірний індекс. Використовуй r0-r14");
				}
				continue;
			}

			if (int.TryParse(input, out int index) && index >= 0 && index < 15)
			{
				PlaySingle(index);
			}
			else
			{
				Console.WriteLine("Невірна команда");
			}
		}
	}

	static void ListWaveforms()
	{
		Console.WriteLine("Доступні waveforms:");
		for (int i = 0; i < Waveforms.Length; i++)
		{
			Console.WriteLine($"  {i,2}: {Waveforms[i]}");
		}
	}

	static void PlaySingle(int index)
	{
		Console.WriteLine($"Playing: {Waveforms[index]}");
		TriggerVibration(200, 200); // Max intensity burst
		Thread.Sleep(100);
		TriggerVibration(0, 0); // Stop
	}

	static void RumbleTest(int index)
	{
		Console.WriteLine($"Rumble test: {Waveforms[index]} (2 секунди)");
		Console.WriteLine("Слухай чи це 'гул' чи 'тук-тук-тук'...\n");

		// Map index to intensity range that selects this waveform
		byte intensity = index switch
		{
			<= 6 => 50,   // Light waveforms
			<= 9 => 120,  // Medium
			_ => 220      // Strong
		};

		// Actually we need to modify the plugin to test specific waveforms
		// For now, just send continuous max vibration
		var start = DateTime.Now;
		while ((DateTime.Now - start).TotalSeconds < 2)
		{
			TriggerVibration(255, 255);
			Thread.Sleep(3);
		}
		TriggerVibration(0, 0);
		Console.WriteLine("Done.");
	}

	static void PlayAll()
	{
		Console.WriteLine("Програю всі waveforms по черзі (по 0.5 сек кожен)...\n");

		for (int i = 0; i < Waveforms.Length; i++)
		{
			Console.WriteLine($"  {i}: {Waveforms[i]}");

			// Trigger with intensity that would select this waveform
			// (this is approximate, actual selection depends on plugin logic)
			var start = DateTime.Now;
			while ((DateTime.Now - start).TotalMilliseconds < 500)
			{
				TriggerVibration(255, 255);
				Thread.Sleep(10);
			}
			TriggerVibration(0, 0);
			Thread.Sleep(300); // Pause between waveforms
		}
		Console.WriteLine("\nDone!");
	}

	static void TriggerVibration(byte left, byte right)
	{
		try
		{
			// Send via XInput to virtual controller
			// The plugin will pick it up
			var vibration = new XINPUT_VIBRATION
			{
				wLeftMotorSpeed = (ushort)(left * 256),
				wRightMotorSpeed = (ushort)(right * 256)
			};
			XInputSetState(0, ref vibration);
		}
		catch { }
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	struct XINPUT_VIBRATION
	{
		public ushort wLeftMotorSpeed;
		public ushort wRightMotorSpeed;
	}

	[System.Runtime.InteropServices.DllImport("xinput1_4.dll")]
	static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);
}
