using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Explores PluginApi.dll to find haptic-related methods and classes.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        string dllPath = @"C:\Program Files\Logi\LogiPluginService\PluginApi.dll";

        if (!File.Exists(dllPath))
        {
            Console.WriteLine($"ERROR: {dllPath} not found!");
            return;
        }

        Console.WriteLine($"Loading: {dllPath}");
        Console.WriteLine(new string('=', 60));

        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var types = assembly.GetExportedTypes();

            Console.WriteLine($"\nTotal public types: {types.Length}\n");

            // Search for haptic-related types
            string[] searchTerms = { "haptic", "vibr", "feedback", "motor", "rumble", "tactile", "device", "mouse" };

            Console.WriteLine("=== SEARCHING FOR HAPTIC-RELATED TYPES ===\n");

            foreach (var type in types)
            {
                string typeName = type.FullName?.ToLower() ?? "";
                bool matches = searchTerms.Any(term => typeName.Contains(term));

                if (matches)
                {
                    PrintTypeInfo(type);
                }
            }

            Console.WriteLine("\n=== ALL TYPES IN Loupedeck NAMESPACE ===\n");

            foreach (var type in types.Where(t => t.Namespace?.StartsWith("Loupedeck") == true).OrderBy(t => t.FullName))
            {
                Console.WriteLine($"  {type.FullName}");
            }

            Console.WriteLine("\n=== PLUGIN BASE CLASS METHODS ===\n");

            var pluginType = types.FirstOrDefault(t => t.Name == "Plugin" && t.IsAbstract);
            if (pluginType != null)
            {
                PrintTypeInfo(pluginType, showAllMethods: true);
            }

            Console.WriteLine("\n=== DEVICE-RELATED CLASSES ===\n");

            foreach (var type in types.Where(t =>
                t.Name.Contains("Device") ||
                t.Name.Contains("Input") ||
                t.Name.Contains("Touch") ||
                t.Name.Contains("Surface")))
            {
                PrintTypeInfo(type);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    static void PrintTypeInfo(Type type, bool showAllMethods = false)
    {
        Console.WriteLine($"TYPE: {type.FullName}");
        Console.WriteLine($"   Base: {type.BaseType?.Name}");
        Console.WriteLine($"   IsClass: {type.IsClass}, IsInterface: {type.IsInterface}, IsAbstract: {type.IsAbstract}");

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        if (methods.Length > 0)
        {
            Console.WriteLine("   Methods:");
            foreach (var method in methods.Take(showAllMethods ? 100 : 20))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"      - {method.ReturnType.Name} {method.Name}({parameters})");
            }
            if (!showAllMethods && methods.Length > 20)
            {
                Console.WriteLine($"      ... and {methods.Length - 20} more methods");
            }
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (properties.Length > 0)
        {
            Console.WriteLine("   Properties:");
            foreach (var prop in properties.Take(15))
            {
                Console.WriteLine($"      - {prop.PropertyType.Name} {prop.Name}");
            }
        }

        Console.WriteLine();
    }
}
