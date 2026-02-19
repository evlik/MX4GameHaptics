using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HapticConfigurator
{
	/// <summary>
	/// Dialog for editing interval curve points.
	/// Allows visual curve editing with drag-and-drop point manipulation.
	/// </summary>
	public class CurveEditorForm : Form
	{
		private readonly String _waveformName;
		private readonly Double _intervalMinMs;
		private readonly Double _intervalMaxMs;

		private List<CurvePoint> _points;
		private Panel _curvePanel;
		private DataGridView _pointsGrid;
		private Button _btnAddPoint;
		private Button _btnRemovePoint;
		private Button _btnReset;
		private Button _btnOk;
		private Button _btnCancel;
		private Label _lblInfo;

		// Curve panel margins
		private const Int32 MarginLeft = 50;
		private const Int32 MarginRight = 20;
		private const Int32 MarginTop = 20;
		private const Int32 MarginBottom = 40;
		private const Int32 PointRadius = 6;

		// Dragging state
		private Int32 _dragPointIndex = -1;
		private Boolean _isDragging = false;

		public CurveEditorForm(
			List<CurvePoint> currentPoints,
			String waveformName,
			Double intervalMinMs,
			Double intervalMaxMs)
		{
			this._waveformName = waveformName;
			this._intervalMinMs = intervalMinMs;
			this._intervalMaxMs = intervalMaxMs;

			// Deep copy the points
			this._points = currentPoints?.Select(p =>
				new CurvePoint { Position = p.Position, Value = p.Value }).ToList()
				?? new List<CurvePoint>
				{
					new CurvePoint { Position = 0.0, Value = 1.0 },
					new CurvePoint { Position = 1.0, Value = 0.0 }
				};

			this.InitializeComponent();
			this.PopulateGrid();
		}

		private void InitializeComponent()
		{
			this.Text = $"Curve Editor - {this._waveformName}";
			this.Size = new Size(600, 500);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.StartPosition = FormStartPosition.CenterParent;

			// Info label
			this._lblInfo = new Label
			{
				Text = $"Interval range: {this._intervalMinMs:F0}ms (fast) to {this._intervalMaxMs:F0}ms (slow)\n" +
					   "X-axis: Intensity (0% to 100%), Y-axis: Interval (min to max)\n" +
					   "Click to add points, drag to move, select in grid to remove",
				Location = new Point(10, 10),
				Size = new Size(560, 50),
				ForeColor = Color.DarkBlue
			};
			this.Controls.Add(this._lblInfo);

			// Curve panel
			this._curvePanel = new Panel
			{
				Location = new Point(10, 65),
				Size = new Size(360, 250),
				BorderStyle = BorderStyle.FixedSingle,
				BackColor = Color.White
			};
			this._curvePanel.Paint += this.CurvePanel_Paint;
			this._curvePanel.MouseDown += this.CurvePanel_MouseDown;
			this._curvePanel.MouseMove += this.CurvePanel_MouseMove;
			this._curvePanel.MouseUp += this.CurvePanel_MouseUp;
			this.Controls.Add(this._curvePanel);

			// Points grid
			this._pointsGrid = new DataGridView
			{
				Location = new Point(380, 65),
				Size = new Size(190, 200),
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				MultiSelect = false,
				RowHeadersVisible = false,
				AllowUserToResizeRows = false
			};

			var colPos = new DataGridViewTextBoxColumn
			{
				Name = "Position",
				HeaderText = "Intensity %",
				Width = 80
			};
			this._pointsGrid.Columns.Add(colPos);

			var colVal = new DataGridViewTextBoxColumn
			{
				Name = "Value",
				HeaderText = "Interval ms",
				Width = 80
			};
			this._pointsGrid.Columns.Add(colVal);

			this._pointsGrid.CellEndEdit += this.PointsGrid_CellEndEdit;
			this.Controls.Add(this._pointsGrid);

			// Add point button
			this._btnAddPoint = new Button
			{
				Text = "+ Add Point",
				Location = new Point(380, 270),
				Size = new Size(90, 25)
			};
			this._btnAddPoint.Click += this.BtnAddPoint_Click;
			this.Controls.Add(this._btnAddPoint);

			// Remove point button
			this._btnRemovePoint = new Button
			{
				Text = "- Remove",
				Location = new Point(475, 270),
				Size = new Size(90, 25)
			};
			this._btnRemovePoint.Click += this.BtnRemovePoint_Click;
			this.Controls.Add(this._btnRemovePoint);

			// Reset button
			this._btnReset = new Button
			{
				Text = "Reset to Linear",
				Location = new Point(380, 300),
				Size = new Size(185, 25)
			};
			this._btnReset.Click += this.BtnReset_Click;
			this.Controls.Add(this._btnReset);

			// Preset buttons
			var btnPresetLog = new Button
			{
				Text = "Logarithmic",
				Location = new Point(10, 325),
				Size = new Size(85, 25)
			};
			btnPresetLog.Click += (s, e) => this.ApplyPreset("log");
			this.Controls.Add(btnPresetLog);

			var btnPresetExp = new Button
			{
				Text = "Exponential",
				Location = new Point(100, 325),
				Size = new Size(85, 25)
			};
			btnPresetExp.Click += (s, e) => this.ApplyPreset("exp");
			this.Controls.Add(btnPresetExp);

			var btnPresetS = new Button
			{
				Text = "S-Curve",
				Location = new Point(190, 325),
				Size = new Size(85, 25)
			};
			btnPresetS.Click += (s, e) => this.ApplyPreset("s");
			this.Controls.Add(btnPresetS);

			var btnPresetStep = new Button
			{
				Text = "Step",
				Location = new Point(280, 325),
				Size = new Size(85, 25)
			};
			btnPresetStep.Click += (s, e) => this.ApplyPreset("step");
			this.Controls.Add(btnPresetStep);

			// OK button
			this._btnOk = new Button
			{
				Text = "OK",
				Location = new Point(380, 420),
				Size = new Size(90, 30),
				DialogResult = DialogResult.OK
			};
			this.Controls.Add(this._btnOk);

			// Cancel button
			this._btnCancel = new Button
			{
				Text = "Cancel",
				Location = new Point(475, 420),
				Size = new Size(90, 30),
				DialogResult = DialogResult.Cancel
			};
			this.Controls.Add(this._btnCancel);

			this.AcceptButton = this._btnOk;
			this.CancelButton = this._btnCancel;
		}

		private void PopulateGrid()
		{
			this._pointsGrid.Rows.Clear();
			var sorted = this._points.OrderBy(p => p.Position).ToList();

			foreach (var point in sorted)
			{
				var intensity = point.Position * 100;
				var interval = this._intervalMinMs + (this._intervalMaxMs - this._intervalMinMs) * point.Value;

				var rowIdx = this._pointsGrid.Rows.Add();
				this._pointsGrid.Rows[rowIdx].Cells["Position"].Value = $"{intensity:F0}";
				this._pointsGrid.Rows[rowIdx].Cells["Value"].Value = $"{interval:F0}";
			}
		}

		private void CurvePanel_Paint(Object sender, PaintEventArgs e)
		{
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			var w = this._curvePanel.Width;
			var h = this._curvePanel.Height;
			var plotW = w - MarginLeft - MarginRight;
			var plotH = h - MarginTop - MarginBottom;

			// Draw grid
			using (var gridPen = new Pen(Color.LightGray, 1))
			{
				gridPen.DashStyle = DashStyle.Dot;
				for (var i = 0; i <= 4; i++)
				{
					var x = MarginLeft + (plotW * i / 4);
					var y = MarginTop + (plotH * i / 4);
					g.DrawLine(gridPen, x, MarginTop, x, MarginTop + plotH);
					g.DrawLine(gridPen, MarginLeft, y, MarginLeft + plotW, y);
				}
			}

			// Draw axes
			using (var axisPen = new Pen(Color.Black, 1))
			{
				g.DrawLine(axisPen, MarginLeft, MarginTop, MarginLeft, MarginTop + plotH);
				g.DrawLine(axisPen, MarginLeft, MarginTop + plotH, MarginLeft + plotW, MarginTop + plotH);
			}

			// Draw axis labels
			using (var font = new Font("Arial", 8))
			{
				// X axis labels (intensity)
				g.DrawString("0%", font, Brushes.Black, MarginLeft - 10, MarginTop + plotH + 5);
				g.DrawString("50%", font, Brushes.Black, MarginLeft + plotW / 2 - 10, MarginTop + plotH + 5);
				g.DrawString("100%", font, Brushes.Black, MarginLeft + plotW - 15, MarginTop + plotH + 5);
				g.DrawString("Intensity", font, Brushes.DarkBlue, MarginLeft + plotW / 2 - 20, MarginTop + plotH + 20);

				// Y axis labels (interval)
				var minLabel = $"{this._intervalMinMs:F0}ms";
				var maxLabel = $"{this._intervalMaxMs:F0}ms";
				g.DrawString(minLabel, font, Brushes.Black, 2, MarginTop + plotH - 5);
				g.DrawString(maxLabel, font, Brushes.Black, 2, MarginTop - 5);

				// Rotate and draw Y axis label
				var state = g.Save();
				g.TranslateTransform(12, MarginTop + plotH / 2 + 20);
				g.RotateTransform(-90);
				g.DrawString("Interval", font, Brushes.DarkBlue, 0, 0);
				g.Restore(state);
			}

			// Sort points and draw curve
			var sorted = this._points.OrderBy(p => p.Position).ToList();
			if (sorted.Count >= 2)
			{
				var curvePoints = new PointF[sorted.Count];
				for (var i = 0; i < sorted.Count; i++)
				{
					curvePoints[i] = this.NormalizedToScreen(sorted[i].Position, sorted[i].Value, plotW, plotH);
				}

				using (var curvePen = new Pen(Color.Blue, 2))
				{
					g.DrawLines(curvePen, curvePoints);
				}
			}

			// Draw points
			for (var i = 0; i < this._points.Count; i++)
			{
				var point = this._points[i];
				var screenPt = this.NormalizedToScreen(point.Position, point.Value, plotW, plotH);

				var isEndpoint = Math.Abs(point.Position) < 0.001 || Math.Abs(point.Position - 1.0) < 0.001;
				var brush = isEndpoint ? Brushes.DarkRed : Brushes.Red;
				var size = isEndpoint ? PointRadius + 2 : PointRadius;

				g.FillEllipse(brush, screenPt.X - size, screenPt.Y - size, size * 2, size * 2);
				g.DrawEllipse(Pens.DarkRed, screenPt.X - size, screenPt.Y - size, size * 2, size * 2);
			}
		}

		private PointF NormalizedToScreen(Double pos, Double val, Int32 plotW, Int32 plotH)
		{
			// X: position 0-1 -> left to right
			// Y: value 0-1, where 1=max interval (top), 0=min interval (bottom)
			var x = MarginLeft + (Single)(pos * plotW);
			var y = MarginTop + plotH - (Single)(val * plotH);
			return new PointF(x, y);
		}

		private (Double pos, Double val) ScreenToNormalized(Int32 x, Int32 y, Int32 plotW, Int32 plotH)
		{
			var pos = Math.Clamp((x - MarginLeft) / (Double)plotW, 0.0, 1.0);
			var val = Math.Clamp(1.0 - (y - MarginTop) / (Double)plotH, 0.0, 1.0);
			return (pos, val);
		}

		private void CurvePanel_MouseDown(Object sender, MouseEventArgs e)
		{
			var w = this._curvePanel.Width;
			var h = this._curvePanel.Height;
			var plotW = w - MarginLeft - MarginRight;
			var plotH = h - MarginTop - MarginBottom;

			// Check if clicking on existing point
			for (var i = 0; i < this._points.Count; i++)
			{
				var pt = this.NormalizedToScreen(this._points[i].Position, this._points[i].Value, plotW, plotH);
				var dist = Math.Sqrt(Math.Pow(e.X - pt.X, 2) + Math.Pow(e.Y - pt.Y, 2));

				if (dist <= PointRadius + 3)
				{
					this._dragPointIndex = i;
					this._isDragging = true;
					return;
				}
			}

			// If not clicking on a point, add new point
			if (e.X >= MarginLeft && e.X <= MarginLeft + plotW &&
				e.Y >= MarginTop && e.Y <= MarginTop + plotH)
			{
				var (pos, val) = this.ScreenToNormalized(e.X, e.Y, plotW, plotH);
				this._points.Add(new CurvePoint { Position = pos, Value = val });
				this.PopulateGrid();
				this._curvePanel.Invalidate();
			}
		}

		private void CurvePanel_MouseMove(Object sender, MouseEventArgs e)
		{
			if (!this._isDragging || this._dragPointIndex < 0)
				return;

			var w = this._curvePanel.Width;
			var h = this._curvePanel.Height;
			var plotW = w - MarginLeft - MarginRight;
			var plotH = h - MarginTop - MarginBottom;

			var (pos, val) = this.ScreenToNormalized(e.X, e.Y, plotW, plotH);

			// Don't allow moving endpoint X positions
			var isEndpoint = Math.Abs(this._points[this._dragPointIndex].Position) < 0.001 ||
							 Math.Abs(this._points[this._dragPointIndex].Position - 1.0) < 0.001;

			if (isEndpoint)
			{
				// Only allow Y movement for endpoints
				this._points[this._dragPointIndex].Value = val;
			}
			else
			{
				this._points[this._dragPointIndex].Position = pos;
				this._points[this._dragPointIndex].Value = val;
			}

			this.PopulateGrid();
			this._curvePanel.Invalidate();
		}

		private void CurvePanel_MouseUp(Object sender, MouseEventArgs e)
		{
			this._isDragging = false;
			this._dragPointIndex = -1;
		}

		private void PointsGrid_CellEndEdit(Object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= this._points.Count)
				return;

			var sorted = this._points.OrderBy(p => p.Position).ToList();
			var point = sorted[e.RowIndex];
			var origIndex = this._points.IndexOf(point);

			var cell = this._pointsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];

			if (e.ColumnIndex == 0) // Position (intensity %)
			{
				if (Double.TryParse(cell.Value?.ToString(), out var intensity))
				{
					var newPos = Math.Clamp(intensity / 100.0, 0.0, 1.0);
					this._points[origIndex].Position = newPos;
				}
			}
			else if (e.ColumnIndex == 1) // Value (interval ms)
			{
				if (Double.TryParse(cell.Value?.ToString(), out var intervalMs))
				{
					// Convert interval ms back to normalized value
					var range = this._intervalMaxMs - this._intervalMinMs;
					var newVal = range > 0
						? Math.Clamp((intervalMs - this._intervalMinMs) / range, 0.0, 1.0)
						: 0.5;
					this._points[origIndex].Value = newVal;
				}
			}

			this.PopulateGrid();
			this._curvePanel.Invalidate();
		}

		private void BtnAddPoint_Click(Object sender, EventArgs e)
		{
			// Add point at center
			var midPos = 0.5;
			var midVal = 0.5;

			// Find a good position
			var sorted = this._points.OrderBy(p => p.Position).ToList();
			if (sorted.Count >= 2)
			{
				// Find largest gap
				var maxGap = 0.0;
				var gapStart = 0.0;
				for (var i = 0; i < sorted.Count - 1; i++)
				{
					var gap = sorted[i + 1].Position - sorted[i].Position;
					if (gap > maxGap)
					{
						maxGap = gap;
						gapStart = sorted[i].Position;
						midPos = gapStart + gap / 2;

						// Interpolate value
						var t = 0.5;
						midVal = sorted[i].Value + (sorted[i + 1].Value - sorted[i].Value) * t;
					}
				}
			}

			this._points.Add(new CurvePoint { Position = midPos, Value = midVal });
			this.PopulateGrid();
			this._curvePanel.Invalidate();
		}

		private void BtnRemovePoint_Click(Object sender, EventArgs e)
		{
			if (this._pointsGrid.SelectedRows.Count == 0)
				return;

			var rowIdx = this._pointsGrid.SelectedRows[0].Index;
			var sorted = this._points.OrderBy(p => p.Position).ToList();

			if (rowIdx >= 0 && rowIdx < sorted.Count)
			{
				var point = sorted[rowIdx];

				// Don't allow removing endpoints
				if (Math.Abs(point.Position) < 0.001 || Math.Abs(point.Position - 1.0) < 0.001)
				{
					MessageBox.Show("Cannot remove endpoint.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				this._points.Remove(point);
				this.PopulateGrid();
				this._curvePanel.Invalidate();
			}
		}

		private void BtnReset_Click(Object sender, EventArgs e)
		{
			this._points = new List<CurvePoint>
			{
				new CurvePoint { Position = 0.0, Value = 1.0 },
				new CurvePoint { Position = 1.0, Value = 0.0 }
			};
			this.PopulateGrid();
			this._curvePanel.Invalidate();
		}

		private void ApplyPreset(String preset)
		{
			this._points.Clear();

			switch (preset)
			{
				case "log":
					// Logarithmic curve (slow start, fast end)
					this._points.Add(new CurvePoint { Position = 0.0, Value = 1.0 });
					this._points.Add(new CurvePoint { Position = 0.3, Value = 0.8 });
					this._points.Add(new CurvePoint { Position = 0.6, Value = 0.5 });
					this._points.Add(new CurvePoint { Position = 0.8, Value = 0.2 });
					this._points.Add(new CurvePoint { Position = 1.0, Value = 0.0 });
					break;

				case "exp":
					// Exponential curve (fast start, slow end)
					this._points.Add(new CurvePoint { Position = 0.0, Value = 1.0 });
					this._points.Add(new CurvePoint { Position = 0.2, Value = 0.8 });
					this._points.Add(new CurvePoint { Position = 0.4, Value = 0.5 });
					this._points.Add(new CurvePoint { Position = 0.7, Value = 0.2 });
					this._points.Add(new CurvePoint { Position = 1.0, Value = 0.0 });
					break;

				case "s":
					// S-curve (slow start, fast middle, slow end)
					this._points.Add(new CurvePoint { Position = 0.0, Value = 1.0 });
					this._points.Add(new CurvePoint { Position = 0.2, Value = 0.95 });
					this._points.Add(new CurvePoint { Position = 0.4, Value = 0.7 });
					this._points.Add(new CurvePoint { Position = 0.6, Value = 0.3 });
					this._points.Add(new CurvePoint { Position = 0.8, Value = 0.05 });
					this._points.Add(new CurvePoint { Position = 1.0, Value = 0.0 });
					break;

				case "step":
					// Step curve (sudden transitions)
					this._points.Add(new CurvePoint { Position = 0.0, Value = 1.0 });
					this._points.Add(new CurvePoint { Position = 0.3, Value = 1.0 });
					this._points.Add(new CurvePoint { Position = 0.31, Value = 0.5 });
					this._points.Add(new CurvePoint { Position = 0.7, Value = 0.5 });
					this._points.Add(new CurvePoint { Position = 0.71, Value = 0.0 });
					this._points.Add(new CurvePoint { Position = 1.0, Value = 0.0 });
					break;
			}

			this.PopulateGrid();
			this._curvePanel.Invalidate();
		}

		/// <summary>
		/// Returns the edited curve points.
		/// </summary>
		public List<CurvePoint> GetCurvePoints()
		{
			return this._points.OrderBy(p => p.Position)
				.Select(p => new CurvePoint { Position = p.Position, Value = p.Value })
				.ToList();
		}
	}
}
