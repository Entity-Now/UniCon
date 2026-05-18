using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using S7.Net;
using S7.Net.Types;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Network;

namespace UniCon.Drivers.S7
{
    [UniconDriver("S7")]
    public class S7Driver : DriverBase
    {
        private Plc? _plc;
        private string? _ip;
        private CpuType _cpuType = CpuType.S71200;
        private short _rack = 0;
        private short _slot = 1;
        private int _readTimeout = 0;
        private int _writeTimeout = 0;

        public S7Driver(string driverId, ILogger logger, IUniconCacheProvider cacheProvider, INetworkMonitor networkMonitor)
            : base(driverId, logger, cacheProvider, networkMonitor)
        {
        }

        protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            ParseConnectionString(connectionString);
            _plc = new Plc(_cpuType, _ip, _rack, _slot);

            if (_readTimeout > 0)
            {
                _plc.ReadTimeout = _readTimeout;
            }
            if (_writeTimeout > 0)
            {
                _plc.WriteTimeout = _writeTimeout;
            }

            await _plc.OpenAsync(ct);
            return _plc.IsConnected;
        }

        private void ParseConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLower();
                var val = kv[1].Trim();
                switch (key)
                {
                    case "cputype": _cpuType = Enum.Parse<CpuType>(val, true); break;
                    case "ip": _ip = val; break;
                    case "rack": _rack = short.Parse(val); break;
                    case "slot": _slot = short.Parse(val); break;
                    case "timeout":
                    case "readtimeout": _readTimeout = int.Parse(val); break;
                    case "writetimeout": _writeTimeout = int.Parse(val); break;
                }
            }
        }

        protected override Task OnDisconnectAsync(CancellationToken ct)
        {
            if (_plc != null)
            {
                _plc.Close();
                _plc = null;
            }
            return Task.CompletedTask;
        }

        protected override async Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
        {
            try
            {
                var result = await _plc!.ReadAsync(request.Address);
                return UniconResponse<T>.CreateSuccess((T)result);
            }
            catch (Exception ex)
            {
                if (_plc == null || !_plc.IsConnected)
                {
                    _logger.LogWarning("[Driver:{Id}] S7 PLC client disconnected during read. Transitioning to Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
                return UniconResponse<T>.CreateFailure(ex.Message, 500);
            }
        }

        protected override async Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
        {
            try
            {
                await _plc!.WriteAsync(request.Address, value);
                return UniconResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                if (_plc == null || !_plc.IsConnected)
                {
                    _logger.LogWarning("[Driver:{Id}] S7 PLC client disconnected during write. Transitioning to Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        public override async Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(
            IEnumerable<UniconRequest> requests, CancellationToken ct = default)
        {
            if (State != DriverState.Connected || _plc == null)
            {
                return requests.Select(req => UniconResponse<object>.CreateFailure($"Driver {DriverId} offline", 503));
            }

            var reqList = requests.ToList();
            if (reqList.Count == 0) return Array.Empty<UniconResponse<object>>();

            try
            {
                var dataItems = new List<(UniconRequest Request, DataItem Item)>();
                var parsedOk = true;

                foreach (var req in reqList)
                {
                    try
                    {
                        var item = DataItem.FromAddress(req.Address);
                        dataItems.Add((req, item));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Driver:{Id}] Failed to parse address '{Addr}' for batch read.", DriverId, req.Address);
                        parsedOk = false;
                        break;
                    }
                }

                if (parsedOk)
                {
                    // Siemens S7 standard PDU limit is 240 bytes. 19 is a safe chunk size.
                    const int maxBatchSize = 19;
                    var resultsMap = new Dictionary<UniconRequest, UniconResponse<object>>();

                    for (int i = 0; i < dataItems.Count; i += maxBatchSize)
                    {
                        if (ct.IsCancellationRequested) break;

                        var chunk = dataItems.Skip(i).Take(maxBatchSize).ToList();
                        var plcItems = chunk.Select(x => x.Item).ToList();

                        await _plc.ReadMultipleVarsAsync(plcItems, ct);

                        foreach (var (req, item) in chunk)
                        {
                            resultsMap[req] = UniconResponse<object>.CreateSuccess(item.Value);
                        }
                    }

                    return reqList.Select(req => resultsMap.TryGetValue(req, out var res) ? res : UniconResponse<object>.CreateFailure("Cancelled", 499));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Driver:{Id}] S7 batch read failed. Falling back to individual reads.", DriverId);
                if (_plc == null || !_plc.IsConnected)
                {
                    _logger.LogWarning("[Driver:{Id}] S7 PLC client disconnected during batch read. Transitioning to Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
            }

            // Fallback to individual reads
            var fallbackResults = new List<UniconResponse<object>>();
            foreach (var req in reqList)
            {
                if (ct.IsCancellationRequested)
                {
                    fallbackResults.Add(UniconResponse<object>.CreateFailure("Cancelled", 499));
                    continue;
                }
                fallbackResults.Add(await InternalReadAsync<object>(req, ct));
            }
            return fallbackResults;
        }

        public override async Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(
            IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default)
        {
            if (State != DriverState.Connected || _plc == null)
            {
                return writes.Select(_ => UniconResponse<bool>.CreateFailure($"Driver {DriverId} offline", 503));
            }

            var writeList = writes.ToList();
            if (writeList.Count == 0) return Array.Empty<UniconResponse<bool>>();

            try
            {
                var dataItems = new List<((UniconRequest Request, object Value) WriteInfo, DataItem Item)>();
                var parsedOk = true;

                foreach (var write in writeList)
                {
                    try
                    {
                        var item = DataItem.FromAddress(write.Request.Address);
                        item.Value = write.Value;
                        dataItems.Add((write, item));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Driver:{Id}] Failed to parse address '{Addr}' or value for batch write.", DriverId, write.Request.Address);
                        parsedOk = false;
                        break;
                    }
                }

                if (parsedOk)
                {
                    // Chunk to 10 for safe S7 write PDU limit (write payload is usually larger than read)
                    const int maxBatchSize = 10;
                    var resultsMap = new Dictionary<UniconRequest, UniconResponse<bool>>();

                    for (int i = 0; i < dataItems.Count; i += maxBatchSize)
                    {
                        if (ct.IsCancellationRequested) break;

                        var chunk = dataItems.Skip(i).Take(maxBatchSize).ToList();
                        var plcItems = chunk.Select(x => x.Item).ToArray();

                        await _plc.WriteAsync(plcItems);

                        foreach (var (writeInfo, _) in chunk)
                        {
                            resultsMap[writeInfo.Request] = UniconResponse<bool>.CreateSuccess(true);
                        }
                    }

                    return writeList.Select(w => resultsMap.TryGetValue(w.Request, out var res) ? res : UniconResponse<bool>.CreateFailure("Cancelled", 499));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Driver:{Id}] S7 batch write failed. Falling back to individual writes.", DriverId);
                if (_plc == null || !_plc.IsConnected)
                {
                    _logger.LogWarning("[Driver:{Id}] S7 PLC client disconnected during batch write. Transitioning to Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
            }

            // Fallback to individual writes
            var fallbackResults = new List<UniconResponse<bool>>();
            foreach (var (req, val) in writeList)
            {
                if (ct.IsCancellationRequested)
                {
                    fallbackResults.Add(UniconResponse<bool>.CreateFailure("Cancelled", 499));
                    continue;
                }
                fallbackResults.Add(await InternalWriteAsync(req, val, ct));
            }
            return fallbackResults;
        }

        public override Task<bool> PingAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_plc is { IsConnected: true });
        }

        public override void Dispose()
        {
            _plc?.Close();
            base.Dispose();
        }
    }
}
