using System;
using System.IO.Pipes;
using System.Text;

namespace HapticConfigurator
{
	/// <summary>
	/// Client for communicating with LogiHapticPlugin via Named Pipe.
	/// </summary>
	public static class PluginClient
	{
		private const Int32 TimeoutMs = 2000;

		/// <summary>
		/// Sends a command to the plugin and returns response.
		/// </summary>
		public static String SendCommand(String command)
		{
			try
			{
				using (var client = new NamedPipeClientStream(".", HapticConfig.PipeName, PipeDirection.InOut))
				{
					client.Connect(TimeoutMs);

					// Send command
					var commandBytes = Encoding.UTF8.GetBytes(command);
					client.Write(commandBytes, 0, commandBytes.Length);

					// Read response
					var buffer = new Byte[4096];
					var bytesRead = client.Read(buffer, 0, buffer.Length);
					return Encoding.UTF8.GetString(buffer, 0, bytesRead);
				}
			}
			catch (TimeoutException)
			{
				return "ERROR:Plugin not running";
			}
			catch (Exception ex)
			{
				return $"ERROR:{ex.Message}";
			}
		}

		/// <summary>
		/// Tests a specific waveform.
		/// </summary>
		public static Boolean TestWaveform(String waveform)
		{
			var response = SendCommand($"TEST:{waveform}");
			return response == "OK";
		}

		/// <summary>
		/// Applies new configuration to the running plugin.
		/// </summary>
		public static Boolean ApplyConfig(HapticConfig config)
		{
			var json = System.Text.Json.JsonSerializer.Serialize(config);
			var response = SendCommand($"CONFIG:{json}");
			return response == "OK";
		}

		/// <summary>
		/// Tells plugin to reload config from file.
		/// </summary>
		public static Boolean ReloadConfig()
		{
			var response = SendCommand("RELOAD");
			return response == "OK";
		}

		/// <summary>
		/// Checks if plugin is running.
		/// </summary>
		public static Boolean IsPluginRunning()
		{
			var response = SendCommand("GET");
			return !response.StartsWith("ERROR:");
		}
	}
}
