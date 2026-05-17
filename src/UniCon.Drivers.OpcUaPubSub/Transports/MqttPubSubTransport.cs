using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace UniCon.Drivers.OpcUaPubSub.Transports;

/// <summary>
/// 基于 MQTT 的 OPC UA JSON/UADP 传输实现
/// </summary>
public class MqttPubSubTransport : IPubSubTransport
{
    private IMqttClient? _mqttClient;
    public event EventHandler<byte[]>? OnMessageReceived;

    public async Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var port = uri.Port > 0 ? uri.Port : Constants.PubSubConstants.DefaultMqttPort;
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(uri.Host, port)
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.PayloadSegment.Array != null)
            {
                OnMessageReceived?.Invoke(this, e.ApplicationMessage.PayloadSegment.ToArray());
            }
            return Task.CompletedTask;
        };

        await _mqttClient.ConnectAsync(options, ct);

        // Topic should normally be configured or extracted from URI query/path
        // For simplicity, we assume the URI absolute path is the topic.
        var topic = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
            ? "opcua/pubsub"
            : uri.AbsolutePath.TrimStart('/');

        await _mqttClient.SubscribeAsync(topic, cancellationToken: ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_mqttClient != null)
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), ct);
            }
            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
