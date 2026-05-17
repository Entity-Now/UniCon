using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniCon.Core.Helpers
{
    /// <summary>
    /// 西门子 PLC CPU 类型枚举
    /// </summary>
    public enum S7CpuType
    {
        S7200,
        S7300,
        S7400,
        S71200,
        S71500
    }

    /// <summary>
    /// OPC UA PubSub 传输协议模式枚举
    /// </summary>
    public enum PubSubScheme
    {
        Udp,
        Mqtt
    }

    /// <summary>
    /// 连接字符串流式构建器，避免手动拼接错误
    /// </summary>
    public static class ConnectionStringBuilder
    {
        public static S7Builder S7() => new S7Builder();
        public static ModbusBuilder Modbus() => new ModbusBuilder();
        public static MqttBuilder Mqtt() => new MqttBuilder();
        public static OpcUaBuilder OpcUa() => new OpcUaBuilder();
        public static OpcUaPubSubBuilder OpcUaPubSub() => new OpcUaPubSubBuilder();
    }

    public class S7Builder
    {
        private S7CpuType _cpuType = S7CpuType.S71200;
        private string _ip = "127.0.0.1";
        private short _rack = 0;
        private short _slot = 1;

        public S7Builder WithCpuType(S7CpuType cpuType) { _cpuType = cpuType; return this; }
        public S7Builder WithIp(string ip) { _ip = ip; return this; }
        public S7Builder WithRack(short rack) { _rack = rack; return this; }
        public S7Builder WithSlot(short slot) { _slot = slot; return this; }

        public string Build() => $"CpuType={_cpuType};Ip={_ip};Rack={_rack};Slot={_slot}";
    }

    public class ModbusBuilder
    {
        private string _ip = "127.0.0.1";
        private int _port = 502;

        public ModbusBuilder WithIp(string ip) { _ip = ip; return this; }
        public ModbusBuilder WithPort(int port) { _port = port; return this; }

        public string Build() => $"ip={_ip};port={_port}";
    }

    public class MqttBuilder
    {
        private string _server = "127.0.0.1";
        private string? _clientId;

        public MqttBuilder WithServer(string server) { _server = server; return this; }
        public MqttBuilder WithClientId(string clientId) { _clientId = clientId; return this; }

        public string Build()
        {
            var result = $"server={_server}";
            if (!string.IsNullOrEmpty(_clientId))
            {
                result += $";clientid={_clientId}";
            }
            return result;
        }
    }

    public class OpcUaBuilder
    {
        private string _host = "127.0.0.1";
        private int _port = 4840;
        private string _path = "";

        public OpcUaBuilder WithHost(string host) { _host = host; return this; }
        public OpcUaBuilder WithPort(int port) { _port = port; return this; }
        public OpcUaBuilder WithPath(string path) { _path = path; return this; }

        public string Build()
        {
            var path = string.IsNullOrEmpty(_path) ? "" : (_path.StartsWith("/") ? _path : "/" + _path);
            return $"opc.tcp://{_host}:{_port}{path}";
        }
    }

    public class OpcUaPubSubBuilder
    {
        private PubSubScheme _scheme = PubSubScheme.Udp;
        private string _host = "224.0.2.14";
        private int _port = 4840;
        private string _topic = "";

        public OpcUaPubSubBuilder WithScheme(PubSubScheme scheme) { _scheme = scheme; return this; }
        public OpcUaPubSubBuilder WithHost(string host) { _host = host; return this; }
        public OpcUaPubSubBuilder WithPort(int port) { _port = port; return this; }
        public OpcUaPubSubBuilder WithTopic(string topic) { _topic = topic; return this; }

        public string Build()
        {
            if (_scheme == PubSubScheme.Mqtt)
            {
                var topicPath = string.IsNullOrEmpty(_topic) ? "/opcua/pubsub" : (_topic.StartsWith("/") ? _topic : "/" + _topic);
                return $"mqtt://{_host}:{_port}{topicPath}";
            }
            return $"opc.udp://{_host}:{_port}";
        }
    }

    /// <summary>
    /// 驱动快捷连接扩展方法
    /// </summary>
    public static class DriverConnectionExtensions
    {
        public static Task<bool> ConnectS7Async(this IUniconDriver driver, string ip, S7CpuType cpuType = S7CpuType.S71200, short rack = 0, short slot = 1, CancellationToken ct = default)
        {
            var connStr = ConnectionStringBuilder.S7().WithIp(ip).WithCpuType(cpuType).WithRack(rack).WithSlot(slot).Build();
            return driver.ConnectAsync(connStr, ct);
        }

        public static Task<bool> ConnectModbusAsync(this IUniconDriver driver, string ip, int port = 502, CancellationToken ct = default)
        {
            var connStr = ConnectionStringBuilder.Modbus().WithIp(ip).WithPort(port).Build();
            return driver.ConnectAsync(connStr, ct);
        }

        public static Task<bool> ConnectMqttAsync(this IUniconDriver driver, string server, string? clientId = null, CancellationToken ct = default)
        {
            var builder = ConnectionStringBuilder.Mqtt().WithServer(server);
            if (clientId != null) builder.WithClientId(clientId);
            return driver.ConnectAsync(builder.Build(), ct);
        }

        public static Task<bool> ConnectOpcUaAsync(this IUniconDriver driver, string host, int port = 4840, string path = "", CancellationToken ct = default)
        {
            var connStr = ConnectionStringBuilder.OpcUa().WithHost(host).WithPort(port).WithPath(path).Build();
            return driver.ConnectAsync(connStr, ct);
        }

        public static Task<bool> ConnectOpcUaPubSubUdpAsync(this IUniconDriver driver, string multicastIp = "224.0.2.14", int port = 4840, CancellationToken ct = default)
        {
            var connStr = ConnectionStringBuilder.OpcUaPubSub().WithScheme(PubSubScheme.Udp).WithHost(multicastIp).WithPort(port).Build();
            return driver.ConnectAsync(connStr, ct);
        }

        public static Task<bool> ConnectOpcUaPubSubMqttAsync(this IUniconDriver driver, string brokerIp, int port = 1883, string topic = "opcua/pubsub", CancellationToken ct = default)
        {
            var connStr = ConnectionStringBuilder.OpcUaPubSub().WithScheme(PubSubScheme.Mqtt).WithHost(brokerIp).WithPort(port).WithTopic(topic).Build();
            return driver.ConnectAsync(connStr, ct);
        }
    }
}
