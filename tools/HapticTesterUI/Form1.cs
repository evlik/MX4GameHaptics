using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace HapticTesterUI
{
	public partial class Form1 : Form
	{
		private HidPlusPlusDevice? _device;
		private volatile bool _sweepRunning = false;
		private Thread? _sweepThread;

		// UI Controls
		private Label _lblStatus = null!;
		private Label _lblIntensity = null!;
		private TrackBar _trkIntensity = null!;
		private Panel _pnlWaveforms = null!;
		private Button _btnConnect = null!;
		private Button _btnSweep = null!;
		private Button _btnStop = null!;

		public Form1()
		{
			InitializeComponent();
			BuildUI();
		}

		private void BuildUI()
		{
			this.Text = "MX Master 4 Haptic Tester";
			this.Size = new Size(500, 620);
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.StartPosition = FormStartPosition.CenterScreen;

			int y = 20;

			// === Connection Section ===
			var lblHeader = new Label
			{
				Text = "MX Master 4 Direct Haptic Control",
				Location = new Point(20, y),
				Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold),
				AutoSize = true
			};
			this.Controls.Add(lblHeader);
			y += 35;

			_btnConnect = new Button
			{
				Text = "Connect to Mouse",
				Location = new Point(20, y),
				Size = new Size(150, 35),
				Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold)
			};
			_btnConnect.Click += BtnConnect_Click;
			this.Controls.Add(_btnConnect);

			_lblStatus = new Label
			{
				Text = "Not connected",
				Location = new Point(180, y + 8),
				Size = new Size(280, 25),
				ForeColor = Color.Gray
			};
			this.Controls.Add(_lblStatus);
			y += 50;

			// Separator
			var sep1 = new Label { Location = new Point(20, y), Size = new Size(440, 2), BorderStyle = BorderStyle.Fixed3D };
			this.Controls.Add(sep1);
			y += 15;

			// === Intensity Section ===
			var lblIntHeader = new Label
			{
				Text = "Intensity Level (0-100%)",
				Location = new Point(20, y),
				Font = new Font(this.Font, FontStyle.Bold),
				AutoSize = true
			};
			this.Controls.Add(lblIntHeader);
			y += 25;

			_trkIntensity = new TrackBar
			{
				Location = new Point(20, y),
				Size = new Size(350, 45),
				Minimum = 0,
				Maximum = 100,
				Value = 75,
				TickFrequency = 10
			};
			_trkIntensity.ValueChanged += TrkIntensity_ValueChanged;
			this.Controls.Add(_trkIntensity);

			_lblIntensity = new Label
			{
				Text = "75%",
				Location = new Point(380, y + 10),
				Size = new Size(60, 25),
				Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleCenter
			};
			this.Controls.Add(_lblIntensity);
			y += 55;

			// Separator
			var sep2 = new Label { Location = new Point(20, y), Size = new Size(440, 2), BorderStyle = BorderStyle.Fixed3D };
			this.Controls.Add(sep2);
			y += 15;

			// === Waveforms Section ===
			var lblWaveHeader = new Label
			{
				Text = "Waveforms (click to play)",
				Location = new Point(20, y),
				Font = new Font(this.Font, FontStyle.Bold),
				AutoSize = true
			};
			this.Controls.Add(lblWaveHeader);
			y += 25;

			_pnlWaveforms = new Panel
			{
				Location = new Point(20, y),
				Size = new Size(440, 220),
				AutoScroll = true
			};
			this.Controls.Add(_pnlWaveforms);
			CreateWaveformButtons();
			y += 230;

			// Separator
			var sep3 = new Label { Location = new Point(20, y), Size = new Size(440, 2), BorderStyle = BorderStyle.Fixed3D };
			this.Controls.Add(sep3);
			y += 15;

			// === Sweep Test Section ===
			var lblSweepHeader = new Label
			{
				Text = "Intensity Sweep Test",
				Location = new Point(20, y),
				Font = new Font(this.Font, FontStyle.Bold),
				AutoSize = true
			};
			this.Controls.Add(lblSweepHeader);
			y += 25;

			_btnSweep = new Button
			{
				Text = "Start Sweep (0% -> 100% -> 0%)",
				Location = new Point(20, y),
				Size = new Size(250, 35),
				BackColor = Color.LightGreen
			};
			_btnSweep.Click += BtnSweep_Click;
			this.Controls.Add(_btnSweep);

			_btnStop = new Button
			{
				Text = "STOP",
				Location = new Point(280, y),
				Size = new Size(80, 35),
				BackColor = Color.LightCoral,
				Enabled = false
			};
			_btnStop.Click += BtnStop_Click;
			this.Controls.Add(_btnStop);
		}

		private void CreateWaveformButtons()
		{
			var waveforms = new (string name, byte id, Color color)[]
			{
				("Sharp State", 0x00, Color.LightBlue),
				("Damp State", 0x01, Color.LightBlue),
				("Sharp Collision", 0x02, Color.Orange),
				("Damp Collision", 0x03, Color.Orange),
				("Subtle Collision", 0x04, Color.LightYellow),
				("Happy Alert", 0x05, Color.LightGreen),
				("Angry Alert", 0x06, Color.LightCoral),
				("Completed", 0x07, Color.LightGreen),
				("Square", 0x08, Color.LightGray),
				("Wave", 0x09, Color.LightCyan),
				("Firework", 0x0A, Color.Pink),
				("Mad", 0x0B, Color.LightCoral),
				("Knock", 0x0C, Color.Wheat),
				("Jingle", 0x0D, Color.Lavender),
				("Ringing", 0x0E, Color.Lavender),
				("Whisper", 0x1B, Color.WhiteSmoke)
			};

			int x = 5, y = 5;
			int btnWidth = 105, btnHeight = 35;
			int cols = 4;

			for (int i = 0; i < waveforms.Length; i++)
			{
				var wf = waveforms[i];
				var btn = new Button
				{
					Text = wf.name,
					Tag = wf.id,
					Location = new Point(x + (i % cols) * (btnWidth + 5), y + (i / cols) * (btnHeight + 5)),
					Size = new Size(btnWidth, btnHeight),
					BackColor = wf.color,
					FlatStyle = FlatStyle.Flat,
					Font = new Font(this.Font.FontFamily, 8)
				};
				btn.Click += WaveformBtn_Click;
				_pnlWaveforms.Controls.Add(btn);
			}
		}

		private void BtnConnect_Click(object? sender, EventArgs e)
		{
			_device?.Dispose();
			_device = new HidPlusPlusDevice();

			_lblStatus.Text = "Connecting...";
			_lblStatus.ForeColor = Color.Orange;
			this.Refresh();

			if (_device.Connect())
			{
				_lblStatus.Text = $"Connected! (idx={_device.DeviceIndex}, feat={_device.HapticFeatureIndex}, long={_device.UseLongReports})";
				_lblStatus.ForeColor = Color.Green;
				_btnConnect.Text = "Reconnect";
			}
			else
			{
				_lblStatus.Text = "Connection failed! Use Bluetooth.";
				_lblStatus.ForeColor = Color.Red;
			}
		}

		private void TrkIntensity_ValueChanged(object? sender, EventArgs e)
		{
			_lblIntensity.Text = $"{_trkIntensity.Value}%";

			// Apply intensity immediately when connected
			if (_device?.IsConnected == true)
			{
				_device.SetHapticLevel((byte)_trkIntensity.Value);
			}
		}

		private void WaveformBtn_Click(object? sender, EventArgs e)
		{
			if (_device?.IsConnected != true)
			{
				MessageBox.Show("Please connect to the mouse first!", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			if (sender is Button btn && btn.Tag is byte waveformId)
			{
				_device.SetHapticLevel((byte)_trkIntensity.Value);
				_device.PlayWaveform(waveformId);
			}
		}

		private void BtnSweep_Click(object? sender, EventArgs e)
		{
			if (_device?.IsConnected != true)
			{
				MessageBox.Show("Please connect to the mouse first!", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_sweepRunning = true;
			_btnSweep.Enabled = false;
			_btnStop.Enabled = true;

			_sweepThread = new Thread(() =>
			{
				int direction = 1;
				int level = 0;
				int step = 2;
				byte waveformId = 0x09; // Wave

				while (_sweepRunning)
				{
					_device.SetHapticLevel((byte)level);
					_device.PlayWaveform(waveformId);

					// Update UI
					this.Invoke(() =>
					{
						_trkIntensity.Value = level;
					});

					level += direction * step;
					if (level >= 100) { level = 100; direction = -1; }
					if (level <= 0) { level = 0; direction = 1; }

					Thread.Sleep(30);
				}

				_device.SetHapticLevel(0);

				this.Invoke(() =>
				{
					_btnSweep.Enabled = true;
					_btnStop.Enabled = false;
				});
			})
			{
				IsBackground = true
			};
			_sweepThread.Start();
		}

		private void BtnStop_Click(object? sender, EventArgs e)
		{
			_sweepRunning = false;
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			_sweepRunning = false;
			_sweepThread?.Join(500);
			_device?.Dispose();
			base.OnFormClosing(e);
		}
	}
}
