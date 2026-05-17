using System.Threading;

namespace UniCon.Core.Models;

/// <summary>
/// ScanGroup 级别的无锁运行统计，通过 Interlocked 保证线程安全
/// </summary>
public sealed class ScanStatistics
{
    private long _scanCount;
    private long _notifyCount;
    private long _errorCount;
    private long _totalReadDurationMs;

    public long ScanCount => Interlocked.Read(ref _scanCount);
    public long NotifyCount => Interlocked.Read(ref _notifyCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    public double AverageReadDurationMs
        => _scanCount == 0 ? 0d : (double)Interlocked.Read(ref _totalReadDurationMs) / _scanCount;

    public double ErrorRate
        => _scanCount == 0 ? 0d : (double)Interlocked.Read(ref _errorCount) / _scanCount;

    public void RecordScan(long durationMs)
    {
        Interlocked.Increment(ref _scanCount);
        Interlocked.Add(ref _totalReadDurationMs, durationMs);
    }

    public void RecordNotify() => Interlocked.Increment(ref _notifyCount);

    public void RecordError() => Interlocked.Increment(ref _errorCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _scanCount, 0);
        Interlocked.Exchange(ref _notifyCount, 0);
        Interlocked.Exchange(ref _errorCount, 0);
        Interlocked.Exchange(ref _totalReadDurationMs, 0);
    }
}
