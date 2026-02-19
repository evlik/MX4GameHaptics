using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace MX4HapticService
{
	/// <summary>
	/// Core service that captures game vibration via ViGEm virtual controller
	/// and outputs haptic feedback to MX Master 4 via HID++.
	/// </summary>
	public class HapticService : IDisposable
	{
		private HapticConfig _config;
		private readonly Object _configLock = new Object();
		private FileSystemWatcher _configWatcher;

		// ViGEm virtual controller
		private ViGEmClient _vigemClient;
		private IXbox360Controller _virtualController;

		// Activity simulator (keeps controller "alive" for games)
		private Timer _activityTimer;
		private Boolean _activityToggle = false;

		// Motor state
		private Byte _currentLeftMotor = 0;
		private Byte _currentRightMotor = 0;
		private readonly Object _motorLock = new Object();

		// Vibration thread
		private Thread _vibrationThread;
		private volatile Boolean _running = false;

		// Direct HID++ device
		private HidPlusPlusDevice _hidppDevice;
		private Byte _lastHapticLevel = 0;

		// Status reporting
		public event Action<String> StatusChanged;
		public event Action<String> Error;

		// Vibration monitoring
		public event Action<Byte, Byte> MotorValuesChanged;
		public event Action<String, Byte, Int32> HapticOutputChanged;

		public Boolean IsRunning => this._running;
		public Boolean IsMouseConnected => this._hidppDevice?.IsConnected == true;
		public Boolean IsControllerConnected => this._virtualController != null;

		// For latency testing
		private Stopwatch _latencyStopwatch;
		private volatile Boolean _waitingForVibration = false;
		public Double LastLatencyMs { get; private set; } = 0;

		public HapticService()
		{
			this._config = HapticConfig.Load();
		}

		/// <summary>
		/// Starts the haptic service.
		/// </summary>
		public Boolean Start()
		{
			try
			{
				// Connect to mouse via HID++
				this._hidppDevice = new HidPlusPlusDevice();
				if (!this._hidppDevice.Connect())
				{
					this.Error?.Invoke("Failed to connect to MX Master 4. Make sure it's connected via Bluetooth or Logi Bolt receiver.");
					return false;
				}
				this.StatusChanged?.Invoke($"Mouse connected via {this._hidppDevice.ConnectionMode} (idx={this._hidppDevice.DeviceIndex:X2}, feature=0x{this._hidppDevice.HapticFeatureId:X4})");

				// Create virtual controller
				if (!this.StartVirtualController())
				{
					this.Error?.Invoke("Failed to create virtual controller. Make sure ViGEmBus is installed.");
					return false;
				}
				this.StatusChanged?.Invoke("Virtual Xbox 360 controller created");

				// Start vibration processing thread
				this.StartVibrationThread();

				// Watch config file for changes
				this.StartConfigWatcher();

				this.StatusChanged?.Invoke($"Running: {this._config.SimpleWaveform}, {this._config.SimpleIntervalMinMs}ms");

				return true;
			}
			catch (Exception ex)
			{
				this.Error?.Invoke($"Failed to start: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Stops the haptic service.
		/// </summary>
		public void Stop()
		{
			this.StopConfigWatcher();
			this.StopVibrationThread();
			this.StopActivitySimulator();
			this.StopVirtualController();

			this._hidppDevice?.Dispose();
			this._hidppDevice = null;

			this.StatusChanged?.Invoke("Service stopped");
		}

		/// <summary>
		/// Reloads configuration from file.
		/// </summary>
		public void ReloadConfig()
		{
			lock (this._configLock)
			{
				this._config = HapticConfig.Load();
			}
			this.StatusChanged?.Invoke($"Config reloaded: {this._config.SimpleWaveform}, {this._config.SimpleIntervalMinMs}ms, fixed={this._config.UseFixedInterval}");
		}

		/// <summary>
		/// Starts watching config file for changes.
		/// </summary>
		private void StartConfigWatcher()
		{
			try
			{
				var configDir = Path.GetDirectoryName(HapticConfig.ConfigPath);
				var configFile = Path.GetFileName(HapticConfig.ConfigPath);

				if (!Directory.Exists(configDir)) return;

				this._configWatcher = new FileSystemWatcher(configDir, configFile)
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
				};

				this._configWatcher.Changed += (s, e) =>
				{
					// Debounce - wait for file to be fully written
					Thread.Sleep(100);
					this.ReloadConfig();
				};

				this._configWatcher.EnableRaisingEvents = true;
			}
			catch { }
		}

		private void StopConfigWatcher()
		{
			this._configWatcher?.Dispose();
			this._configWatcher = null;
		}

		#region ViGEm Virtual Controller

		private Boolean StartVirtualController()
		{
			try
			{
				this._vigemClient = new ViGEmClient();
				this._virtualController = this._vigemClient.CreateXbox360Controller();
				this._virtualController.FeedbackReceived += this.OnVibrationReceived;
				this._virtualController.Connect();
				this.StartActivitySimulator();
				return true;
			}
			catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException)
			{
				this.Error?.Invoke("ViGEmBus driver not installed! Download from: https://github.com/nefarius/ViGEmBus/releases");
				return false;
			}
			catch (Exception ex)
			{
				this.Error?.Invoke($"Controller error: {ex.Message}");
				return false;
			}
		}

		private void StartActivitySimulator()
		{
			// Simulate small stick movements to keep controller "active" for games
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
			catch { }
		}

		private void OnVibrationReceived(Object sender, Xbox360FeedbackReceivedEventArgs e)
		{
			// Measure latency if we're testing
			if (this._waitingForVibration && e.LargeMotor > 0)
			{
				this._latencyStopwatch?.Stop();
				this.LastLatencyMs = this._latencyStopwatch?.Elapsed.TotalMilliseconds ?? 0;
				this._waitingForVibration = false;
				this.StatusChanged?.Invoke($"ViGEm latency: {this.LastLatencyMs:F1}ms");
			}

			lock (this._motorLock)
			{
				this._currentLeftMotor = e.LargeMotor;
				this._currentRightMotor = e.SmallMotor;
			}

			// Notify monitor
			this.MotorValuesChanged?.Invoke(e.LargeMotor, e.SmallMotor);
		}

		/// <summary>
		/// Tests ViGEm round-trip latency by triggering rumble via XInput.
		/// </summary>
		public void TestViGEmLatency()
		{
			if (this._virtualController == null)
			{
				this.Error?.Invoke("Controller not connected");
				return;
			}

			this.StatusChanged?.Invoke("Testing ViGEm latency...");

			// Start timing
			this._latencyStopwatch = Stopwatch.StartNew();
			this._waitingForVibration = true;

			// Trigger vibration via XInput API (simulates what a game does)
			new Thread(() =>
			{
				try
				{
					// Use XInput to send vibration to our virtual controller
					// This tests the full round-trip: XInput -> ViGEm -> our callback
					var result = NativeMethods.XInputSetState(0, new NativeMethods.XINPUT_VIBRATION
					{
						wLeftMotorSpeed = 65535,
						wRightMotorSpeed = 65535
					});

					Thread.Sleep(200);

					// Stop vibration
					NativeMethods.XInputSetState(0, new NativeMethods.XINPUT_VIBRATION
					{
						wLeftMotorSpeed = 0,
						wRightMotorSpeed = 0
					});

					if (!this._waitingForVibration)
					{
						// Already received - latency was measured
					}
					else
					{
						this._waitingForVibration = false;
						this.Error?.Invoke("No vibration received - is controller index 0?");
					}
				}
				catch (Exception ex)
				{
					this.Error?.Invoke($"XInput error: {ex.Message}");
				}
			})
			{ IsBackground = true }.Start();
		}

		#endregion

		#region Vibration Processing Thread

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
		}

		private void StopVibrationThread()
		{
			this._running = false;
			this._vibrationThread?.Join(500);
			this._vibrationThread = null;
		}

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
								waveformName = this._config.SimpleWaveform;
								hapticLevel = this._config.CalculateSimpleHapticLevel(motorIntensity);
								intervalMs = this._config.CalculateSimpleInterval(motorIntensity);
							}
							else
							{
								waveformName = this._config.SelectWaveform(motorIntensity);
								hapticLevel = (Byte)(motorIntensity * 100 / 255);
								intervalMs = this._config.CalculateDynamicInterval(motorIntensity) / 10;
							}
						}

						if (this._hidppDevice?.IsConnected == true)
						{
							if (hapticLevel != this._lastHapticLevel)
							{
								this._hidppDevice.SetHapticLevel(hapticLevel);
								this._lastHapticLevel = hapticLevel;
							}

							var waveformId = this.MapWaveformNameToId(waveformName);
							this._hidppDevice.PlayWaveform(waveformId);

							// Notify monitor
							this.HapticOutputChanged?.Invoke(waveformName, hapticLevel, intervalMs);
						}

						// Precise delay
						var targetTicks = intervalMs * Stopwatch.Frequency / 1000;
						sw.Restart();
						while (sw.ElapsedTicks < targetTicks && this._running)
						{
							Thread.SpinWait(10);
						}
					}
					else
					{
						// No vibration - reset level
						if (this._lastHapticLevel != 0)
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
				_ => (Byte)HapticWaveform.Wave
			};
		}

		#endregion

		public void Dispose()
		{
			this.Stop();
		}
	}

	/// <summary>
	/// Native XInput methods for latency testing.
	/// </summary>
	internal static class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_VIBRATION
		{
			public UInt16 wLeftMotorSpeed;
			public UInt16 wRightMotorSpeed;
		}

		[DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
		public static extern UInt32 XInputSetState(UInt32 dwUserIndex, ref XINPUT_VIBRATION pVibration);

		public static UInt32 XInputSetState(UInt32 dwUserIndex, XINPUT_VIBRATION vibration)
		{
			return XInputSetState(dwUserIndex, ref vibration);
		}
	}
}
