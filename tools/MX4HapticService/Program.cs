using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AutoUpdaterDotNET;

namespace MX4HapticService
{
	static class Program
	{
		// Update URL - points to XML file on GitHub or your server
		public const String UpdateUrl = "https://raw.githubusercontent.com/evlik/MX4GameHaptics/master/updates/update.xml";

		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new TrayApplicationContext());
		}
	}

	/// <summary>
	/// System tray application for MX4 Haptic Service.
	/// </summary>
	public class TrayApplicationContext : ApplicationContext
	{
		private NotifyIcon _trayIcon;
		private HapticService _service;
		private ToolStripMenuItem _statusItem;
		private ToolStripMenuItem _startStopItem;
		private ToolStripMenuItem _hapticsEnabledItem;
		private VibrationMonitorForm _monitorForm;

		public TrayApplicationContext()
		{
			this._service = new HapticService();
			this._service.StatusChanged += this.OnStatusChanged;
			this._service.Error += this.OnError;

			// Create monitor form and connect events
			this._monitorForm = new VibrationMonitorForm();
			this._service.MotorValuesChanged += (left, right) =>
				this._monitorForm.UpdateMotors(left, right);
			this._service.HapticOutputChanged += (waveform, level, interval) =>
				this._monitorForm.UpdateHapticOutput(waveform, level, interval);

			this.BuildTrayIcon();

			// Configure auto-updater
			this.ConfigureAutoUpdater();

			// Auto-start service
			this.StartService();

			// Check for updates silently on startup
			this.CheckForUpdates(silent: true);
		}

		private void ConfigureAutoUpdater()
		{
			AutoUpdater.Icon = CreateIcon().ToBitmap();
			AutoUpdater.ShowSkipButton = true;
			AutoUpdater.ShowRemindLaterButton = true;
			AutoUpdater.RunUpdateAsAdmin = true;
			AutoUpdater.ReportErrors = false; // Don't show errors for silent checks
			AutoUpdater.Synchronous = false;

			// Custom update message
			AutoUpdater.AppTitle = "MX4 Game Haptics";

			// Handle update events
			AutoUpdater.CheckForUpdateEvent += this.OnCheckForUpdateEvent;
		}

		private Boolean _manualUpdateCheck = false;

		private void OnCheckForUpdateEvent(UpdateInfoEventArgs args)
		{
			if (args.Error != null)
			{
				if (this._manualUpdateCheck)
				{
					MessageBox.Show($"Failed to check for updates:\n{args.Error.Message}",
						"Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			else if (args.IsUpdateAvailable)
			{
				var message = $"New version {args.CurrentVersion} is available!\n\n" +
					$"Current version: {args.InstalledVersion}\n\n" +
					"Would you like to download it now?";

				if (MessageBox.Show(message, "Update Available",
					MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
				{
					try
					{
						if (AutoUpdater.DownloadUpdate(args))
						{
							// Stop service and exit to allow update
							this._service.Stop();
							Application.Exit();
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Update failed: {ex.Message}", "Error",
							MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
			}
			else if (this._manualUpdateCheck)
			{
				MessageBox.Show($"You have the latest version ({args.InstalledVersion}).",
					"No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}

			this._manualUpdateCheck = false;
		}

		private void CheckForUpdates(Boolean silent = false)
		{
			this._manualUpdateCheck = !silent;
			AutoUpdater.ReportErrors = !silent;
			AutoUpdater.Start(Program.UpdateUrl);
		}

		private void BuildTrayIcon()
		{
			this._statusItem = new ToolStripMenuItem("Starting...")
			{
				Enabled = false
			};

			this._startStopItem = new ToolStripMenuItem("Stop", null, this.OnStartStopClick);

			this._hapticsEnabledItem = new ToolStripMenuItem("Haptics to Mouse", null, this.OnHapticsToggleClick)
			{
				Checked = true,
				CheckOnClick = true
			};

			var contextMenu = new ContextMenuStrip();
			contextMenu.Items.Add(this._statusItem);
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(this._startStopItem);
			contextMenu.Items.Add(this._hapticsEnabledItem);
			contextMenu.Items.Add(new ToolStripMenuItem("Vibration Monitor", null, this.OnShowMonitorClick));
			contextMenu.Items.Add(new ToolStripMenuItem("Test ViGEm Latency", null, this.OnTestLatencyClick));
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(new ToolStripMenuItem("Reload Config", null, this.OnReloadConfigClick));
			contextMenu.Items.Add(new ToolStripMenuItem("Open Configurator", null, this.OnOpenConfiguratorClick));
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(new ToolStripMenuItem("Check for Updates", null, this.OnCheckForUpdatesClick));
			contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, this.OnExitClick));

			this._trayIcon = new NotifyIcon
			{
				Icon = CreateIcon(),
				ContextMenuStrip = contextMenu,
				Text = "MX4 Haptic Service",
				Visible = true
			};

			this._trayIcon.DoubleClick += this.OnStartStopClick;
		}

		private static Icon CreateIcon()
		{
			// Load custom icon from app directory
			try
			{
				var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
				if (File.Exists(iconPath))
				{
					return new Icon(iconPath, 16, 16);
				}
			}
			catch
			{
				// Fall back to system icon on error
			}
			return SystemIcons.Application;
		}

		private void StartService()
		{
			if (this._service.Start())
			{
				this._startStopItem.Text = "Stop";
				this.UpdateStatus("Running", Color.Green);
			}
			else
			{
				this._startStopItem.Text = "Start";
				this.UpdateStatus("Stopped", Color.Red);
			}
		}

		private void StopService()
		{
			this._service.Stop();
			this._startStopItem.Text = "Start";
			this.UpdateStatus("Stopped", Color.Gray);
		}

		private void UpdateStatus(String status, Color color)
		{
			if (this._statusItem.GetCurrentParent()?.InvokeRequired == true)
			{
				this._statusItem.GetCurrentParent()?.Invoke(new Action(() => this.UpdateStatus(status, color)));
				return;
			}

			this._statusItem.Text = status;
			this._statusItem.ForeColor = color;
			this._trayIcon.Text = $"MX4 Haptic Service - {status}";
		}

		private void OnStatusChanged(String message)
		{
			this.UpdateStatus(message, Color.Green);
		}

		private void OnError(String message)
		{
			this.UpdateStatus($"Error: {message}", Color.Red);
			this._trayIcon.ShowBalloonTip(3000, "MX4 Haptic Service", message, ToolTipIcon.Error);
		}

		private void OnStartStopClick(Object sender, EventArgs e)
		{
			if (this._service.IsRunning)
			{
				this.StopService();
			}
			else
			{
				this.StartService();
			}
		}

		private void OnShowMonitorClick(Object sender, EventArgs e)
		{
			this._monitorForm.Show();
			this._monitorForm.BringToFront();
		}

		private void OnTestLatencyClick(Object sender, EventArgs e)
		{
			this._service.TestViGEmLatency();
		}

		private void OnHapticsToggleClick(Object sender, EventArgs e)
		{
			this._service.HapticsEnabled = this._hapticsEnabledItem.Checked;
		}

		private void OnReloadConfigClick(Object sender, EventArgs e)
		{
			this._service.ReloadConfig();
			this._trayIcon.ShowBalloonTip(1000, "MX4 Haptic Service", "Configuration reloaded", ToolTipIcon.Info);
		}

		private void OnOpenConfiguratorClick(Object sender, EventArgs e)
		{
			try
			{
				var exeDir = Path.GetDirectoryName(Application.ExecutablePath);

				// Try multiple locations for the configurator
				var possiblePaths = new[]
				{
					Path.Combine(exeDir, "HapticConfigurator.exe"),                    // Same directory
					Path.Combine(exeDir, "Configurator", "HapticConfigurator.exe"),   // Installer subdirectory
					Path.Combine(exeDir, "..", "Configurator", "HapticConfigurator.exe"), // Alternative layout
				};

				foreach (var path in possiblePaths)
				{
					if (File.Exists(path))
					{
						System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
						{
							FileName = path,
							UseShellExecute = true
						});
						return;
					}
				}

				// Development mode - use dotnet run
				var projectRoot = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", ".."));
				var configuratorProject = Path.Combine(projectRoot, "HapticConfigurator", "HapticConfigurator.csproj");

				if (File.Exists(configuratorProject))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
					{
						FileName = "dotnet",
						Arguments = $"run --project \"{configuratorProject}\"",
						UseShellExecute = true,
						CreateNoWindow = true
					});
				}
				else
				{
					MessageBox.Show("Configurator not found.\n\nRun manually:\ndotnet run --project HapticConfigurator",
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to open configurator: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void OnCheckForUpdatesClick(Object sender, EventArgs e)
		{
			this.CheckForUpdates(silent: false);
		}

		private void OnExitClick(Object sender, EventArgs e)
		{
			this._service.Dispose();
			this._trayIcon.Visible = false;
			Application.Exit();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				this._monitorForm?.Dispose();
				this._service?.Dispose();
				this._trayIcon?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
