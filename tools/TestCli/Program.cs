using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LogiHapticPlugin;
using Microsoft.Extensions.Logging;

namespace MX4TestCli;

/// <summary>
/// Command-line interface for testing MX4GameHaptics functionality.
/// Allows testing haptic feedback without requiring a game.
/// </summary>
internal class Program
{
    private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    });

    /// <summary>
    /// Entry point for the test CLI.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code (0 for success).</returns>
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "vibrate" => await HandleVibrate(args),
                "waveform" => HandleWaveform(args),
                "sequence" => await HandleSequence(args),
                "list" => HandleList(args),
                "test" => await HandleTest(),
                "server" => await HandleServer(),
                "help" or "--help" or "-h" => PrintUsage(),
                _ => PrintUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the 'vibrate' command - sends motor values through the pipe.
    /// </summary>
    private static async Task<int> HandleVibrate(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: mx4test vibrate <leftMotor> <rightMotor>");
            Console.Error.WriteLine("       Values are 0-255");
            return 1;
        }

        if (!byte.TryParse(args[1], out byte leftMotor) ||
            !byte.TryParse(args[2], out byte rightMotor))
        {
            Console.Error.WriteLine("Error: Motor values must be 0-255");
            return 1;
        }

        Console.WriteLine($"Sending vibration: L={leftMotor}, R={rightMotor}");

        return await SendVibrationToPipe(leftMotor, rightMotor) ? 0 : 1;
    }

    /// <summary>
    /// Handles the 'waveform' command - triggers a specific waveform directly.
    /// </summary>
    private static int HandleWaveform(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: mx4test waveform <index|name>");
            Console.Error.WriteLine("       Index: 0-14");
            Console.Error.WriteLine("       Name: sharp_collision, knock, wave, etc.");
            return 1;
        }

        Waveform waveform;

        if (int.TryParse(args[1], out int index))
        {
            if (index < 0 || index > 14)
            {
                Console.Error.WriteLine("Error: Waveform index must be 0-14");
                return 1;
            }
            waveform = (Waveform)index;
        }
        else
        {
            if (!TryParseWaveformName(args[1], out waveform))
            {
                Console.Error.WriteLine($"Error: Unknown waveform name: {args[1]}");
                Console.Error.WriteLine("Use 'mx4test list waveforms' to see available waveforms");
                return 1;
            }
        }

        Console.WriteLine($"Triggering waveform: {waveform} (index {(int)waveform})");

        var hapticService = new HapticService(LoggerFactory.CreateLogger<HapticService>());
        return hapticService.TriggerHaptic(waveform) ? 0 : 1;
    }

    /// <summary>
    /// Handles the 'sequence' command - plays a preset haptic sequence.
    /// </summary>
    private static async Task<int> HandleSequence(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: mx4test sequence <name>");
            Console.Error.WriteLine("Use 'mx4test list sequences' to see available sequences");
            return 1;
        }

        var hapticService = new HapticService(LoggerFactory.CreateLogger<HapticService>());
        var sequenceEngine = new SequenceEngine(hapticService, LoggerFactory.CreateLogger<SequenceEngine>());

        string sequenceName = args[1];
        Console.WriteLine($"Playing sequence: {sequenceName}");

        try
        {
            await sequenceEngine.PlayPresetAsync(sequenceName);
            Console.WriteLine("Sequence completed");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the 'list' command - lists waveforms or sequences.
    /// </summary>
    private static int HandleList(string[] args)
    {
        string listType = args.Length > 1 ? args[1].ToLowerInvariant() : "all";

        if (listType is "waveforms" or "all")
        {
            Console.WriteLine("Available Waveforms:");
            Console.WriteLine("====================");
            foreach (Waveform wf in Enum.GetValues<Waveform>())
            {
                Console.WriteLine($"  {(int)wf,2}: {wf}");
            }
            Console.WriteLine();
        }

        if (listType is "sequences" or "all")
        {
            var hapticService = new HapticService();
            var sequenceEngine = new SequenceEngine(hapticService);

            Console.WriteLine("Available Sequences:");
            Console.WriteLine("====================");
            foreach (string name in sequenceEngine.GetPresetNames())
            {
                Console.WriteLine($"  {name}");
            }
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Handles the 'test' command - runs a quick test of all waveforms.
    /// </summary>
    private static async Task<int> HandleTest()
    {
        Console.WriteLine("Running haptic test...");
        Console.WriteLine("This will play all waveforms with a 500ms delay between each.");
        Console.WriteLine("Press Ctrl+C to cancel.");
        Console.WriteLine();

        var hapticService = new HapticService(LoggerFactory.CreateLogger<HapticService>());

        await hapticService.PlayAllWaveformsAsync(500);

        Console.WriteLine("Test completed");
        return 0;
    }

    /// <summary>
    /// Handles the 'server' command - starts the pipe server for testing.
    /// </summary>
    private static async Task<int> HandleServer()
    {
        Console.WriteLine("Starting MX4GameHaptics pipe server...");
        Console.WriteLine($"Pipe: {Constants.FullPipePath}");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var hapticService = new HapticService(LoggerFactory.CreateLogger<HapticService>());
        var logger = LoggerFactory.CreateLogger<VibrationPipeServer>();

        var server = new VibrationPipeServer(
            Constants.PipeName,
            (left, right) =>
            {
                var waveform = WaveformMapper.MapToWaveform(left, right);
                Console.WriteLine($"Received: L={left,3}, R={right,3} -> {waveform}");
                hapticService.TriggerHaptic(waveform);
            },
            logger
        );

        await server.StartAsync(cts.Token);

        Console.WriteLine("Server running. Waiting for connections...");

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }

        await server.StopAsync();
        Console.WriteLine("Server stopped");

        return 0;
    }

    /// <summary>
    /// Sends vibration data to the named pipe.
    /// </summary>
    private static async Task<bool> SendVibrationToPipe(byte leftMotor, byte rightMotor)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.Out);

            Console.WriteLine("Connecting to pipe server...");
            await pipe.ConnectAsync(5000);

            byte[] buffer = [leftMotor, rightMotor];
            await pipe.WriteAsync(buffer);
            await pipe.FlushAsync();

            Console.WriteLine("Vibration data sent successfully");
            return true;
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("Error: Could not connect to pipe server (timeout)");
            Console.Error.WriteLine("Make sure the server is running: mx4test server");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error sending to pipe: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parses a waveform name string to enum value.
    /// </summary>
    private static bool TryParseWaveformName(string name, out Waveform waveform)
    {
        // Try direct enum parse first
        if (Enum.TryParse(name, ignoreCase: true, out waveform))
        {
            return true;
        }

        // Try snake_case conversion
        string pascalCase = string.Join("",
            name.Split('_')
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

        return Enum.TryParse(pascalCase, ignoreCase: true, out waveform);
    }

    /// <summary>
    /// Prints usage information.
    /// </summary>
    private static int PrintUsage()
    {
        Console.WriteLine("MX4GameHaptics Test CLI");
        Console.WriteLine("=======================");
        Console.WriteLine();
        Console.WriteLine("Usage: mx4test <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  vibrate <left> <right>    Send vibration (0-255 each)");
        Console.WriteLine("  waveform <index|name>     Trigger specific waveform (0-14)");
        Console.WriteLine("  sequence <name>           Play preset sequence");
        Console.WriteLine("  list [waveforms|sequences] List available options");
        Console.WriteLine("  test                      Play all waveforms sequentially");
        Console.WriteLine("  server                    Start pipe server for testing");
        Console.WriteLine("  help                      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  mx4test vibrate 255 128         High left, medium right motor");
        Console.WriteLine("  mx4test waveform 0              Trigger sharp_collision");
        Console.WriteLine("  mx4test waveform knock          Trigger knock by name");
        Console.WriteLine("  mx4test sequence explosion      Play explosion sequence");
        Console.WriteLine("  mx4test list                    List all waveforms and sequences");
        Console.WriteLine();

        return 0;
    }

    /// <summary>
    /// Prints unknown command error.
    /// </summary>
    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Use 'mx4test help' for usage information");
        return 1;
    }
}
