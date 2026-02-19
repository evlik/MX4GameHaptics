using System;
using System.Drawing;
using System.Windows.Forms;

namespace MX4HapticService
{
	/// <summary>
	/// Real-time vibration monitor window.
	/// </summary>
	public class VibrationMonitorForm : Form
	{
		private ProgressBar _barLeftMotor;
		private ProgressBar _barRightMotor;
		private Label _lblLeftValue;
		private Label _lblRightValue;
		private Label _lblCombinedValue;
		private Label _lblHapticLevel;
		private Label _lblInterval;
		private Label _lblWaveform;
		private Panel _pnlVisualizer;
		private Timer _updateTimer;

		private Byte _leftMotor = 0;
		private Byte _rightMotor = 0;
		private Byte _hapticLevel = 0;
		private Int32 _interval = 0;
		private String _waveform = "wave";
		private DateTime _lastPulse = DateTime.MinValue;

		public VibrationMonitorForm()
		{
			this.InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.Text = "Vibration Monitor";
			this.Size = new Size(400, 400);
			this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
			this.StartPosition = FormStartPosition.Manual;
			this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 420, 50);
			this.TopMost = true;
			this.BackColor = Color.FromArgb(30, 30, 30);
			this.ForeColor = Color.White;

			var y = 15;

			// Title
			var lblTitle = new Label
			{
				Text = "VIBRATION MONITOR",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 12, FontStyle.Bold),
				ForeColor = Color.LimeGreen
			};
			this.Controls.Add(lblTitle);
			y += 35;

			// Left Motor
			var lblLeft = new Label
			{
				Text = "Left Motor:",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 10)
			};
			this.Controls.Add(lblLeft);

			this._lblLeftValue = new Label
			{
				Text = "0",
				Location = new Point(320, y),
				Size = new Size(50, 20),
				Font = new Font("Consolas", 10, FontStyle.Bold),
				ForeColor = Color.Cyan,
				TextAlign = ContentAlignment.MiddleRight
			};
			this.Controls.Add(this._lblLeftValue);
			y += 22;

			this._barLeftMotor = new ProgressBar
			{
				Location = new Point(15, y),
				Size = new Size(355, 25),
				Maximum = 255,
				Style = ProgressBarStyle.Continuous
			};
			this.Controls.Add(this._barLeftMotor);
			y += 35;

			// Right Motor
			var lblRight = new Label
			{
				Text = "Right Motor:",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 10)
			};
			this.Controls.Add(lblRight);

			this._lblRightValue = new Label
			{
				Text = "0",
				Location = new Point(320, y),
				Size = new Size(50, 20),
				Font = new Font("Consolas", 10, FontStyle.Bold),
				ForeColor = Color.Cyan,
				TextAlign = ContentAlignment.MiddleRight
			};
			this.Controls.Add(this._lblRightValue);
			y += 22;

			this._barRightMotor = new ProgressBar
			{
				Location = new Point(15, y),
				Size = new Size(355, 25),
				Maximum = 255,
				Style = ProgressBarStyle.Continuous
			};
			this.Controls.Add(this._barRightMotor);
			y += 40;

			// Combined / Max
			this._lblCombinedValue = new Label
			{
				Text = "Combined: 0 / 255",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 11, FontStyle.Bold),
				ForeColor = Color.Yellow
			};
			this.Controls.Add(this._lblCombinedValue);
			y += 30;

			// Haptic output
			var lblHapticHeader = new Label
			{
				Text = "─── HAPTIC OUTPUT ───",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 9),
				ForeColor = Color.Gray
			};
			this.Controls.Add(lblHapticHeader);
			y += 25;

			this._lblWaveform = new Label
			{
				Text = "Waveform: wave",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 10),
				ForeColor = Color.LimeGreen
			};
			this.Controls.Add(this._lblWaveform);
			y += 22;

			this._lblHapticLevel = new Label
			{
				Text = "Level: 0%",
				Location = new Point(15, y),
				AutoSize = true,
				Font = new Font("Consolas", 10),
				ForeColor = Color.Orange
			};
			this.Controls.Add(this._lblHapticLevel);

			this._lblInterval = new Label
			{
				Text = "Interval: 0 ms",
				Location = new Point(200, y),
				AutoSize = true,
				Font = new Font("Consolas", 10),
				ForeColor = Color.Magenta
			};
			this.Controls.Add(this._lblInterval);
			y += 35;

			// Visual pulse indicator
			this._pnlVisualizer = new Panel
			{
				Location = new Point(15, y),
				Size = new Size(355, 40),
				BackColor = Color.FromArgb(50, 50, 50)
			};
			this._pnlVisualizer.Paint += this.PnlVisualizer_Paint;
			this.Controls.Add(this._pnlVisualizer);

			// Update timer
			this._updateTimer = new Timer { Interval = 16 }; // ~60 FPS
			this._updateTimer.Tick += (s, e) => this._pnlVisualizer.Invalidate();
			this._updateTimer.Start();

			this.FormClosing += (s, e) =>
			{
				e.Cancel = true;
				this.Hide();
			};
		}

		/// <summary>
		/// Updates the display with new motor values.
		/// </summary>
		public void UpdateMotors(Byte leftMotor, Byte rightMotor)
		{
			this._leftMotor = leftMotor;
			this._rightMotor = rightMotor;

			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => this.UpdateMotorsUI()));
			}
			else
			{
				this.UpdateMotorsUI();
			}
		}

		private void UpdateMotorsUI()
		{
			this._barLeftMotor.Value = this._leftMotor;
			this._barRightMotor.Value = this._rightMotor;
			this._lblLeftValue.Text = this._leftMotor.ToString();
			this._lblRightValue.Text = this._rightMotor.ToString();

			var combined = Math.Max(this._leftMotor, this._rightMotor);
			this._lblCombinedValue.Text = $"Combined: {combined} / 255";
			this._lblCombinedValue.ForeColor = combined > 200 ? Color.Red :
				combined > 100 ? Color.Orange : Color.Yellow;
		}

		/// <summary>
		/// Updates the haptic output display.
		/// </summary>
		public void UpdateHapticOutput(String waveform, Byte hapticLevel, Int32 intervalMs)
		{
			this._waveform = waveform;
			this._hapticLevel = hapticLevel;
			this._interval = intervalMs;
			this._lastPulse = DateTime.Now;

			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => this.UpdateHapticUI()));
			}
			else
			{
				this.UpdateHapticUI();
			}
		}

		private void UpdateHapticUI()
		{
			this._lblWaveform.Text = $"Waveform: {this._waveform}";
			this._lblHapticLevel.Text = $"Level: {this._hapticLevel}%";
			this._lblInterval.Text = $"Interval: {this._interval} ms";
		}

		private void PnlVisualizer_Paint(Object sender, PaintEventArgs e)
		{
			var g = e.Graphics;
			var width = this._pnlVisualizer.Width;
			var height = this._pnlVisualizer.Height;

			// Background
			g.Clear(Color.FromArgb(50, 50, 50));

			// Draw intensity bar
			var combined = Math.Max(this._leftMotor, this._rightMotor);
			var barWidth = (Int32)(combined / 255.0 * width);

			// Gradient color based on intensity
			Color barColor;
			if (combined > 200)
				barColor = Color.Red;
			else if (combined > 150)
				barColor = Color.OrangeRed;
			else if (combined > 100)
				barColor = Color.Orange;
			else if (combined > 50)
				barColor = Color.Yellow;
			else
				barColor = Color.LimeGreen;

			if (barWidth > 0)
			{
				using (var brush = new SolidBrush(barColor))
				{
					g.FillRectangle(brush, 0, 0, barWidth, height);
				}
			}

			// Pulse flash effect
			var timeSincePulse = (DateTime.Now - this._lastPulse).TotalMilliseconds;
			if (timeSincePulse < 100 && this._hapticLevel > 0)
			{
				var alpha = (Int32)(255 * (1 - timeSincePulse / 100));
				using (var brush = new SolidBrush(Color.FromArgb(alpha, Color.White)))
				{
					g.FillRectangle(brush, 0, 0, width, height);
				}
			}

			// Border
			g.DrawRectangle(Pens.Gray, 0, 0, width - 1, height - 1);
		}

		protected override void Dispose(Boolean disposing)
		{
			if (disposing)
			{
				this._updateTimer?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
