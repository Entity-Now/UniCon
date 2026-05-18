using System;
using System.Collections.Generic;

namespace UniCon.Core.Models;

/// <summary>数据质量状态 (OPC UA DataStatus 简化映射)</summary>
public enum DataStatus
{
    Good = 0,
    Bad = 1,
    Uncertain = 2,
    Timeout = 3
}

/// <summary>
/// 带质量码、双时间戳的数据值包装器。
/// SourceTimestamp：数据在源设备产生的时间（由 Driver 填充）。
/// ServerTimestamp ：数据到达网关服务器的时间（由 ScanScheduler 填充）。
/// </summary>
public class DataValue<T>
{
    public T? Value { get; set; }
    public DataStatus Status { get; set; } = DataStatus.Good;
    public QualityCode Quality { get; set; } = QualityCode.Good;
    public DateTime SourceTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime ServerTimestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
        => $"[{Status}|0x{(ushort)Quality:X4}] {Value} (src:{SourceTimestamp:HH:mm:ss.fff})";
}

/// <summary>统一请求结构</summary>
public class UniconRequest
{
    public string Address { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public object? Body { get; set; }
}

/// <summary>增强型统一响应结构</summary>
public class UniconResponse<T>
{
    public bool Success { get; set; }

    /// <summary>包含质量码与时间戳的数据载体</summary>
    public DataValue<T>? Data { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();
    public int StatusCode { get; set; } = 0;
    public string? ErrorMessage { get; set; }

    public static UniconResponse<T> CreateSuccess(T value, DataStatus status = DataStatus.Good) => new()
    {
        Success = true,
        Data = new DataValue<T> { Value = value, Status = status }
    };

    public static UniconResponse<T> CreateFailure(string error, int code = -1) => new()
    {
        Success = false,
        ErrorMessage = error,
        StatusCode = code,
        Data = new DataValue<T> { Status = DataStatus.Bad, Quality = QualityCode.BadNoCommunication }
    };
}
