using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HapticConfigurator
{
	public partial class MainForm : Form
	{
		// Dark theme colors
		private static readonly Color BgDark = Color.FromArgb(32, 32, 32);
		private static readonly Color BgPanel = Color.FromArgb(45, 45, 45);
		private static readonly Color BgControl = Color.FromArgb(60, 60, 60);
		private static readonly Color FgText = Color.FromArgb(240, 240, 240);
		private static readonly Color Accent = Color.FromArgb(0, 212, 170);
		private static readonly Color AccentDark = Color.FromArgb(0, 160, 130);

		private HapticConfig _config;

		// Simple Mode Controls
		private RadioButton _rbSimpleMode;
		private RadioButton _rbAdvancedMode;
		private Panel _pnlSimpleMode;
		private Panel _pnlAdvancedMode;
		private ComboBox _cboSimpleWaveform;
		private TrackBar _trkIntensityScale;
		private Label _lblIntensityScaleValue;
		private TrackBar _trkMinHaptic;
		private Label _lblMinHapticValue;
		private TrackBar _trkMaxHaptic;
		private Label _lblMaxHapticValue;
		private TrackBar _trkSimpleIntervalMin;
		private Label _lblSimpleIntervalMinValue;
		private TrackBar _trkSimpleIntervalMax;
		private Label _lblSimpleIntervalMaxValue;
		private CheckBox _chkFixedInterval;

		// Waveform Zones DataGridView (Advanced)
		private DataGridView _gridZones;
		private Button _btnAddZone;
		private Label _lblStatus;

		// Store curve data per zone (index matches grid row)
		private List<List<CurvePoint>> _zoneCurves = new List<List<CurvePoint>>();

		// PDM Controls (Advanced)
		private CheckBox _chkDynamicIntensity;
		private TrackBar _trkInterval;
		private Label _lblIntervalValue;

		// Continuous test
		private volatile Boolean _testRunning = false;
		private volatile Boolean _testAllRunning = false;
		private Thread _testThread;

		// Direct HID++ device
		private HidPlusPlusDevice? _device;
		private Button _btnConnect = null!;
		private Byte _lastHapticLevel = 0;

		public MainForm()
		{
			this.InitializeComponent();
			this.ApplyDarkTheme();
			this._config = HapticConfig.Load();
			this.LoadConfigToUI();
			this.FormClosing += (s, e) =>
			{
				this._testAllRunning = false;
				this._testRunning = false;
				this._device?.Dispose();
			};
		}

		/// <summary>
		/// Applies dark theme styling to all controls.
		/// </summary>
		private void ApplyDarkTheme()
		{
			this.BackColor = BgDark;
			this.ForeColor = FgText;

			foreach (Control ctrl in this.Controls)
			{
				this.ApplyThemeToControl(ctrl);
			}
		}

		/// <summary>
		/// Recursively applies theme to a control and its children.
		/// </summary>
		private void ApplyThemeToControl(Control ctrl)
		{
			ctrl.ForeColor = FgText;

			if (ctrl is Panel panel)
			{
				panel.BackColor = BgPanel;
			}
			else if (ctrl is Button btn)
			{
				btn.FlatStyle = FlatStyle.Flat;
				btn.FlatAppearance.BorderColor = Accent;
				btn.FlatAppearance.BorderSize = 1;
				btn.BackColor = BgControl;
				btn.ForeColor = FgText;
			}
			else if (ctrl is TextBox txt)
			{
				txt.BackColor = BgControl;
				txt.ForeColor = FgText;
				txt.BorderStyle = BorderStyle.FixedSingle;
			}
			else if (ctrl is ComboBox cbo)
			{
				cbo.BackColor = BgControl;
				cbo.ForeColor = FgText;
				cbo.FlatStyle = FlatStyle.Flat;
			}
			else if (ctrl is CheckBox chk)
			{
				chk.BackColor = Color.Transparent;
				chk.ForeColor = FgText;
			}
			else if (ctrl is RadioButton rb)
			{
				rb.BackColor = Color.Transparent;
				rb.ForeColor = FgText;
			}
			else if (ctrl is Label lbl)
			{
				lbl.BackColor = Color.Transparent;
			}
			else if (ctrl is DataGridView dgv)
			{
				dgv.BackgroundColor = BgPanel;
				dgv.GridColor = BgControl;
				dgv.DefaultCellStyle.BackColor = BgControl;
				dgv.DefaultCellStyle.ForeColor = FgText;
				dgv.DefaultCellStyle.SelectionBackColor = Accent;
				dgv.DefaultCellStyle.SelectionForeColor = BgDark;
				dgv.ColumnHeadersDefaultCellStyle.BackColor = BgPanel;
				dgv.ColumnHeadersDefaultCellStyle.ForeColor = FgText;
				dgv.EnableHeadersVisualStyles = false;
			}
			else if (ctrl is TrackBar)
			{
				// TrackBar doesn't support custom colors in WinForms
				ctrl.BackColor = BgPanel;
			}
			else
			{
				ctrl.BackColor = BgDark;
			}

			// Recursively apply to children
			foreach (Control child in ctrl.Controls)
			{
				this.ApplyThemeToControl(child);
			}
		}

		private void InitializeComponent()
		{
			this.Text = "MX4 Haptic Configurator";
			this.Size = new Size(600, 610);
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.StartPosition = FormStartPosition.CenterScreen;

			var y = 15;
			const Int32 labelX = 20;

			// ===== Mode Selection =====
			this._rbSimpleMode = new RadioButton
			{
				Text = "Simple Mode (Recommended)",
				Location = new Point(20, y),
				AutoSize = true,
				Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold),
				Checked = true
			};
			this._rbSimpleMode.CheckedChanged += (s, e) => this.UpdateModeVisibility();
			this.Controls.Add(this._rbSimpleMode);

			this._rbAdvancedMode = new RadioButton
			{
				Text = "Advanced (PDM Zones)",
				Location = new Point(250, y),
				AutoSize = true,
				Font = new Font(this.Font.FontFamily, 10)
			};
			this._rbAdvancedMode.CheckedChanged += (s, e) => this.UpdateModeVisibility();
			this.Controls.Add(this._rbAdvancedMode);
			y += 30;

			// ===== Simple Mode Panel =====
			this._pnlSimpleMode = new Panel
			{
				Location = new Point(0, y),
				Size = new Size(600, 310),
				Visible = true
			};
			this.Controls.Add(this._pnlSimpleMode);

			this.BuildSimpleModeUI();

			// ===== Advanced Mode Panel =====
			this._pnlAdvancedMode = new Panel
			{
				Location = new Point(0, y),
				Size = new Size(600, 310),
				Visible = false
			};
			this.Controls.Add(this._pnlAdvancedMode);

			this.BuildAdvancedModeUI();

			y += 315;

			// ===== Connection =====
			this._btnConnect = new Button
			{
				Text = "Connect Mouse",
				Location = new Point(20, y),
				Size = new Size(120, 30)
			};
			this._btnConnect.Click += this.BtnConnect_Click;
			this.Controls.Add(this._btnConnect);

			var btnTestSweep = new Button
			{
				Text = "Test Sweep",
				Location = new Point(150, y),
				Size = new Size(100, 30)
			};
			btnTestSweep.Click += this.BtnTestSweep_Click;
			this.Controls.Add(btnTestSweep);

			var btnTestStop = new Button
			{
				Text = "STOP",
				Location = new Point(260, y),
				Size = new Size(60, 30)
			};
			btnTestStop.Click += (s, e) => { this._testAllRunning = false; this._testRunning = false; };
			this.Controls.Add(btnTestStop);

			var btnSave = new Button
			{
				Text = "Save",
				Location = new Point(340, y),
				Size = new Size(70, 30)
			};
			btnSave.Click += this.BtnSave_Click;
			this.Controls.Add(btnSave);

			var btnApply = new Button
			{
				Text = "Apply",
				Location = new Point(420, y),
				Size = new Size(70, 30)
			};
			btnApply.Click += this.BtnApply_Click;
			this.Controls.Add(btnApply);

			var btnReset = new Button
			{
				Text = "Reset",
				Location = new Point(500, y),
				Size = new Size(60, 30)
			};
			btnReset.Click += this.BtnReset_Click;
			this.Controls.Add(btnReset);

			y += 45;

			// Status label
			this._lblStatus = new Label
			{
				Location = new Point(20, y),
				Size = new Size(540, 40),
				Text = "Click 'Connect Mouse' to start",
				ForeColor = Color.Gray
			};
			this.Controls.Add(this._lblStatus);
		}

		private void BtnConnect_Click(Object? sender, EventArgs e)
		{
			this._device?.Dispose();
			this._device = new HidPlusPlusDevice();

			this.SetStatus("Connecting...", Color.Orange);
			this.Refresh();

			if (this._device.Connect())
			{
				this._btnConnect.Text = "Reconnect";
				this._btnConnect.FlatAppearance.BorderColor = Accent;
				this.SetStatus($"Connected! (idx={this._device.DeviceIndex:X2}, feat={this._device.HapticFeatureIndex}, long={this._device.UseLongReports})", Accent);
			}
			else
			{
				this._btnConnect.FlatAppearance.BorderColor = Color.Salmon;
				this.SetStatus("Connection failed! Make sure mouse is connected via Bluetooth.", Color.Salmon);
			}
		}

		private void BuildSimpleModeUI()
		{
			var y = 10;
			const Int32 labelX = 20;
			const Int32 trackX = 200;

			// Waveform Selection
			var lblWaveform = new Label
			{
				Text = "Waveform:",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblWaveform);

			this._cboSimpleWaveform = new ComboBox
			{
				Location = new Point(trackX, y),
				Size = new Size(150, 25),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			this._cboSimpleWaveform.Items.AddRange(HapticConfig.AvailableWaveforms);
			this._cboSimpleWaveform.SelectedItem = "wave";
			this._pnlSimpleMode.Controls.Add(this._cboSimpleWaveform);

			var btnTestWave = new Button
			{
				Text = "Test",
				Location = new Point(360, y),
				Size = new Size(50, 25)
			};
			btnTestWave.Click += (s, e) =>
			{
				if (this._device?.IsConnected != true)
				{
					this.SetStatus("Connect to mouse first!", Color.Red);
					return;
				}
				var waveform = this._cboSimpleWaveform.SelectedItem?.ToString() ?? "wave";
				this._device.SetHapticLevel(75);
				this._device.PlayWaveform(this.MapWaveformNameToId(waveform));
				this.SetStatus($"Played: {waveform} at 75%", Color.Green);
			};
			this._pnlSimpleMode.Controls.Add(btnTestWave);
			y += 35;

			// Intensity Scale
			var lblIntScale = new Label
			{
				Text = "Intensity Scale:",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblIntScale);

			this._trkIntensityScale = new TrackBar
			{
				Location = new Point(trackX, y),
				Size = new Size(200, 45),
				Minimum = 50,
				Maximum = 200,
				Value = 100,
				TickFrequency = 25
			};
			this._trkIntensityScale.ValueChanged += (s, e) =>
				this._lblIntensityScaleValue.Text = $"{this._trkIntensityScale.Value / 100.0:F2}x";
			this._pnlSimpleMode.Controls.Add(this._trkIntensityScale);

			this._lblIntensityScaleValue = new Label
			{
				Text = "1.00x",
				Location = new Point(410, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlSimpleMode.Controls.Add(this._lblIntensityScaleValue);
			y += 45;

			// Min Haptic Level
			var lblMinHaptic = new Label
			{
				Text = "Min Level (at low intensity):",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblMinHaptic);

			this._trkMinHaptic = new TrackBar
			{
				Location = new Point(trackX, y),
				Size = new Size(200, 45),
				Minimum = 0,
				Maximum = 100,
				Value = 10,
				TickFrequency = 10
			};
			this._trkMinHaptic.ValueChanged += (s, e) =>
				this._lblMinHapticValue.Text = $"{this._trkMinHaptic.Value}%";
			this._pnlSimpleMode.Controls.Add(this._trkMinHaptic);

			this._lblMinHapticValue = new Label
			{
				Text = "10%",
				Location = new Point(410, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlSimpleMode.Controls.Add(this._lblMinHapticValue);
			y += 45;

			// Max Haptic Level
			var lblMaxHaptic = new Label
			{
				Text = "Max Level (at high intensity):",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblMaxHaptic);

			this._trkMaxHaptic = new TrackBar
			{
				Location = new Point(trackX, y),
				Size = new Size(200, 45),
				Minimum = 0,
				Maximum = 100,
				Value = 100,
				TickFrequency = 10
			};
			this._trkMaxHaptic.ValueChanged += (s, e) =>
				this._lblMaxHapticValue.Text = $"{this._trkMaxHaptic.Value}%";
			this._pnlSimpleMode.Controls.Add(this._trkMaxHaptic);

			this._lblMaxHapticValue = new Label
			{
				Text = "100%",
				Location = new Point(410, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlSimpleMode.Controls.Add(this._lblMaxHapticValue);
			y += 45;

			// Fixed Interval checkbox
			this._chkFixedInterval = new CheckBox
			{
				Text = "Fixed Interval",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._chkFixedInterval.CheckedChanged += (s, e) => this.UpdateIntervalControlsState();
			this._pnlSimpleMode.Controls.Add(this._chkFixedInterval);
			y += 30;

			// Pulse Frequency Min (fast pulses at high intensity / or fixed interval)
			var lblIntervalMin = new Label
			{
				Text = "Min Interval (fast, at high):",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblIntervalMin);

			this._trkSimpleIntervalMin = new TrackBar
			{
				Location = new Point(trackX, y),
				Size = new Size(200, 45),
				Minimum = 1,
				Maximum = 500,
				Value = 15,
				TickFrequency = 50
			};
			this._trkSimpleIntervalMin.ValueChanged += (s, e) =>
				this._lblSimpleIntervalMinValue.Text = $"{this._trkSimpleIntervalMin.Value} ms";
			this._pnlSimpleMode.Controls.Add(this._trkSimpleIntervalMin);

			this._lblSimpleIntervalMinValue = new Label
			{
				Text = "15 ms",
				Location = new Point(410, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlSimpleMode.Controls.Add(this._lblSimpleIntervalMinValue);
			y += 45;

			// Pulse Frequency Max (slow pulses at low intensity)
			var lblIntervalMax = new Label
			{
				Text = "Max Interval (slow, at low):",
				Location = new Point(labelX, y + 3),
				AutoSize = true
			};
			this._pnlSimpleMode.Controls.Add(lblIntervalMax);

			this._trkSimpleIntervalMax = new TrackBar
			{
				Location = new Point(trackX, y),
				Size = new Size(200, 45),
				Minimum = 1,
				Maximum = 500,
				Value = 150,
				TickFrequency = 50
			};
			this._trkSimpleIntervalMax.ValueChanged += (s, e) =>
				this._lblSimpleIntervalMaxValue.Text = $"{this._trkSimpleIntervalMax.Value} ms";
			this._pnlSimpleMode.Controls.Add(this._trkSimpleIntervalMax);

			this._lblSimpleIntervalMaxValue = new Label
			{
				Text = "150 ms",
				Location = new Point(410, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlSimpleMode.Controls.Add(this._lblSimpleIntervalMaxValue);
		}

		private void UpdateIntervalControlsState()
		{
			var isFixed = this._chkFixedInterval.Checked;
			this._trkSimpleIntervalMax.Enabled = !isFixed;
		}

		private void BuildAdvancedModeUI()
		{
			var y = 10;
			const Int32 labelX = 20;

			// Zones header
			var lblZonesHeader = new Label
			{
				Text = "Waveform Zones (1-8 zones)",
				Location = new Point(labelX, y),
				Font = new Font(this.Font, FontStyle.Bold),
				AutoSize = true
			};
			this._pnlAdvancedMode.Controls.Add(lblZonesHeader);
			y += 22;

			// DataGridView for zones
			this._gridZones = new DataGridView
			{
				Location = new Point(20, y),
				Size = new Size(540, 150),
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				MultiSelect = false,
				RowHeadersVisible = false,
				AllowUserToResizeRows = false
			};

			var colWaveform = new DataGridViewComboBoxColumn
			{
				Name = "Waveform",
				HeaderText = "Waveform",
				DataSource = HapticConfig.AvailableWaveforms.ToList(),
				FillWeight = 30
			};
			this._gridZones.Columns.Add(colWaveform);

			this._gridZones.Columns.Add(new DataGridViewTextBoxColumn { Name = "IntMin", HeaderText = "Int Min", FillWeight = 12 });
			this._gridZones.Columns.Add(new DataGridViewTextBoxColumn { Name = "IntMax", HeaderText = "Int Max", FillWeight = 12 });
			this._gridZones.Columns.Add(new DataGridViewTextBoxColumn { Name = "IntervalMin", HeaderText = "Int↓ (ms)", FillWeight = 14 });
			this._gridZones.Columns.Add(new DataGridViewTextBoxColumn { Name = "IntervalMax", HeaderText = "Int↑ (ms)", FillWeight = 14 });
			this._gridZones.Columns.Add(new DataGridViewButtonColumn { Name = "Curve", HeaderText = "", Text = "📈", UseColumnTextForButtonValue = true, FillWeight = 8 });
			this._gridZones.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "X", UseColumnTextForButtonValue = true, FillWeight = 8 });
			this._gridZones.Columns.Add(new DataGridViewButtonColumn { Name = "Test", HeaderText = "", Text = "Test", UseColumnTextForButtonValue = true, FillWeight = 10 });

			this._gridZones.CellClick += this.GridZones_CellClick;
			this._gridZones.CellValidating += this.GridZones_CellValidating;
			this._pnlAdvancedMode.Controls.Add(this._gridZones);
			y += 155;

			// Add Zone button
			this._btnAddZone = new Button
			{
				Text = "+ Add Zone",
				Location = new Point(20, y),
				Size = new Size(100, 25)
			};
			this._btnAddZone.Click += this.BtnAddZone_Click;
			this._pnlAdvancedMode.Controls.Add(this._btnAddZone);
			y += 35;

			// PDM checkbox
			this._chkDynamicIntensity = new CheckBox
			{
				Text = "Enable PDM (pulse rate varies with intensity)",
				Location = new Point(labelX, y),
				Size = new Size(350, 20),
				Checked = true
			};
			this._chkDynamicIntensity.CheckedChanged += (s, e) => this.UpdatePdmControlsState();
			this._pnlAdvancedMode.Controls.Add(this._chkDynamicIntensity);
			y += 25;

			// Fixed interval
			var lblFixed = new Label { Text = "Fixed Interval:", Location = new Point(labelX, y + 3), AutoSize = true };
			this._pnlAdvancedMode.Controls.Add(lblFixed);

			this._trkInterval = new TrackBar
			{
				Location = new Point(150, y),
				Size = new Size(150, 40),
				Minimum = 1,
				Maximum = 500,
				Value = 30,
				TickFrequency = 50
			};
			this._trkInterval.ValueChanged += (s, e) => this._lblIntervalValue.Text = $"{this._trkInterval.Value / 10.0:F1} ms";
			this._pnlAdvancedMode.Controls.Add(this._trkInterval);

			this._lblIntervalValue = new Label
			{
				Text = "3.0 ms",
				Location = new Point(310, y + 10),
				Size = new Size(60, 20)
			};
			this._pnlAdvancedMode.Controls.Add(this._lblIntervalValue);
		}

		private void UpdateModeVisibility()
		{
			var isSimple = this._rbSimpleMode.Checked;
			this._pnlSimpleMode.Visible = isSimple;
			this._pnlAdvancedMode.Visible = !isSimple;
		}

		private Label AddLabel(String text, Int32 x, Int32 y)
		{
			var label = new Label
			{
				Text = text,
				Location = new Point(x, y + 3),
				AutoSize = true
			};
			this.Controls.Add(label);
			return label;
		}

		private TrackBar AddTrackBar(Int32 x, Int32 y, Int32 min, Int32 max, Int32 value)
		{
			var track = new TrackBar
			{
				Location = new Point(x, y),
				Size = new Size(180, 45),
				Minimum = min,
				Maximum = max,
				Value = value,
				TickFrequency = (max - min) / 10
			};
			this.Controls.Add(track);
			return track;
		}

		private Label AddValueLabel(Int32 x, Int32 y)
		{
			var label = new Label
			{
				Location = new Point(x, y + 10),
				Size = new Size(60, 20),
				TextAlign = ContentAlignment.MiddleCenter
			};
			this.Controls.Add(label);
			return label;
		}

		private void UpdatePdmControlsState()
		{
			var enabled = this._chkDynamicIntensity.Checked;
			this._trkInterval.Enabled = !enabled;
		}

		#region Waveform Zones Grid

		private void PopulateZonesGrid()
		{
			this._gridZones.Rows.Clear();
			this._zoneCurves.Clear();

			foreach (var zone in this._config.WaveformZones)
			{
				var rowIndex = this._gridZones.Rows.Add();
				var row = this._gridZones.Rows[rowIndex];
				row.Cells["Waveform"].Value = zone.Waveform;
				row.Cells["IntMin"].Value = zone.IntensityMin;
				row.Cells["IntMax"].Value = zone.IntensityMax;
				row.Cells["IntervalMin"].Value = (zone.IntervalMinTenths / 10.0).ToString("F0");
				row.Cells["IntervalMax"].Value = (zone.IntervalMaxTenths / 10.0).ToString("F0");

				// Store a copy of the curve
				var curveCopy = zone.IntervalCurve?.Select(p =>
					new CurvePoint { Position = p.Position, Value = p.Value }).ToList()
					?? GetDefaultCurve();
				this._zoneCurves.Add(curveCopy);
			}
		}

		private static List<CurvePoint> GetDefaultCurve()
		{
			return new List<CurvePoint>
			{
				new CurvePoint { Position = 0.0, Value = 1.0 },
				new CurvePoint { Position = 1.0, Value = 0.0 }
			};
		}

		private void GridZones_CellClick(Object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0) return;

			var columnName = this._gridZones.Columns[e.ColumnIndex].Name;

			if (columnName == "Delete")
			{
				if (this._gridZones.Rows.Count > 1)
				{
					this._gridZones.Rows.RemoveAt(e.RowIndex);
					this._zoneCurves.RemoveAt(e.RowIndex);
					this.RecalculateZoneBoundaries();
				}
				else
				{
					this.SetStatus("Minimum 1 zone required", Color.Orange);
				}
			}
			else if (columnName == "Curve")
			{
				this.OpenCurveEditor(e.RowIndex);
			}
			else if (columnName == "Test")
			{
				var waveform = this._gridZones.Rows[e.RowIndex].Cells["Waveform"].Value?.ToString();
				if (!String.IsNullOrEmpty(waveform))
				{
					this.StartZoneTest(waveform, e.RowIndex);
				}
			}
		}

		private void GridZones_CellValidating(Object sender, DataGridViewCellValidatingEventArgs e)
		{
			var columnName = this._gridZones.Columns[e.ColumnIndex].Name;

			if (columnName == "IntMin" || columnName == "IntMax")
			{
				if (!Int32.TryParse(e.FormattedValue?.ToString(), out var value) || value < 0 || value > 256)
				{
					e.Cancel = true;
					this.SetStatus("Value must be 0-256", Color.Red);
				}
			}
			else if (columnName == "IntervalMin" || columnName == "IntervalMax")
			{
				if (!Double.TryParse(e.FormattedValue?.ToString(), out var value) || value < 1 || value > 1000)
				{
					e.Cancel = true;
					this.SetStatus("Interval must be 1-1000 ms", Color.Red);
				}
			}
		}

		private void BtnAddZone_Click(Object sender, EventArgs e)
		{
			if (this._gridZones.Rows.Count >= 8)
			{
				this.SetStatus("Maximum 8 zones allowed", Color.Orange);
				return;
			}

			var lastMax = 0;
			if (this._gridZones.Rows.Count > 0)
			{
				var lastRow = this._gridZones.Rows[this._gridZones.Rows.Count - 1];
				Int32.TryParse(lastRow.Cells["IntMax"].Value?.ToString(), out lastMax);
			}

			var rowIndex = this._gridZones.Rows.Add();
			var row = this._gridZones.Rows[rowIndex];
			row.Cells["Waveform"].Value = "wave";
			row.Cells["IntMin"].Value = lastMax;
			row.Cells["IntMax"].Value = 256;
			row.Cells["IntervalMin"].Value = "15";
			row.Cells["IntervalMax"].Value = "200";

			// Add default curve for new zone
			this._zoneCurves.Add(GetDefaultCurve());

			this.RecalculateZoneBoundaries();
		}

		private void RecalculateZoneBoundaries()
		{
			var rowCount = this._gridZones.Rows.Count;
			if (rowCount == 0) return;

			var rangePerZone = 256 / rowCount;

			for (var i = 0; i < rowCount; i++)
			{
				var row = this._gridZones.Rows[i];
				row.Cells["IntMin"].Value = i * rangePerZone;
				row.Cells["IntMax"].Value = (i == rowCount - 1) ? 256 : (i + 1) * rangePerZone;
			}
		}

		private void StartZoneTest(String waveform, Int32 rowIndex)
		{
			if (this._testRunning)
			{
				this._testRunning = false;
				return;
			}

			// Get zone settings
			var row = this._gridZones.Rows[rowIndex];
			Int32.TryParse(row.Cells["IntMin"].Value?.ToString(), out var minIntensity);
			Int32.TryParse(row.Cells["IntMax"].Value?.ToString(), out var maxIntensity);
			Double.TryParse(row.Cells["IntervalMin"].Value?.ToString(), out var intervalMinMs);
			Double.TryParse(row.Cells["IntervalMax"].Value?.ToString(), out var intervalMaxMs);

			// Use middle of the zone
			var midIntensity = (Byte)((minIntensity + maxIntensity) / 2);

			// Calculate interval at mid intensity
			var range = maxIntensity - minIntensity;
			var normalized = (midIntensity - minIntensity) / (Double)range;
			var intervalMs = intervalMaxMs - (intervalMaxMs - intervalMinMs) * normalized;
			var intervalTenths = (Int32)(intervalMs * 10);

			this._testRunning = true;
			this._testThread = new Thread(() =>
			{
				var sw = Stopwatch.StartNew();
				while (this._testRunning)
				{
					PluginClient.TestWaveform(waveform);

					var targetTicks = intervalTenths * Stopwatch.Frequency / 10000;
					sw.Restart();
					while (sw.ElapsedTicks < targetTicks && this._testRunning)
					{
						Thread.SpinWait(10);
					}
				}
			})
			{
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal
			};
			this._testThread.Start();

			this.SetStatus($"Testing: {waveform} @ {intervalMs:F0}ms (click again to stop)", Color.Blue);
		}

		private void OpenCurveEditor(Int32 rowIndex)
		{
			if (rowIndex < 0 || rowIndex >= this._zoneCurves.Count)
				return;

			var row = this._gridZones.Rows[rowIndex];
			var waveform = row.Cells["Waveform"].Value?.ToString() ?? "wave";
			Double.TryParse(row.Cells["IntervalMin"].Value?.ToString(), out var intervalMinMs);
			Double.TryParse(row.Cells["IntervalMax"].Value?.ToString(), out var intervalMaxMs);

			using (var editor = new CurveEditorForm(
				this._zoneCurves[rowIndex],
				waveform,
				intervalMinMs,
				intervalMaxMs))
			{
				if (editor.ShowDialog(this) == DialogResult.OK)
				{
					this._zoneCurves[rowIndex] = editor.GetCurvePoints();
					this.SetStatus($"Curve updated for zone {rowIndex + 1} ({waveform})", Color.Green);
				}
			}
		}

		#endregion

		#region Load/Save Config

		private void LoadConfigToUI()
		{
			// Simple Mode
			this._rbSimpleMode.Checked = this._config.EnableSimpleMode;
			this._rbAdvancedMode.Checked = !this._config.EnableSimpleMode;

			// Simple mode controls
			var waveformIndex = Array.IndexOf(HapticConfig.AvailableWaveforms, this._config.SimpleWaveform);
			if (waveformIndex >= 0) this._cboSimpleWaveform.SelectedIndex = waveformIndex;
			else this._cboSimpleWaveform.SelectedIndex = 0;

			this._trkIntensityScale.Value = Math.Clamp((Int32)(this._config.IntensityScale * 100), 50, 200);
			this._trkMinHaptic.Value = Math.Clamp(this._config.MinHapticLevel, 0, 100);
			this._trkMaxHaptic.Value = Math.Clamp(this._config.MaxHapticLevel, 0, 100);
			this._chkFixedInterval.Checked = this._config.UseFixedInterval;
			this._trkSimpleIntervalMin.Value = Math.Clamp(this._config.SimpleIntervalMinMs, 1, 500);
			this._trkSimpleIntervalMax.Value = Math.Clamp(this._config.SimpleIntervalMaxMs, 1, 500);

			// Update labels
			this._lblIntensityScaleValue.Text = $"{this._trkIntensityScale.Value / 100.0:F2}x";
			this._lblMinHapticValue.Text = $"{this._trkMinHaptic.Value}%";
			this._lblMaxHapticValue.Text = $"{this._trkMaxHaptic.Value}%";
			this._lblSimpleIntervalMinValue.Text = $"{this._trkSimpleIntervalMin.Value} ms";
			this._lblSimpleIntervalMaxValue.Text = $"{this._trkSimpleIntervalMax.Value} ms";

			// Advanced mode controls
			this.PopulateZonesGrid();
			this._chkDynamicIntensity.Checked = this._config.EnableDynamicIntensity;
			this._trkInterval.Value = Math.Clamp(this._config.PulseIntervalMs, 1, 500);
			this._lblIntervalValue.Text = $"{this._trkInterval.Value / 10.0:F1} ms";

			this.UpdateModeVisibility();
			this.UpdatePdmControlsState();
			this.UpdateIntervalControlsState();
		}

		private void SaveUIToConfig()
		{
			// Simple Mode settings
			this._config.EnableSimpleMode = this._rbSimpleMode.Checked;
			this._config.SimpleWaveform = this._cboSimpleWaveform.SelectedItem?.ToString() ?? "wave";
			this._config.IntensityScale = this._trkIntensityScale.Value / 100.0;
			this._config.MinHapticLevel = this._trkMinHaptic.Value;
			this._config.MaxHapticLevel = this._trkMaxHaptic.Value;
			this._config.UseFixedInterval = this._chkFixedInterval.Checked;
			this._config.SimpleIntervalMinMs = this._trkSimpleIntervalMin.Value;
			this._config.SimpleIntervalMaxMs = this._trkSimpleIntervalMax.Value;

			// Advanced Mode settings
			this._config.WaveformZones.Clear();
			for (var i = 0; i < this._gridZones.Rows.Count; i++)
			{
				var row = this._gridZones.Rows[i];
				var waveform = row.Cells["Waveform"].Value?.ToString() ?? "wave";
				Int32.TryParse(row.Cells["IntMin"].Value?.ToString(), out var intMin);
				Int32.TryParse(row.Cells["IntMax"].Value?.ToString(), out var intMax);
				Double.TryParse(row.Cells["IntervalMin"].Value?.ToString(), out var intervalMinMs);
				Double.TryParse(row.Cells["IntervalMax"].Value?.ToString(), out var intervalMaxMs);

				var zone = new WaveformZone
				{
					Waveform = waveform,
					IntensityMin = intMin,
					IntensityMax = intMax,
					IntervalMinTenths = (Int32)(intervalMinMs * 10),
					IntervalMaxTenths = (Int32)(intervalMaxMs * 10)
				};

				// Copy curve if available
				if (i < this._zoneCurves.Count && this._zoneCurves[i] != null)
				{
					zone.IntervalCurve = this._zoneCurves[i]
						.Select(p => new CurvePoint { Position = p.Position, Value = p.Value })
						.ToList();
				}

				this._config.WaveformZones.Add(zone);
			}

			this._config.EnableDynamicIntensity = this._chkDynamicIntensity.Checked;
			this._config.PulseIntervalMs = this._trkInterval.Value;
		}

		#endregion

		#region Button Handlers

		private void BtnTestSweep_Click(Object sender, EventArgs e)
		{
			if (this._device?.IsConnected != true)
			{
				this.SetStatus("Connect to mouse first!", Color.Red);
				return;
			}

			if (this._testAllRunning)
			{
				this._testAllRunning = false;
				return;
			}

			this.SaveUIToConfig();
			this._testAllRunning = true;

			new Thread(() =>
			{
				var sw = Stopwatch.StartNew();
				const Int32 sweepDurationMs = 5000;
				var startTime = sw.ElapsedMilliseconds;

				while (this._testAllRunning && this._device?.IsConnected == true)
				{
					var elapsed = sw.ElapsedMilliseconds - startTime;
					if (elapsed >= sweepDurationMs) break;

					var progress = (Double)elapsed / sweepDurationMs;
					var intensity = (Byte)Math.Clamp((Int32)(1 + progress * 254), 1, 255);

					String waveform;
					Int32 intervalMs;
					Byte hapticLevel;

					if (this._config.EnableSimpleMode)
					{
						waveform = this._config.SimpleWaveform;
						hapticLevel = this._config.CalculateSimpleHapticLevel(intensity);
						intervalMs = this._config.CalculateSimpleInterval(intensity);
					}
					else
					{
						waveform = this._config.SelectWaveform(intensity);
						hapticLevel = (Byte)(intensity * 100 / 255);
						intervalMs = this._config.CalculateDynamicInterval(intensity) / 10;
					}

					this.Invoke(() => this.SetStatus(
						$"Motor: {intensity}/255 | Level: {hapticLevel}% | Interval: {intervalMs}ms | {waveform}",
						Color.Blue));

					// Direct HID++ haptic
					if (hapticLevel != this._lastHapticLevel)
					{
						this._device.SetHapticLevel(hapticLevel);
						this._lastHapticLevel = hapticLevel;
					}
					this._device.PlayWaveform(this.MapWaveformNameToId(waveform));

					var delayStart = sw.ElapsedTicks;
					var targetTicks = intervalMs * Stopwatch.Frequency / 1000;
					while (sw.ElapsedTicks - delayStart < targetTicks && this._testAllRunning)
					{
						Thread.SpinWait(10);
					}
				}

				// Reset level
				this._device?.SetHapticLevel(0);
				this._lastHapticLevel = 0;

				this._testAllRunning = false;
				this.Invoke(() =>
				{
					var mode = this._config.EnableSimpleMode ? "Simple" : "PDM";
					this.SetStatus($"{mode} mode sweep completed (1→255)", Color.Green);
				});
			})
			{ IsBackground = true, Priority = ThreadPriority.AboveNormal }.Start();
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

		private void BtnTestAll_Click(Object sender, EventArgs e)
		{
			this.BtnTestSweep_Click(sender, e);
		}

		private void BtnApply_Click(Object sender, EventArgs e)
		{
			this.SaveUIToConfig();
			this._config.Save();
			this.SetStatus("Config saved (service auto-reloads)", Color.Green);
		}

		private void BtnSave_Click(Object sender, EventArgs e)
		{
			this.SaveUIToConfig();
			this._config.Save();
			this.SetStatus($"Config saved to {HapticConfig.ConfigPath}", Color.Green);
		}

		private void BtnReset_Click(Object sender, EventArgs e)
		{
			this._config = new HapticConfig();
			this.LoadConfigToUI();
			this.SetStatus("Reset to defaults", Color.Blue);
		}

		#endregion

		private void SetStatus(String text, Color color)
		{
			this._lblStatus.Text = text;
			this._lblStatus.ForeColor = color;
		}
	}
}
