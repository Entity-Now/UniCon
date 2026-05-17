using System;
using System.Threading;
using System.Threading.Tasks;
using EasyModbus;
using Microsoft.Extensions.Logging;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;

namespace UniCon.Drivers.Modbus
{
    public class ModbusDriver : DriverBase
    {
        private ModbusClient? _client;
        private string? _ip;
        private int _port;

        private byte _unitId = 1;
        private int _timeout = 2000;

        public ModbusDriver(string driverId, ILogger logger, IUniconCacheProvider cacheProvider)
            : base(driverId, logger, cacheProvider)
        {
        }

        protected override Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLower();
                var val = kv[1].Trim();
                if (key == "ip") _ip = val;
                else if (key == "port") _port = int.Parse(val);
                else if (key == "unitid" || key == "slaveid") _unitId = byte.Parse(val);
                else if (key == "timeout" || key == "connectiontimeout") _timeout = int.Parse(val);
            }

            _client = new ModbusClient(_ip, _port)
            {
                UnitIdentifier = _unitId,
                ConnectionTimeout = _timeout
            };
            _client.Connect();
            return Task.FromResult(_client.Connected);
        }

        protected override Task OnDisconnectAsync(CancellationToken ct)
        {
            _client?.Disconnect();
            _client = null;
            return Task.CompletedTask;
        }

        protected override async Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
        {
            try
            {
                var parts = request.Address.Split(':');
                var type = parts[0].ToLower();
                int addr = int.Parse(parts[1]);

                object result = type switch
                {
                    "holding" => _client!.ReadHoldingRegisters(addr, 1)[0],
                    "input" => _client!.ReadInputRegisters(addr, 1)[0],
                    "coil" => _client!.ReadCoils(addr, 1)[0],
                    "discrete" => _client!.ReadDiscreteInputs(addr, 1)[0],
                    _ => throw new NotSupportedException($"Type {type} not supported")
                };

                return UniconResponse<T>.CreateSuccess((T)Convert.ChangeType(result, typeof(T)));
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
                var parts = request.Address.Split(':');
                var type = parts[0].ToLower();
                int addr = int.Parse(parts[1]);

                if (type == "holding")
                    _client!.WriteSingleRegister(addr, Convert.ToInt32(value));
                else if (type == "coil")
                    _client!.WriteSingleCoil(addr, Convert.ToBoolean(value));
                else
                    throw new NotSupportedException($"Writing to {type} not supported");

                return UniconResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        public override void Dispose()
        {
            _client?.Disconnect();
            base.Dispose();
        }
    }
}
