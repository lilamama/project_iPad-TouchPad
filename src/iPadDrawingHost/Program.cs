using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IPadDrawingHost.Input;
using IPadDrawingHost.Native;
using IPadDrawingHost.Network;

namespace IPadDrawingHost;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // 1. Enable Per-Monitor V2 DPI awareness for pixel-accurate coordinate mapping
        try
        {
            Win32Pointer.SetProcessDpiAwarenessContext(Win32Pointer.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Fallback for older builds
        }

        PrintBanner();

        int port = 8080;
        ScreenMappingMode screenMode = ScreenMappingMode.PrimaryScreen;
        SqueezeAction squeezeAction = SqueezeAction.EraserToggle;
        bool maintainAspect = true;

        // Parse CLI arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out port);
            }
            else if (args[i].Equals("--screen", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                string s = args[++i].ToLowerInvariant();
                if (s == "virtual" || s == "all") screenMode = ScreenMappingMode.VirtualDesktop;
                else if (s == "primary") screenMode = ScreenMappingMode.PrimaryScreen;
            }
            else if (args[i].Equals("--squeeze", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                string sq = args[++i].ToLowerInvariant();
                if (sq == "undo") squeezeAction = SqueezeAction.UndoShortcut;
                else if (sq == "barrel") squeezeAction = SqueezeAction.BarrelButton;
                else if (sq == "hold") squeezeAction = SqueezeAction.EraserHold;
                else squeezeAction = SqueezeAction.EraserToggle;
            }
            else if (args[i].Equals("--no-aspect", StringComparison.OrdinalIgnoreCase))
            {
                maintainAspect = false;
            }
        }

        var mapper = new CoordinateMapper
        {
            Mode = screenMode,
            MaintainAspectRatio = maintainAspect,
            SourceAspectRatio = 4.0 / 3.0 // Standard iPad aspect ratio
        };

        var bounds = mapper.GetTargetBounds();
        Console.WriteLine($"[Display Config] Target Display: {screenMode}");
        Console.WriteLine($"[Display Config] Active Bounds: {bounds.width}x{bounds.height} at ({bounds.left}, {bounds.top})");
        Console.WriteLine($"[Display Config] Maintain Aspect Ratio: {maintainAspect} (Source: 4:3)");
        Console.WriteLine($"[Stylus Config] Squeeze Trigger: {squeezeAction}");
        Console.WriteLine();

        DisplayLocalIpAddresses(port);

        using var penEngine = new PenStateEngine(mapper)
        {
            SqueezeBehavior = squeezeAction
        };

        var server = new UdpPenServer(port, penEngine);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Shutdown] Stopping UDP Server and releasing pen device...");
            cts.Cancel();
        };

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("===============================================================================");
        Console.WriteLine(" Ready to receive! In drawing apps (Photoshop / Krita), ensure Windows Ink is ON.");
        Console.WriteLine(" Press Ctrl+C to stop.");
        Console.WriteLine("===============================================================================\n");
        Console.ResetColor();

        await server.StartAsync(cts.Token);
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ___ ____            _   _____             _     ____        _   
 |_ _|  _ \ __ _   __| | |_   _|__  _   _  | |__ |  _ \ __ _ | |_ 
  | || |_) / _` | / _` |   | |/ _ \| | | | | '_ \| |_) / _` || __|
  | ||  __/ (_| || (_| |   | | (_) | |_| |_| | | |  __/ (_| || |_ 
 |___|_|   \__,_| \__,_|   |_|\___/ \__,_(_)_| |_|_|   \__,_| \__|
        Windows Ink Host for iPad Drawing & Apple Pencil Pro
");
        Console.ResetColor();
    }

    private static void DisplayLocalIpAddresses(int port)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Network] Connect your iPad app to one of the following IP addresses on port {port}:");
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    Console.WriteLine($"    -> {ip}:{port}");
                }
            }
            Console.WriteLine($"    -> 127.0.0.1:{port} (Localhost)");
        }
        catch
        {
            Console.WriteLine($"    -> 127.0.0.1:{port}");
        }
        Console.ResetColor();
        Console.WriteLine();
    }
}
