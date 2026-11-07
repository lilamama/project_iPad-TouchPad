using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IPadDrawingHost.Input;

namespace IPadDrawingHost.Network;

public class UdpPenServer
{
    private readonly int _port;
    private readonly PenStateEngine _engine;
    private readonly JsonSerializerOptions _jsonOptions;
    private long _totalPackets = 0;
    private long _packetsInLastSecond = 0;
    private DateTime _lastStatsReport = DateTime.UtcNow;

    public UdpPenServer(int port, PenStateEngine engine)
    {
        _port = port;
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var client = new UdpClient();
        var socket = client.Client;

        // Windows UDP Socket bug fix: Ignore WSAECONNRESET (10054)
        const int SIO_UDP_CONNRESET = -1744830452;
        try
        {
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (PlatformNotSupportedException)
        {
            // SIO_UDP_CONNRESET is Windows specific
        }

        socket.ReceiveBufferSize = 1024 * 1024; // 1 MB buffer for high frequency 240Hz touches
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, _port));

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[UDP Server] Listening for iPad pen packets on UDP port {_port}...");
        Console.WriteLine($"[UDP Server] Ready for connections from local network.");
        Console.ResetColor();

        byte[] buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync(cancellationToken);
                _totalPackets++;
                _packetsInLastSecond++;

                var payload = JsonSerializer.Deserialize<PenPayload>(result.Buffer, _jsonOptions);
                if (payload != null)
                {
                    _engine.ProcessPayload(payload);
                }

                // Periodic telemetry (every 2 seconds)
                var now = DateTime.UtcNow;
                if ((now - _lastStatsReport).TotalSeconds >= 2.0)
                {
                    double pps = _packetsInLastSecond / (now - _lastStatsReport).TotalSeconds;
                    _packetsInLastSecond = 0;
                    _lastStatsReport = now;

                    if (pps > 0)
                    {
                        Console.WriteLine($"[Status] Active - Receiving: {pps:F1} packets/sec | Last client: {result.RemoteEndPoint}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (JsonException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[JSON Error] Failed to parse pen packet: {ex.Message}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[UDP Error] {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
