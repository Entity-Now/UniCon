using System;
using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// 异常值变更检测策略：仅在值、质量码或状态发生实质性变化时触发通知。
/// 支持 TagMetadata.Deadband 死区过滤，抑制浮点抖动造成的无效推送。
/// </summary>
internal sealed class ExceptionBasedScanStrategy : IScanStrategy
{
    public static readonly IScanStrategy Instance = new ExceptionBasedScanStrategy();

    private ExceptionBasedScanStrategy() { }

    public bool ShouldNotify(DataValue<object> newValue, DataValue<object>? cachedValue, TagMetadata? metadata)
    {
        if (cachedValue is null) return true;

        if (cachedValue.Status != newValue.Status || cachedValue.Quality != newValue.Quality)
            return true;

        if (newValue.Value is null) return cachedValue.Value is not null;

        // 死区检测：对数值类型进行阈值过滤
        if (metadata is { Deadband: > 0 }
            && TryGetDouble(newValue.Value, out var newD)
            && TryGetDouble(cachedValue.Value, out var oldD))
        {
            return Math.Abs(newD - oldD) > metadata.Deadband;
        }

        return !Equals(cachedValue.Value, newValue.Value);
    }

    private static bool TryGetDouble(object? value, out double result)
    {
        result = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            decimal m => (double)m,
            _ => double.NaN
        };

        return !double.IsNaN(result);
    }
}
