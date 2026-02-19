using System;
using System.Drawing;
using System.Windows.Forms;

namespace HapticConfigurator
{
	/// <summary>
	/// Simple input dialog for text entry.
	/// </summary>
	public class InputDialog : Form
	{
		private static readonly Color BgDark = Color.FromArgb(32, 32, 32);
		private static readonly Color BgControl = Color.FromArgb(60, 60, 60);
		private static readonly Color FgText = Color.FromArgb(240, 240, 240);
		private static readonly Color Accent = Color.FromArgb(0, 212, 170);

		private readonly TextBox _txtInput;
		private readonly Button _btnOk;
		private readonly Button _btnCancel;

		public String InputText => this._txtInput.Text;

		public InputDialog(String title, String prompt, String defaultValue = "")
		{
			this.Text = title;
			this.Size = new Size(350, 150);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.BackColor = BgDark;
			this.ForeColor = FgText;

			var lblPrompt = new Label
			{
				Text = prompt,
				Location = new Point(15, 15),
				AutoSize = true,
				ForeColor = FgText
			};
			this.Controls.Add(lblPrompt);

			this._txtInput = new TextBox
			{
				Location = new Point(15, 40),
				Size = new Size(305, 25),
				Text = defaultValue,
				BackColor = BgControl,
				ForeColor = FgText,
				BorderStyle = BorderStyle.FixedSingle
			};
			this._txtInput.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Enter)
				{
					this.DialogResult = DialogResult.OK;
					this.Close();
				}
				else if (e.KeyCode == Keys.Escape)
				{
					this.DialogResult = DialogResult.Cancel;
					this.Close();
				}
			};
			this.Controls.Add(this._txtInput);

			this._btnOk = new Button
			{
				Text = "OK",
				Location = new Point(150, 75),
				Size = new Size(80, 28),
				DialogResult = DialogResult.OK,
				FlatStyle = FlatStyle.Flat,
				BackColor = BgControl,
				ForeColor = FgText
			};
			this._btnOk.FlatAppearance.BorderColor = Accent;
			this.Controls.Add(this._btnOk);

			this._btnCancel = new Button
			{
				Text = "Cancel",
				Location = new Point(240, 75),
				Size = new Size(80, 28),
				DialogResult = DialogResult.Cancel,
				FlatStyle = FlatStyle.Flat,
				BackColor = BgControl,
				ForeColor = FgText
			};
			this._btnCancel.FlatAppearance.BorderColor = Color.Gray;
			this.Controls.Add(this._btnCancel);

			this.AcceptButton = this._btnOk;
			this.CancelButton = this._btnCancel;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			this._txtInput.Focus();
			this._txtInput.SelectAll();
		}
	}
}
