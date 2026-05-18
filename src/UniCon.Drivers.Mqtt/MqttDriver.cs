using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Network;

namespace UniCon.Drivers.Mqtt
{
    /// <summary>
    /// MQTT 驱动：Push 型协议，不进入 Polling Scheduler。
    /// 订阅由 MQTT broker 消息事件驱动，接收后通过 Channel 分发（via NotificationDispatcher）。
    /// </summary>
    [UniconDriver("Mqtt")]
    public class MqttDriver : DriverBase
    {
        private IMqttClient? _mqttClient;

        // callback 改为异步 Func，与 IUniconDriver 接口对齐
        private readonly ConcurrentDictionary<string, Func<DataValue<object>, Task>> _subscriptions = new();

        public MqttDriver(string driverId, ILogger logger, IUniconCacheProvider cacheProvider, INetworkMonitor networkMonitor)
            : base(driverId, logger, cacheProvider, networkMonitor)
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
                    case "host": server = val; break;
                    case "port": port = int.Parse(val); break;
                    case "clientid": builder.WithClientId(val); break;
                    case "username":
                    case "user": username = val; break;
                    case "password":
                    case "pwd": password = val; break;
                    case "cleansession": cleanSession = bool.TryParse(val, out var c) ? c : cleanSession; break;
                    case "keepalive": keepAlive = int.Parse(val); break;
                    case "usetls":
                    case "tls": useTls = bool.TryParse(val, out var t) ? t : useTls; break;
                }
            }

            builder.WithTcpServer(server, port);
            if (!string.IsNullOrEmpty(username))
                builder.WithCredentials(username, password);
            builder.WithCleanSession(cleanSession);
            builder.WithKeepAlivePeriod(TimeSpan.FromSeconds(keepAlive));
            if (useTls)
                builder.WithTlsOptions(o => o.UseTls(true));

            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.DisconnectedAsync += e =>
            {
                if (State == DriverState.Connected)
                {
                    _logger.LogWarning("[Driver:{Id}] MQTT client unexpectedly disconnected. Transitioning to Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
                return Task.CompletedTask;
            };

            await _mqttClient.ConnectAsync(builder.Build(), ct);
            return _mqttClient.IsConnected;
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            if (!_subscriptions.TryGetValue(e.ApplicationMessage.Topic, out var callback)) return;

            var dataValue = new DataValue<object>
            {
                Value = Encoding.UTF8.GetString(e.ApplicationMessage.Payload),
                Status = DataStatus.Good,
                SourceTimestamp = DateTime.UtcNow,
                ServerTimestamp = DateTime.UtcNow
            };

            try
            {
                await callback(dataValue);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[MqttDriver:{Id}] Callback error for topic '{Topic}'", DriverId, e.ApplicationMessage.Topic);
            }
        }

        protected override async Task OnDisconnectAsync(CancellationToken ct)
        {
            if (_mqttClient is null) return;
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), ct);
            _mqttClient.Dispose();
            _mqttClient = null;
        }

        protected override Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
            => Task.FromResult(UniconResponse<T>.CreateFailure("MQTT is pub/sub only.", 405));

        protected override async Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
        {
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(request.Address)
                    .WithPayload(value?.ToString() ?? string.Empty)
                    .Build();

                await _mqttClient!.PublishAsync(message, ct);
                return UniconResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        /// <summary>
        /// MQTT 订阅直接绑定 broker topic，不进入 PollingScheduler。
        /// </summary>
        public override async Task<string> SubscribeAsync(
            string address,
            Func<DataValue<object>, Task> callback,
            CancellationToken ct = default)
        {
            _subscriptions[address] = callback;
            await _mqttClient!.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter(address).Build(), ct);

            return address; // MQTT 以 topic 作为订阅 ID
        }

        public override async Task UnsubscribeAsync(string address, CancellationToken ct = default)
        {
            _subscriptions.TryRemove(address, out _);
            if (_mqttClient is not null)
                await _mqttClient.UnsubscribeAsync(
                    new MqttClientUnsubscribeOptionsBuilder().WithTopicFilter(address).Build(), ct);
        }

        public override void Dispose()
        {
            _mqttClient?.Dispose();
            base.Dispose();
        }
    }
}
