using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace UniCon.Drivers.OpcUaPubSub.Transports;

/// <summary>
/// 基于 UDP 组播的 OPC UA UADP 传输实现
/// </summary>
public class UdpPubSubTransport : IPubSubTransport
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public event EventHandler<byte[]>? OnMessageReceived;
    public event EventHandler? ConnectionLost;

    public Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        var port = uri.Port > 0 ? uri.Port : Constants.PubSubConstants.DefaultUdpPort;
        var multicastAddress = IPAddress.Parse(uri.Host);

        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        var localEp = new IPEndPoint(IPAddress.Any, port);
        _udpClient.Client.Bind(localEp);
        _udpClient.JoinMulticastGroup(multicastAddress);

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        if (_udpClient == null) return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var result = await _udpClient.ReceiveAsync(token);
                OnMessageReceived?.Invoke(this, result.Buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }
        catch (Exception ex)
        {
            // Handle or log socket errors
            Console.WriteLine($"[UdpTransport] Receive loop error: {ex.Message}");
            if (!token.IsCancellationRequested)
            {
                ConnectionLost?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
            _receiveTask = null;
        }

        if (_udpClient != null)
        {
            try
            {
                _udpClient.DropMulticastGroup(IPAddress.Any); // Simplified for teardown
            }
            catch { }

            _udpClient.Close();
            _udpClient.Dispose();
            _udpClient = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
