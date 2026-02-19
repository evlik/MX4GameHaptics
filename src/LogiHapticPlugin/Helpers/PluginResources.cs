namespace Loupedeck.LogiHapticPlugin
{
    using System;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Static helper for accessing plugin resources.
    /// </summary>
    internal static class PluginResources
    {
        private static Assembly _assembly;

        public static void Init(Assembly assembly) => _assembly = assembly;

        public static Stream GetResourceStream(String resourceName)
        {
            return _assembly?.GetManifestResourceStream(resourceName);
        }

        public static String[] GetResourceNames()
        {
            return _assembly?.GetManifestResourceNames() ?? Array.Empty<String>();
        }
    }
}
