using System;
using System.Windows.Forms;

namespace HapticConfigurator
{
	internal static class Program
	{
		[STAThread]
		static void Main()
		{
			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());
		}
	}
}
