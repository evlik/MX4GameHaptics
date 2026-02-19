using System;
using System.Drawing;
using System.Windows.Forms;

namespace MX4HapticService
{
	static class Program
	{
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

			// Auto-start service
			this.StartService();
		}

		private void BuildTrayIcon()
		{
			this._statusItem = new ToolStripMenuItem("Starting...")
			{
				Enabled = false
			};

			this._startStopItem = new ToolStripMenuItem("Stop", null, this.OnStartStopClick);

			var contextMenu = new ContextMenuStrip();
			contextMenu.Items.Add(this._statusItem);
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(this._startStopItem);
			contextMenu.Items.Add(new ToolStripMenuItem("Vibration Monitor", null, this.OnShowMonitorClick));
			contextMenu.Items.Add(new ToolStripMenuItem("Test ViGEm Latency", null, this.OnTestLatencyClick));
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(new ToolStripMenuItem("Reload Config", null, this.OnReloadConfigClick));
			contextMenu.Items.Add(new ToolStripMenuItem("Open Configurator", null, this.OnOpenConfiguratorClick));
			contextMenu.Items.Add(new ToolStripSeparator());
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
			// Use system icon to avoid GDI+ issues
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

		private void OnReloadConfigClick(Object sender, EventArgs e)
		{
			this._service.ReloadConfig();
			this._trayIcon.ShowBalloonTip(1000, "MX4 Haptic Service", "Configuration reloaded", ToolTipIcon.Info);
		}

		private void OnOpenConfiguratorClick(Object sender, EventArgs e)
		{
			try
			{
				// Get the project root directory
				var exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
				var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(exeDir, "..", "..", "..", ".."));
				var configuratorProject = System.IO.Path.Combine(projectRoot, "HapticConfigurator", "HapticConfigurator.csproj");

				if (System.IO.File.Exists(configuratorProject))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
					{
						FileName = "dotnet",
						Arguments = $"run --project \"{configuratorProject}\"",
						UseShellExecute = true
					});
				}
				else
				{
					MessageBox.Show($"Configurator not found at:\n{configuratorProject}\n\nRun manually:\ndotnet run --project HapticConfigurator",
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to open configurator: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
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
