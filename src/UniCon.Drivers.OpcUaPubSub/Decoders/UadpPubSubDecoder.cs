using System;
using System.Collections.Generic;
using UniCon.Core.Models;
using UniCon.OpcUaPubSub.binary;
using UniCon.OpcUaPubSub.binary.DataPoints;
using UniCon.OpcUaPubSub.binary.Decode;
using UniCon.OpcUaPubSub.binary.Messages;
using UniCon.OpcUaPubSub.binary.Messages.Key;
using UniCon.OpcUaPubSub.binary.Messages.Delta;

namespace UniCon.Drivers.OpcUaPubSub.Decoders
{
    /// <summary>
    /// 基于西门子库的 OPC UA UADP 二进制解码器
    /// </summary>
    public class UadpPubSubDecoder : IPubSubDecoder
    {
        private readonly DecodeMessage _decodeMessage;

        public UadpPubSubDecoder()
        {
            // 初始化西门子的二进制解码服务，使用默认编码选项
            _decodeMessage = new DecodeMessage(new EncodingOptions());
        }

        public IDictionary<string, DataValue<object>> Decode(byte[] payload)
        {
            var result = new Dictionary<string, DataValue<object>>(StringComparer.OrdinalIgnoreCase);
            if (payload == null || payload.Length == 0) return result;

            try
            {
                // 使用西门子包解析二进制 NetworkMessage
                var networkMessage = _decodeMessage.ParseBinaryMessage(payload);
                if (networkMessage == null) return result;

                // 判断是否为带数据的 DataFrame (KeyFrame 或 DeltaFrame)
                if (networkMessage is DataFrame dataFrame && dataFrame.Items != null)
                {
                    foreach (var item in dataFrame.Items)
                    {
                        if (string.IsNullOrEmpty(item.Name)) continue;

                        var dataValue = new DataValue<object>();

                        if (item is ProcessDataPointValue pdv)
                        {
                            dataValue.Value = pdv.Value;

                            // 0x00 代表质量完好，或者是依据高位标志判断。这里做简易工业级映射：
                            // 质量字段不为 0xFFFF 且不是 Bad 的定义时映射为 Good。
                            dataValue.Status = (pdv.Quality == 0 || pdv.Quality == 0xC0)
                                ? DataStatus.Good
                                : DataStatus.Bad;

                            // 提取源时间戳
                            try
                            {
                                dataValue.SourceTimestamp = pdv.GetDateTime();
                            }
                            catch
                            {
                                dataValue.SourceTimestamp = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            // 可能是 File 等其他数据点
                            dataValue.Value = item.ToString();
                            dataValue.Status = DataStatus.Good;
                            dataValue.SourceTimestamp = DateTime.UtcNow;
                        }

                        // 将地址作为 Key 写入结果中
                        result[item.Name] = dataValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UadpPubSubDecoder] Binary Parse Error: {ex.Message}");
            }

            return result;
        }
    }
}
