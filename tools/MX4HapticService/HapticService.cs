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

		// Pass-through mode (forwards real controller input to virtual)
		private Thread _passThroughThread;
		private volatile Boolean _realControllerConnected = false;
		private UInt32 _realControllerIndex = 0;

		// Haptics enable/disable
		private volatile Boolean _hapticsEnabled = true;
		public Boolean HapticsEnabled
		{
			get => this._hapticsEnabled;
			set
			{
				this._hapticsEnabled = value;
				if (!value && this._hidppDevice?.IsConnected == true)
				{
					this._hidppDevice.SetHapticLevel(0);
					this._lastHapticLevel = 0;
				}
				this.StatusChanged?.Invoke(value ? "Haptics enabled" : "Haptics disabled");
			}
		}

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
		public Boolean IsRealControllerConnected => this._realControllerConnected;

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

				// Start pass-through thread (auto-detects real controller)
				this.StartPassThroughThread();

				// Watch config file for changes
				this.StartConfigWatcher();

				var preset = this._config.CurrentPreset;
				this.StatusChanged?.Invoke($"Running: preset={this._config.ActivePreset}, mode={preset.Mode}, waveform={preset.Waveform}");

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
			this.StopPassThroughThread();
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
			var preset = this._config.CurrentPreset;
			this.StatusChanged?.Invoke($"Config reloaded: preset={this._config.ActivePreset}, mode={preset.Mode}");
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

		#region Pass-Through Mode

		/// <summary>
		/// Starts the pass-through thread that forwards real controller input to virtual.
		/// </summary>
		private void StartPassThroughThread()
		{
			this._passThroughThread = new Thread(this.PassThroughLoop)
			{
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal,
				Name = "PassThroughThread"
			};
			this._passThroughThread.Start();
		}

		/// <summary>
		/// Stops the pass-through thread.
		/// </summary>
		private void StopPassThroughThread()
		{
			this._passThroughThread?.Join(500);
			this._passThroughThread = null;
		}

		/// <summary>
		/// Detects if a real XInput controller is connected.
		/// Returns the index of the first connected controller, or -1 if none.
		/// </summary>
		private Int32 DetectRealController()
		{
			// Check all 4 possible XInput slots
			// Skip slot 0 if our virtual controller is there (it usually is)
			for (UInt32 i = 1; i < 4; i++)
			{
				var state = new NativeMethods.XINPUT_STATE();
				if (NativeMethods.XInputGetState(i, ref state) == NativeMethods.ERROR_SUCCESS)
				{
					return (Int32)i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Pass-through loop: reads real controller and forwards to virtual.
		/// </summary>
		private void PassThroughLoop()
		{
			var lastPacketNumber = UInt32.MaxValue;
			var checkInterval = 0;

			while (this._running)
			{
				try
				{
					// Periodically check for real controller connection
					if (checkInterval++ % 100 == 0)
					{
						var realIndex = this.DetectRealController();
						var wasConnected = this._realControllerConnected;

						if (realIndex >= 0)
						{
							this._realControllerIndex = (UInt32)realIndex;
							this._realControllerConnected = true;

							if (!wasConnected)
							{
								this.StopActivitySimulator();
								this.StatusChanged?.Invoke($"Real controller detected at index {realIndex} - pass-through active");
							}
						}
						else
						{
							this._realControllerConnected = false;

							if (wasConnected)
							{
								this.StartActivitySimulator();
								this.StatusChanged?.Invoke("Real controller disconnected - activity simulator active");
							}
						}
					}

					// If real controller connected, forward its input
					if (this._realControllerConnected && this._virtualController != null)
					{
						var state = new NativeMethods.XINPUT_STATE();
						if (NativeMethods.XInputGetState(this._realControllerIndex, ref state) == NativeMethods.ERROR_SUCCESS)
						{
							// Only update if state changed
							if (state.dwPacketNumber != lastPacketNumber)
							{
								lastPacketNumber = state.dwPacketNumber;
								this.ForwardInputToVirtual(state.Gamepad);
							}
						}
					}

					Thread.Sleep(5); // ~200Hz polling
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
		/// Forwards gamepad input from real controller to virtual controller.
		/// </summary>
		private void ForwardInputToVirtual(NativeMethods.XINPUT_GAMEPAD gamepad)
		{
			if (this._virtualController == null) return;

			try
			{
				// Forward axes
				this._virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, gamepad.sThumbLX);
				this._virtualController.SetAxisValue(Xbox360Axis.LeftThumbY, gamepad.sThumbLY);
				this._virtualController.SetAxisValue(Xbox360Axis.RightThumbX, gamepad.sThumbRX);
				this._virtualController.SetAxisValue(Xbox360Axis.RightThumbY, gamepad.sThumbRY);

				// Forward triggers (convert byte 0-255 to slider value)
				this._virtualController.SetSliderValue(Xbox360Slider.LeftTrigger, gamepad.bLeftTrigger);
				this._virtualController.SetSliderValue(Xbox360Slider.RightTrigger, gamepad.bRightTrigger);

				// Forward buttons
				this.ForwardButtons(gamepad.wButtons);
			}
			catch { }
		}

		/// <summary>
		/// Forwards button states from real to virtual controller.
		/// </summary>
		private void ForwardButtons(UInt16 buttons)
		{
			// XInput button masks
			const UInt16 DPAD_UP = 0x0001;
			const UInt16 DPAD_DOWN = 0x0002;
			const UInt16 DPAD_LEFT = 0x0004;
			const UInt16 DPAD_RIGHT = 0x0008;
			const UInt16 START = 0x0010;
			const UInt16 BACK = 0x0020;
			const UInt16 LEFT_THUMB = 0x0040;
			const UInt16 RIGHT_THUMB = 0x0080;
			const UInt16 LEFT_SHOULDER = 0x0100;
			const UInt16 RIGHT_SHOULDER = 0x0200;
			const UInt16 A = 0x1000;
			const UInt16 B = 0x2000;
			const UInt16 X = 0x4000;
			const UInt16 Y = 0x8000;

			this._virtualController.SetButtonState(Xbox360Button.Up, (buttons & DPAD_UP) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Down, (buttons & DPAD_DOWN) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Left, (buttons & DPAD_LEFT) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Right, (buttons & DPAD_RIGHT) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Start, (buttons & START) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Back, (buttons & BACK) != 0);
			this._virtualController.SetButtonState(Xbox360Button.LeftThumb, (buttons & LEFT_THUMB) != 0);
			this._virtualController.SetButtonState(Xbox360Button.RightThumb, (buttons & RIGHT_THUMB) != 0);
			this._virtualController.SetButtonState(Xbox360Button.LeftShoulder, (buttons & LEFT_SHOULDER) != 0);
			this._virtualController.SetButtonState(Xbox360Button.RightShoulder, (buttons & RIGHT_SHOULDER) != 0);
			this._virtualController.SetButtonState(Xbox360Button.A, (buttons & A) != 0);
			this._virtualController.SetButtonState(Xbox360Button.B, (buttons & B) != 0);
			this._virtualController.SetButtonState(Xbox360Button.X, (buttons & X) != 0);
			this._virtualController.SetButtonState(Xbox360Button.Y, (buttons & Y) != 0);
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
						// Skip haptic output if disabled
						if (!this._hapticsEnabled)
						{
							Thread.Sleep(10);
							continue;
						}

						Byte hapticLevel;
						String waveformName;
						Int32 intervalMs;

						lock (this._configLock)
						{
							if (this._config.EnablePresetMode)
							{
								// New preset mode - uses dual motor simulation
								var preset = this._config.CurrentPreset;
								waveformName = preset.Waveform;
								hapticLevel = preset.CalculateIntensity(left, right);
								intervalMs = preset.CalculateInterval(left, right);
							}
#pragma warning disable CS0618
							else if (this._config.EnableSimpleMode)
							{
								// Legacy simple mode
								Byte motorIntensity = Math.Max(left, right);
								waveformName = this._config.SimpleWaveform;
								hapticLevel = this._config.CalculateSimpleHapticLevel(motorIntensity);
								intervalMs = this._config.CalculateSimpleInterval(motorIntensity);
							}
#pragma warning restore CS0618
							else
							{
								// Legacy PDM mode
								Byte motorIntensity = Math.Max(left, right);
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
	/// Native XInput methods for controller input and vibration.
	/// </summary>
	internal static class NativeMethods
	{
		public const UInt32 ERROR_SUCCESS = 0;
		public const UInt32 ERROR_DEVICE_NOT_CONNECTED = 1167;

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_VIBRATION
		{
			public UInt16 wLeftMotorSpeed;
			public UInt16 wRightMotorSpeed;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_GAMEPAD
		{
			public UInt16 wButtons;
			public Byte bLeftTrigger;
			public Byte bRightTrigger;
			public Int16 sThumbLX;
			public Int16 sThumbLY;
			public Int16 sThumbRX;
			public Int16 sThumbRY;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_STATE
		{
			public UInt32 dwPacketNumber;
			public XINPUT_GAMEPAD Gamepad;
		}

		[DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
		public static extern UInt32 XInputSetState(UInt32 dwUserIndex, ref XINPUT_VIBRATION pVibration);

		[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
		public static extern UInt32 XInputGetState(UInt32 dwUserIndex, ref XINPUT_STATE pState);

		public static UInt32 XInputSetState(UInt32 dwUserIndex, XINPUT_VIBRATION vibration)
		{
			return XInputSetState(dwUserIndex, ref vibration);
		}
	}
}
