namespace Loupedeck.LogiHapticPlugin
{
    using System;

    /// <summary>
    /// Application class for the LogiHaptic plugin.
    /// This plugin does not connect to any specific application.
    /// </summary>
    public class LogiHapticApplication : ClientApplication
    {
        public LogiHapticApplication()
        {
        }

        protected override String GetProcessName() => "";
        protected override String GetBundleName() => "";
        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
