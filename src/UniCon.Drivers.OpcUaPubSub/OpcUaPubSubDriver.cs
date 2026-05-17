using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Drivers.OpcUaPubSub.Constants;
using UniCon.Drivers.OpcUaPubSub.Transports;
using UniCon.Drivers.OpcUaPubSub.Decoders;

namespace UniCon.Drivers.OpcUaPubSub;

public class OpcUaPubSubDriver : DriverBase
{
    private IPubSubTransport? _transport;
    private IPubSubDecoder? _decoder;
    private readonly ConcurrentDictionary<string, Func<DataValue<object>, Task>> _subscriptions = new();

    public OpcUaPubSubDriver(string driverId, ILogger logger, IUniconCacheProvider cacheProvider)
        : base(driverId, logger, cacheProvider)
    {
    }

    protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
    {
        var uri = new Uri(connectionString);

        // 1. 初始化传输层
        if (uri.Scheme.Equals(PubSubConstants.SchemeUdp, StringComparison.OrdinalIgnoreCase))
        {
            _transport = new UdpPubSubTransport();
        }
        else if (uri.Scheme.Equals(PubSubConstants.SchemeMqtt, StringComparison.OrdinalIgnoreCase))
        {
            _transport = new MqttPubSubTransport();
        }
        else
        {
            throw new NotSupportedException(PubSubConstants.ErrorUnsupportedProtocol);
        }

        // 2. 依据协议或连接串中的参数动态初始化解码器
        if (uri.Scheme.Equals(PubSubConstants.SchemeUdp, StringComparison.OrdinalIgnoreCase))
        {
            _decoder = new UadpPubSubDecoder();
        }
        else
        {
            if (uri.Query.Contains("encoding=binary", StringComparison.OrdinalIgnoreCase))
            {
                _decoder = new UadpPubSubDecoder();
            }
            else
            {
                _decoder = new JsonPubSubDecoder();
            }
        }

        // 3. 挂载事件
        _transport.OnMessageReceived += Transport_OnMessageReceived;

        // 4. 连接底层
        await _transport.ConnectAsync(uri, ct);
        return true;
    }

    private void Transport_OnMessageReceived(object? sender, byte[] payload)
    {
        if (_decoder == null) return;

        try
        {
            var dataSets = _decoder.Decode(payload);

            // 将解码出的数据分发给对应的订阅者
            foreach (var kvp in dataSets)
            {
                if (_subscriptions.TryGetValue(kvp.Key, out var callback))
                {
                    // fire-and-forget；异常隔离在 callback 内部
                    _ = callback.Invoke(kvp.Value);
                }
            }
        }
        catch (Exception ex)
        {
            // Log decoding error
            Console.WriteLine($"[OpcUaPubSubDriver] Decode error: {ex.Message}");
        }
    }

    protected override async Task OnDisconnectAsync(CancellationToken ct)
    {
        if (_transport != null)
        {
            _transport.OnMessageReceived -= Transport_OnMessageReceived;
            await _transport.DisconnectAsync(ct);
            await _transport.DisposeAsync();
            _transport = null;
        }
        _subscriptions.Clear();
    }

    public override Task<bool> PingAsync(CancellationToken ct = default)
    {
        // PubSub 通常是单向数据流，如果 transport 没报错，默认视为存活
        return Task.FromResult(_transport != null);
    }

    // --- 不支持 Read / Write，严格限定为 PubSub ---

    protected override Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
    {
        throw new NotSupportedException(PubSubConstants.ErrorReadNotSupported);
    }

    protected override Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
    {
        throw new NotSupportedException(PubSubConstants.ErrorWriteNotSupported);
    }

    public override Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(IEnumerable<UniconRequest> requests, CancellationToken ct = default)
    {
        throw new NotSupportedException(PubSubConstants.ErrorReadNotSupported);
    }

    public override Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default)
    {
        throw new NotSupportedException(PubSubConstants.ErrorWriteNotSupported);
    }

    // --- 核心订阅逻辑 ---

    public override Task<string> SubscribeAsync(
        string address,
        Func<DataValue<object>, Task> callback,
        CancellationToken ct = default)
    {
        _subscriptions[address] = callback;
        return Task.FromResult(address);
    }

    public override Task UnsubscribeAsync(string address, CancellationToken ct = default)
    {
        _subscriptions.TryRemove(address, out _);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _transport?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _subscriptions.Clear();
        base.Dispose();
    }
}
