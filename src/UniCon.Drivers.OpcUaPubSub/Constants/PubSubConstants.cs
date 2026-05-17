namespace UniCon.Drivers.OpcUaPubSub.Constants;

public static class PubSubConstants
{
    public const string SchemeUdp = "opc.udp";
    public const string SchemeMqtt = "mqtt";

    public const int DefaultUdpPort = 4840;
    public const int DefaultMqttPort = 1883;

    public const string ErrorUnsupportedProtocol = "Unsupported PubSub protocol scheme. Use 'opc.udp://' or 'mqtt://'.";
    public const string ErrorWriteNotSupported = "Write operation is not supported in OPC UA PubSub mode.";
    public const string ErrorReadNotSupported = "Read operation is not supported in OPC UA PubSub mode. Please use SubscribeAsync.";
}
