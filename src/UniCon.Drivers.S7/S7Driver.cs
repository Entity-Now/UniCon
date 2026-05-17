using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using S7.Net;
using UniCon.Core;
using UniCon.Core.Models;

namespace UniCon.Drivers.S7
{
    public class S7Driver : DriverBase
    {
        private Plc? _plc;
        private string? _ip;
        private CpuType _cpuType = CpuType.S71200;
        private short _rack = 0;
        private short _slot = 1;
        private int _readTimeout = 0;
        private int _writeTimeout = 0;

        public S7Driver(string driverId, ILogger logger) : base(driverId, logger)
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
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        public override void Dispose()
        {
            _plc?.Close();
            _syncLock.Dispose();
            _connectionLock.Dispose();
        }
    }
}
