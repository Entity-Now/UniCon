using System;
using System.Collections.Generic;

namespace UniCon.Core.Models
{
    /// <summary>
    /// 数据质量状态 (RULE 1.1)
    /// </summary>
    public enum DataStatus
    {
        Good = 0,
        Bad = 1,
        Uncertain = 2,
        Timeout = 3
    }

    /// <summary>
    /// 带质量戳和时间戳的数据包装器 (Value Quality)
    /// </summary>
    public class DataValue<T>
    {
        public T? Value { get; set; }
        public DataStatus Status { get; set; } = DataStatus.Good;
        public DateTime SourceTimestamp { get; set; } = DateTime.Now;

        public override string ToString() => $"[{Status}] {Value} (@{SourceTimestamp:HH:mm:ss.fff})";
    }

    /// <summary>
    /// 统一请求结构
    /// </summary>
    public class UniconRequest
    {
        public string Address { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public object? Body { get; set; }
    }

    /// <summary>
    /// 增强型统一响应结构
    /// </summary>
    public class UniconResponse<T>
    {
        public bool Success { get; set; }

        /// <summary>
        /// 包装后的数据内容 (包含质量和时间戳)
        /// </summary>
        public DataValue<T>? Data { get; set; }

        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// 状态码，便于程序化错误处理
        /// </summary>
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
            Data = new DataValue<T> { Status = DataStatus.Bad }
        };
    }
}
