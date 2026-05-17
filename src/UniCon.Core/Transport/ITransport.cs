using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Transport;

/// <summary>
/// 传输层抽象：封装物理连接的收发细节（TCP/Serial/MQTT/USB 等）。
/// Driver 只负责协议解析与数据映射，Transport 负责字节流的收发与连接维护。
/// 读写锁下沉到 Transport 层，支持并发读（多 Reader），写时独占。
/// </summary>
public interface ITransport : IAsyncDisposable
{
    bool IsOpen { get; }

    Task OpenAsync(string connectionString, CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);

    /// <summary>
    /// 并发安全的批量读取原始字节块。
    /// 实现应内部维护 ReaderWriterLockSlim 或 SemaphoreSlim 以支持并发读。
    /// </summary>
    Task<IReadOnlyList<byte[]>> ReadBytesAsync(
        IReadOnlyList<ReadRequest> requests,
        CancellationToken ct = default);

    /// <summary>写时独占，不允许并发</summary>
    Task WriteBytesAsync(WriteRequest request, CancellationToken ct = default);
}

/// <summary>原始字节读取请求（地址 + 期望字节长度）</summary>
public sealed record ReadRequest(string Address, int ByteCount);

/// <summary>原始字节写入请求</summary>
public sealed record WriteRequest(string Address, byte[] Data);
