using System;
using System.Collections.Generic;
using System.Text.Json;
using UniCon.Core.Models;

namespace UniCon.Drivers.OpcUaPubSub.Decoders;

/// <summary>
/// OPC UA PubSub 报文解码器接口
/// </summary>
public interface IPubSubDecoder
{
    /// <summary>
    /// 将原始字节流解码为 UniCon 标准的数据字典 (地址 -> 数据值)
    /// </summary>
    IDictionary<string, DataValue<object>> Decode(byte[] payload);
}

/// <summary>
/// 标准 OPC UA Part 14 JSON 解码器实现
/// 兼容标准 NetworkMessage 格式以及扁平化 Payload 格式
/// </summary>
public class JsonPubSubDecoder : IPubSubDecoder
{
    public IDictionary<string, DataValue<object>> Decode(byte[] payload)
    {
        var result = new Dictionary<string, DataValue<object>>(StringComparer.OrdinalIgnoreCase);

        if (payload == null || payload.Length == 0) return result;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // 1. 尝试解析标准的 OPC UA NetworkMessage 格式 (包含 Messages 数组)
            if (root.TryGetProperty("Messages", out var messagesProperty) && messagesProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var messageElement in messagesProperty.EnumerateArray())
                {
                    if (messageElement.TryGetProperty("Payload", out var payloadProperty))
                    {
                        ParsePayload(payloadProperty, result);
                    }
                }
            }
            // 2. 尝试解析单体 DataSetMessage 格式 (直接包含 Payload)
            else if (root.TryGetProperty("Payload", out var singlePayloadProperty))
            {
                ParsePayload(singlePayloadProperty, result);
            }
            // 3. 退化模式：假设整个 JSON 就是一个平铺的 Key-Value 字典
            else if (root.ValueKind == JsonValueKind.Object)
            {
                ParsePayload(root, result);
            }
        }
        catch (JsonException ex)
        {
            // Log 可以在后续接入传入的 logger
            Console.WriteLine($"[JsonPubSubDecoder] JSON Parse Error: {ex.Message}");
        }

        return result;
    }

    private void ParsePayload(JsonElement payloadElement, Dictionary<string, DataValue<object>> result)
    {
        if (payloadElement.ValueKind != JsonValueKind.Object) return;

        foreach (var property in payloadElement.EnumerateObject())
        {
            var key = property.Name;
            var element = property.Value;
            var dataValue = new DataValue<object>();

            // OPC UA PubSub JSON DataValue 格式，通常包含 Value, StatusCode, SourceTimestamp
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("Value", out var valProp))
            {
                dataValue.Value = ExtractValue(valProp);

                if (element.TryGetProperty("StatusCode", out var statusProp))
                {
                    // 简化处理: OPC UA StatusCode 0 代表 Good
                    var code = statusProp.ValueKind == JsonValueKind.Object && statusProp.TryGetProperty("Code", out var cProp)
                        ? cProp.GetInt32()
                        : (statusProp.ValueKind == JsonValueKind.Number ? statusProp.GetInt32() : 0);

                    dataValue.Status = code == 0 ? DataStatus.Good : DataStatus.Bad;
                }

                if (element.TryGetProperty("SourceTimestamp", out var tsProp) && tsProp.TryGetDateTime(out var dt))
                {
                    dataValue.SourceTimestamp = dt;
                }
            }
            else
            {
                // 原始值模式 (Non-reversible JSON 或者扁平 JSON)
                dataValue.Value = ExtractValue(element);
                dataValue.Status = DataStatus.Good;
                dataValue.SourceTimestamp = DateTime.UtcNow;
            }

            result[key] = dataValue;
        }
    }

    private object? ExtractValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText() // 复杂对象回退为字符串
        };
    }
}
