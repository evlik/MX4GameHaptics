namespace Loupedeck.LogiHapticPlugin
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.IO.Pipes;
	using System.Text;
	using System.Text.Json;
	using System.Threading;
	using Nefarius.ViGEm.Client;
	using Nefarius.ViGEm.Client.Targets;
	using Nefarius.ViGEm.Client.Targets.Xbox360;

	/// <summary>
	/// Loupedeck plugin that captures game vibration and triggers haptic feedback on MX Master 4.
	/// Configuration via external UI through Named pipe.
	/// </summary>
	public class LogiHapticPlugin : Plugin
	{
		private static readonly List<String> HapticWaveforms = new List<String>
		{
			"sharp_collision", "sharp_state_change", "knock", "damp_collision",
			"mad", "ringing", "subtle_collision", "completed", "jingle",
			"damp_state_change", "firework", "happy_alert", "wave", "angry_alert", "square"
		};

		// Configuration
		private HapticConfig _config;
		private readonly Object _configLock = new Object();

		// ViGEm virtual controller
		private ViGEmClient _vigemClient;
		private IXbox360Controller _virtualController;

		// Activity simulator
		private Timer _activityTimer;
		private Boolean _activityToggle = false;

		// Vibration state
		private Byte _currentLeftMotor = 0;
		private Byte _currentRightMotor = 0;
		private readonly Object _motorLock = new Object();

		// High-frequency vibration thread
		private Thread _vibrationThread;
		private volatile Boolean _running = false;

		// Named pipe server for configuration commands
		private Thread _pipeServerThread;
		private volatile Boolean _pipeRunning = false;

		// Direct HID++ device for smooth haptics
		private HidPlusPlusDevice _hidppDevice;
		private Boolean _useDirectHidpp = true;  // Toggle between HID++ and Loupedeck API
		private Byte _lastHapticLevel = 0;  // Cache to avoid redundant SetLevel calls

		public override Boolean UsesApplicationApiOnly => true;
		public override Boolean HasNoApplication => true;

		public LogiHapticPlugin()
		{
			PluginLog.Init(this.Log);
			PluginResources.Init(this.Assembly);
		}

		public override void Load()
		{
			this._config = HapticConfig.Load();
			this.RegisterHapticEvents();
			this.StartPipeServer();
			this.StartVirtualController();

			// Try to connect via direct HID++
			this._hidppDevice = new HidPlusPlusDevice();
			if (this._hidppDevice.Connect())
			{
				PluginLog.Info("Direct HID++ haptic control enabled - SMOOTH MODE!");
				this._useDirectHidpp = true;
			}
			else
			{
				PluginLog.Warning("HID++ not available, falling back to Loupedeck API");
				this._useDirectHidpp = false;
			}

			this.StartVibrationThread();
			PluginLog.Info($"LogiHapticPlugin loaded - interval: {this._config.PulseIntervalMs}ms");
		}

		public override void Unload()
		{
			this.StopVibrationThread();
			this.StopPipeServer();
			this.StopActivitySimulator();
			this.StopVirtualController();

			this._hidppDevice?.Dispose();
			this._hidppDevice = null;

			PluginLog.Info("LogiHapticPlugin unloaded");
		}

		private void RegisterHapticEvents()
		{
			foreach (var waveform in HapticWaveforms)
			{
				this.PluginEvents.AddEvent(waveform, waveform, null);
			}
			PluginLog.Info($"Registered {HapticWaveforms.Count} haptic waveforms");
		}

		#region Named Pipe Server

		/// <summary>
		/// Starts named pipe server for receiving commands from HapticConfigurator.
		/// Commands:
		/// - TEST:waveform_name - trigger single waveform
		/// - CONFIG:{json} - update configuration
		/// - RELOAD - reload config from file
		/// </summary>
		private void StartPipeServer()
		{
			this._pipeRunning = true;
			this._pipeServerThread = new Thread(this.PipeServerLoop)
			{
				IsBackground = true,
				Name = "HapticConfigPipe"
			};
			this._pipeServerThread.Start();
			PluginLog.Info("Config pipe server started");
		}

		private void StopPipeServer()
		{
			this._pipeRunning = false;
			this._pipeServerThread?.Join(1000);
		}

		private void PipeServerLoop()
		{
			while (this._pipeRunning)
			{
				try
				{
					using (var server = new NamedPipeServerStream(
						HapticConfig.PipeName,
						PipeDirection.InOut,
						NamedPipeServerStream.MaxAllowedServerInstances,  // Allow multiple connections
						PipeTransmissionMode.Message,
						PipeOptions.Asynchronous))
					{
						// Wait for connection with timeout
						var asyncResult = server.BeginWaitForConnection(null, null);
						while (!asyncResult.IsCompleted && this._pipeRunning)
						{
							Thread.Sleep(100);
						}

						if (!this._pipeRunning)
						{
							break;
						}

						server.EndWaitForConnection(asyncResult);

						// Read command
						var buffer = new Byte[4096];
						var bytesRead = server.Read(buffer, 0, buffer.Length);
						if (bytesRead > 0)
						{
							var command = Encoding.UTF8.GetString(buffer, 0, bytesRead);
							var response = this.ProcessCommand(command);

							// Send response
							var responseBytes = Encoding.UTF8.GetBytes(response);
							server.Write(responseBytes, 0, responseBytes.Length);
						}
					}
				}
				catch (Exception ex)
				{
					if (this._pipeRunning)
					{
						PluginLog.Warning(ex, "Pipe server error");
						Thread.Sleep(500);
					}
				}
			}
		}

		private String ProcessCommand(String command)
		{
			try
			{
				if (command.StartsWith("TEST:"))
				{
					var waveform = command.Substring(5).Trim();
					this.TriggerHaptic(waveform);
					return "OK";
				}

				if (command.StartsWith("CONFIG:"))
				{
					var json = command.Substring(7);
					var newConfig = JsonSerializer.Deserialize<HapticConfig>(json);
					if (newConfig != null)
					{
						lock (this._configLock)
						{
							this._config = newConfig;
						}
						PluginLog.Info("Config updated via pipe");
						return "OK";
					}
					return "ERROR:Invalid config";
				}

				if (command == "RELOAD")
				{
					lock (this._configLock)
					{
						this._config = HapticConfig.Load();
					}
					PluginLog.Info("Config reloaded from file");
					return "OK";
				}

				if (command == "GET")
				{
					lock (this._configLock)
					{
						return JsonSerializer.Serialize(this._config);
					}
				}

				return "ERROR:Unknown command";
			}
			catch (Exception ex)
			{
				return $"ERROR:{ex.Message}";
			}
		}

		#endregion

		#region ViGEm Virtual Controller

		private void StartVirtualController()
		{
			try
			{
				this._vigemClient = new ViGEmClient();
				this._virtualController = this._vigemClient.CreateXbox360Controller();
				this._virtualController.FeedbackReceived += this.OnVibrationReceived;
				this._virtualController.Connect();
				this.StartActivitySimulator();
				PluginLog.Info("Virtual Xbox 360 controller connected");
			}
			catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException)
			{
				PluginLog.Warning("ViGEmBus not installed");
			}
			catch (Exception ex)
			{
				PluginLog.Warning(ex, "Failed to create virtual controller");
			}
		}

		private void StartActivitySimulator()
		{
			this._activityTimer = new Timer(this.SimulateActivity, null, 0, 100);
		}

		private void StopActivitySimulator()
		{
			this._activityTimer?.Dispose();
			this._activityTimer = null;
		}

		private void SimulateActivity(Object state)
		{
			if (this._virtualController == null) return;

			try
			{
				Int16 value = this._activityToggle ? (Int16)1500 : (Int16)(-1500);
				this._activityToggle = !this._activityToggle;
				this._virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, value);
				Thread.Sleep(10);
				this._virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
			}
			catch { }
		}

		private void StopVirtualController()
		{
			try
			{
				if (this._virtualController != null)
				{
					this._virtualController.FeedbackReceived -= this.OnVibrationReceived;
					this._virtualController.Disconnect();
					this._virtualController = null;
				}
				this._vigemClient?.Dispose();
				this._vigemClient = null;
			}
			catch (Exception ex)
			{
				PluginLog.Warning(ex, "Error stopping virtual controller");
			}
		}

		private void OnVibrationReceived(Object sender, Xbox360FeedbackReceivedEventArgs e)
		{
			lock (this._motorLock)
			{
				this._currentLeftMotor = e.LargeMotor;
				this._currentRightMotor = e.SmallMotor;
			}
		}

		#endregion

		#region High-Frequency Vibration Thread

		private void StartVibrationThread()
		{
			this._running = true;
			this._vibrationThread = new Thread(this.VibrationLoop)
			{
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal,
				Name = "HapticVibrationThread"
			};
			this._vibrationThread.Start();
			PluginLog.Info($"Vibration thread started (interval: {this._config.PulseIntervalMs}ms)");
		}

		private void StopVibrationThread()
		{
			this._running = false;
			this._vibrationThread?.Join(500);
			this._vibrationThread = null;
		}

		/// <summary>
		/// High-frequency loop that sends haptic pulses.
		/// Supports two modes:
		/// - Simple Mode: Single waveform + direct HID++ intensity + frequency
		/// - PDM Mode: Multiple waveforms zones with pulse density modulation
		/// </summary>
		private void VibrationLoop()
		{
			var sw = Stopwatch.StartNew();

			while (this._running)
			{
				try
				{
					Byte left, right;
					lock (this._motorLock)
					{
						left = this._currentLeftMotor;
						right = this._currentRightMotor;
					}

					// If any motor is active, pulse!
					if (left > 0 || right > 0)
					{
						Byte motorIntensity = Math.Max(left, right);
						Byte hapticLevel;
						String waveformName;
						Int32 intervalMs;

						lock (this._configLock)
						{
							if (this._config.EnableSimpleMode)
							{
								// SIMPLE MODE: Single waveform + direct intensity + frequency
								waveformName = this._config.SimpleWaveform;
								hapticLevel = this._config.CalculateSimpleHapticLevel(motorIntensity);
								intervalMs = this._config.CalculateSimpleInterval(motorIntensity);
							}
							else
							{
								// PDM MODE: Multiple waveforms with zone-based intervals
								waveformName = this._config.SelectWaveform(motorIntensity);
								hapticLevel = (Byte)(motorIntensity * 100 / 255);
								intervalMs = this._config.CalculateDynamicInterval(motorIntensity) / 10;
							}
						}

						if (this._useDirectHidpp && this._hidppDevice?.IsConnected == true)
						{
							// Direct HID++: Set intensity level, then play waveform
							// Only update level if changed (reduces USB traffic)
							if (hapticLevel != this._lastHapticLevel)
							{
								this._hidppDevice.SetHapticLevel(hapticLevel);
								this._lastHapticLevel = hapticLevel;
							}

							var waveformId = this.MapWaveformNameToId(waveformName);
							this._hidppDevice.PlayWaveform(waveformId);
						}
						else
						{
							// Fallback to Loupedeck API
							this.TriggerHapticLegacy(waveformName);
						}

						// Precise delay based on interval
						var targetTicks = intervalMs * Stopwatch.Frequency / 1000;
						sw.Restart();
						while (sw.ElapsedTicks < targetTicks && this._running)
						{
							Thread.SpinWait(10);
						}
					}
					else
					{
						// No vibration - reset level and sleep
						if (this._lastHapticLevel != 0 && this._useDirectHidpp)
						{
							this._hidppDevice?.SetHapticLevel(0);
							this._lastHapticLevel = 0;
						}
						Thread.Sleep(10);
					}
				}
				catch (ThreadInterruptedException)
				{
					break;
				}
				catch
				{
					// Continue on errors
				}
			}
		}

		/// <summary>
		/// Maps waveform name string to HapticWaveform enum value.
		/// </summary>
		private Byte MapWaveformNameToId(String name)
		{
			return name?.ToLowerInvariant() switch
			{
				"sharp_state_change" => (Byte)HapticWaveform.SharpStateChange,
				"damp_state_change" => (Byte)HapticWaveform.DampStateChange,
				"sharp_collision" => (Byte)HapticWaveform.SharpCollision,
				"damp_collision" => (Byte)HapticWaveform.DampCollision,
				"subtle_collision" => (Byte)HapticWaveform.SubtleCollision,
				"happy_alert" => (Byte)HapticWaveform.HappyAlert,
				"angry_alert" => (Byte)HapticWaveform.AngryAlert,
				"completed" => (Byte)HapticWaveform.Completed,
				"square" => (Byte)HapticWaveform.Square,
				"wave" => (Byte)HapticWaveform.Wave,
				"firework" => (Byte)HapticWaveform.Firework,
				"mad" => (Byte)HapticWaveform.Mad,
				"knock" => (Byte)HapticWaveform.Knock,
				"jingle" => (Byte)HapticWaveform.Jingle,
				"ringing" => (Byte)HapticWaveform.Ringing,
				"whisper_collision" => (Byte)HapticWaveform.WhisperCollision,
				_ => (Byte)HapticWaveform.Wave
			};
		}

		#endregion

		#region Haptic Output

		/// <summary>
		/// Triggers haptic via direct HID++ or falls back to Loupedeck API.
		/// </summary>
		private void TriggerHaptic(String waveformName)
		{
			if (this._useDirectHidpp && this._hidppDevice?.IsConnected == true)
			{
				var waveformId = this.MapWaveformNameToId(waveformName);
				this._hidppDevice.PlayWaveform(waveformId);
			}
			else
			{
				this.TriggerHapticLegacy(waveformName);
			}
		}

		/// <summary>
		/// Legacy haptic trigger via Loupedeck API.
		/// </summary>
		private void TriggerHapticLegacy(String waveformName)
		{
			try
			{
				this.PluginEvents.RaiseEvent(waveformName);
			}
			catch
			{
				// Ignore errors for maximum speed
			}
		}

		#endregion
	}
}
