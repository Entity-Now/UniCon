using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using UniCon.Core;
using UniCon.Core.Models;

namespace UniCon.Drivers.Mqtt
{
    public class MqttDriver : DriverBase
    {
        private IMqttClient? _mqttClient;
        private readonly ConcurrentDictionary<string, Action<DataValue<object>>> _subscriptions = new();

        public MqttDriver(string driverId, ILogger logger) : base(driverId, logger)
        {
        }

        protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            var parts = connectionString.Split(';');
            var builder = new MqttClientOptionsBuilder();
            string server = "127.0.0.1";
            int port = 1883;
            string? username = null;
            string? password = null;
            bool cleanSession = true;
            int keepAlive = 15;
            bool useTls = false;

            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLower();
                var val = kv[1].Trim();

                switch (key)
                {
                    case "server":
                    case "host":
                        server = val;
                        break;
                    case "port":
                        port = int.Parse(val);
                        break;
                    case "clientid":
                        builder.WithClientId(val);
                        break;
                    case "username":
                    case "user":
                        username = val;
                        break;
                    case "password":
                    case "pwd":
                        password = val;
                        break;
                    case "cleansession":
                        cleanSession = bool.TryParse(val, out var clean) ? clean : cleanSession;
                        break;
                    case "keepalive":
                        keepAlive = int.Parse(val);
                        break;
                    case "usetls":
                    case "tls":
                        useTls = bool.TryParse(val, out var tls) ? tls : useTls;
                        break;
                }
            }

            builder.WithTcpServer(server, port);
            if (!string.IsNullOrEmpty(username))
            {
                builder.WithCredentials(username, password);
            }
            builder.WithCleanSession(cleanSession);
            builder.WithKeepAlivePeriod(TimeSpan.FromSeconds(keepAlive));
            if (useTls)
            {
                builder.WithTlsOptions(o => o.UseTls(true));
            }

            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                if (_subscriptions.TryGetValue(e.ApplicationMessage.Topic, out var callback))
                {
                    var val = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    callback(new DataValue<object> { Value = val, Status = DataStatus.Good, SourceTimestamp = DateTime.Now });
                }
                return Task.CompletedTask;
            };

            await _mqttClient.ConnectAsync(builder.Build(), ct);
            return _mqttClient.IsConnected;
        }

        protected override async Task OnDisconnectAsync(CancellationToken ct)
        {
            if (_mqttClient != null)
            {
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), ct);
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }

        protected override Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct) =>
            Task.FromResult(UniconResponse<T>.CreateFailure("MQTT is pub/sub only.", 405));

        protected override async Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
        {
            try
            {
                var message = new MqttApplicationMessageBuilder().WithTopic(request.Address).WithPayload(value?.ToString() ?? "").Build();
                await _mqttClient!.PublishAsync(message, ct);
                return UniconResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        public override async Task SubscribeAsync(string address, Action<DataValue<object>> callback, CancellationToken ct = default)
        {
            _subscriptions.TryAdd(address, callback);
            await _mqttClient!.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(address).Build(), ct);
        }

        public override async Task UnsubscribeAsync(string address, CancellationToken ct = default)
        {
            _subscriptions.TryRemove(address, out _);
            await _mqttClient!.UnsubscribeAsync(new MqttClientUnsubscribeOptionsBuilder().WithTopicFilter(address).Build(), ct);
        }

        public override void Dispose()
        {
            _mqttClient?.Dispose();
            _syncLock.Dispose();
            _connectionLock.Dispose();
        }
    }
}
