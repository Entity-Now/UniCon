using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Drivers.S7;
using UniCon.Drivers.Modbus;
using UniCon.Drivers.OpcUa;
using UniCon.Drivers.Mqtt;
using UniCon.Drivers.OpcUaPubSub;

namespace UniCon.WebServer.Services
{
    /// <summary>
    /// 驱动管理与通信服务：提供动态驱动管理与通用读写/订阅缓存逻辑 (RULE 2.1)
    /// </summary>
    public class CommunicationService : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConnectionManager _connectionManager;
        private readonly IDriverRegistry _driverRegistry;
        private readonly IUniconCacheProvider _cacheProvider;

        // 活动订阅点位注册表 (RULE 2.2)
        private readonly ConcurrentDictionary<(string DriverId, string Address), bool> _activeSubscriptions = new();

        public CommunicationService(ILoggerFactory loggerFactory, IDriverRegistry driverRegistry, IConnectionManager connectionManager, IUniconCacheProvider cacheProvider)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _driverRegistry = driverRegistry ?? throw new ArgumentNullException(nameof(driverRegistry));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

            // ODM 动态驱动加载工厂委托
            UniCon.Core.Odm.OdmEngine.DriverFactory = CreateDriverInstance;
        }

        #region 动态驱动管理接口

        /// <summary>
        /// 动态实例化、注册并激活驱动
        /// </summary>
        public async Task<DriverStatus> CreateDriverAsync(string driverId, string driverType, string connectionString, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID cannot be empty.", nameof(driverId));
            if (string.IsNullOrWhiteSpace(driverType)) throw new ArgumentException("Driver Type cannot be empty.", nameof(driverType));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

            if (_driverRegistry.Get(driverId) != null)
            {
                throw new InvalidOperationException($"Driver with ID '{driverId}' is already registered.");
            }

            var driver = CreateDriverInstance(driverId, driverType);
            _driverRegistry.Register(driver);
            await _connectionManager.RegisterDriverAsync(driver, connectionString, ct);

            return GetDriverStatus(driver);
        }

        /// <summary>
        /// 动态断开、注销并销毁驱动实例
        /// </summary>
        public async Task<bool> RemoveDriverAsync(string driverId, CancellationToken ct = default)
        {
            var driver = _driverRegistry.Get(driverId);
            if (driver == null) return false;

            // 1. 彻底断开物理连接并清理 Watchdog/Dispose 资源
            await _connectionManager.UnregisterDriverAsync(driverId, ct);

            // 2. 从全局注册中心移除
            _driverRegistry.Unregister(driverId);

            // 3. 清理该驱动相关的所有活动订阅缓存
            var matchingKeys = _activeSubscriptions.Keys.Where(k => k.DriverId.Equals(driverId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in matchingKeys)
            {
                _activeSubscriptions.TryRemove(key, out _);
                await _cacheProvider.RemoveAsync(key.DriverId, key.Address, ct);
            }

            return true;
        }

        /// <summary>
        /// 获取当前全部已注册驱动的状态列表
        /// </summary>
        public IEnumerable<DriverStatus> GetDrivers()
        {
            return _driverRegistry.GetAll().Select(GetDriverStatus);
        }

        /// <summary>
        /// 获取指定驱动的实时状态
        /// </summary>
        public DriverStatus? GetDriver(string driverId)
        {
            var driver = _driverRegistry.Get(driverId);
            return driver != null ? GetDriverStatus(driver) : null;
        }

        #endregion

        #region 通用读写接口 (利用反射实现强类型调用)

        /// <summary>
        /// 通用动态强类型读取
        /// </summary>
        public async Task<UniconResponse<object>> ReadAddressAsync(string driverId, string address, string typeHint, CancellationToken ct = default)
        {
            var driver = _driverRegistry.Get(driverId);
            if (driver == null)
            {
                return UniconResponse<object>.CreateFailure($"Driver with ID '{driverId}' was not found.", 404);
            }

            var targetType = MapTypeHint(typeHint);
            try
            {
                // 反射调用 IUniconDriver.ReadAsync<T>()
                var readMethod = typeof(IUniconDriver)
                    .GetMethod(nameof(IUniconDriver.ReadAsync))?
                    .MakeGenericMethod(targetType);

                if (readMethod == null)
                {
                    return UniconResponse<object>.CreateFailure("Internal error resolving communication interface method.", 500);
                }

                var request = new UniconRequest { Address = address };
                var task = (Task)readMethod.Invoke(driver, new object[] { request, ct })!;
                await task;

                // 提取 Task<UniconResponse<T>> 的 Result 属性
                var resultProperty = task.GetType().GetProperty("Result")!;
                var responseObj = resultProperty.GetValue(task);

                if (responseObj == null)
                {
                    return UniconResponse<object>.CreateFailure("Driver returned a null response object.", 500);
                }

                var success = (bool)responseObj.GetType().GetProperty("Success")!.GetValue(responseObj)!;
                if (!success)
                {
                    var errorMsg = (string?)responseObj.GetType().GetProperty("ErrorMessage")!.GetValue(responseObj) ?? "Unknown Error";
                    var errorCode = (int)responseObj.GetType().GetProperty("StatusCode")!.GetValue(responseObj)!;
                    return UniconResponse<object>.CreateFailure(errorMsg, errorCode);
                }

                var dataValueObj = responseObj.GetType().GetProperty("Data")!.GetValue(responseObj)!;
                var val = dataValueObj.GetType().GetProperty("Value")!.GetValue(dataValueObj);
                var status = (DataStatus)dataValueObj.GetType().GetProperty("Status")!.GetValue(dataValueObj)!;
                var timestamp = (DateTime)dataValueObj.GetType().GetProperty("SourceTimestamp")!.GetValue(dataValueObj)!;

                return UniconResponse<object>.CreateSuccess(new DataValue<object>
                {
                    Value = val,
                    Status = status,
                    SourceTimestamp = timestamp
                });
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException ?? ex;
                return UniconResponse<object>.CreateFailure($"Communication error: {innerEx.Message}", 500);
            }
        }

        /// <summary>
        /// 通用动态强类型写入
        /// </summary>
        public async Task<UniconResponse<bool>> WriteAddressAsync(string driverId, string address, object rawValue, string typeHint, CancellationToken ct = default)
        {
            var driver = _driverRegistry.Get(driverId);
            if (driver == null)
            {
                return UniconResponse<bool>.CreateFailure($"Driver with ID '{driverId}' was not found.", 404);
            }

            var targetType = MapTypeHint(typeHint);
            object convertedValue;

            try
            {
                // 解析并转换 JSON 或原始数据到强类型
                if (rawValue is System.Text.Json.JsonElement jsonElem)
                {
                    convertedValue = jsonElem.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.Number => targetType == typeof(float) || targetType == typeof(double)
                            ? Convert.ChangeType(jsonElem.GetDouble(), targetType)
                            : Convert.ChangeType(jsonElem.GetInt64(), targetType),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.String => targetType == typeof(string)
                            ? jsonElem.GetString() ?? string.Empty
                            : Convert.ChangeType(jsonElem.GetString() ?? string.Empty, targetType),
                        _ => Convert.ChangeType(jsonElem.ToString(), targetType)
                    };
                }
                else
                {
                    convertedValue = Convert.ChangeType(rawValue, targetType);
                }
            }
            catch (Exception ex)
            {
                return UniconResponse<bool>.CreateFailure($"Data serialization conversion error: Cannot convert parameter value to '{targetType.Name}'. Detail: {ex.Message}", 400);
            }

            try
            {
                // 反射调用 IUniconDriver.WriteAsync<T>()
                var writeMethod = typeof(IUniconDriver)
                    .GetMethod(nameof(IUniconDriver.WriteAsync))?
                    .MakeGenericMethod(targetType);

                if (writeMethod == null)
                {
                    return UniconResponse<bool>.CreateFailure("Internal error resolving communication interface write method.", 500);
                }

                var request = new UniconRequest { Address = address };
                var task = (Task)writeMethod.Invoke(driver, new object[] { request, convertedValue, ct })!;
                await task;

                var resultProperty = task.GetType().GetProperty("Result")!;
                return (UniconResponse<bool>)resultProperty.GetValue(task)!;
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException ?? ex;
                return UniconResponse<bool>.CreateFailure($"Communication write failure: {innerEx.Message}", 500);
            }
        }

        #endregion

        #region 通用订阅/退订管理

        /// <summary>
        /// 注册某个地址的订阅
        /// </summary>
        public async Task<bool> SubscribeAddressAsync(string driverId, string address, CancellationToken ct = default)
        {
            var driver = _driverRegistry.Get(driverId);
            if (driver == null) return false;

            var key = (DriverId: driverId, Address: address);

            // 避免重复订阅同一个地址
            if (_activeSubscriptions.ContainsKey(key)) return true;

            // 初始化默认占位状态写入统一缓存介质
            await _cacheProvider.SetAsync(driverId, address, new DataValue<object>
            {
                Value = null,
                Status = DataStatus.Bad,
                SourceTimestamp = DateTime.Now
            }, ct);

            _activeSubscriptions[key] = true;

            await driver.SubscribeAsync(address, async dataValue =>
            {
                // 回调触发时将数据写入统一缓存介质
                // ExceptionBased 模式下只有数值或状态变化时才触发此回调
                await _cacheProvider.SetAsync(driverId, address, dataValue, CancellationToken.None);
            }, ct);

            return true;
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public async Task<bool> UnsubscribeAddressAsync(string driverId, string address, CancellationToken ct = default)
        {
            var driver = _driverRegistry.Get(driverId);
            if (driver == null) return false;

            var key = (DriverId: driverId, Address: address);
            if (_activeSubscriptions.TryRemove(key, out _))
            {
                await driver.UnsubscribeAsync(address, ct);
                await _cacheProvider.RemoveAsync(driverId, address, ct);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取当前所有活动订阅的值缓存 (异步方式，解耦介质)
        /// </summary>
        public async Task<IEnumerable<SubscriptionStatus>> GetSubscribedValuesAsync(string? driverId = null, CancellationToken ct = default)
        {
            var list = new List<SubscriptionStatus>();
            var keys = _activeSubscriptions.Keys.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(driverId))
            {
                keys = keys.Where(k => k.DriverId.Equals(driverId, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var key in keys)
            {
                var val = await _cacheProvider.GetAsync(key.DriverId, key.Address, ct);
                if (val != null)
                {
                    list.Add(new SubscriptionStatus(
                        key.DriverId,
                        key.Address,
                        val.Value,
                        val.Status.ToString(),
                        val.SourceTimestamp
                    ));
                }
            }

            return list;
        }

        /// <summary>
        /// 获取特定订阅项的实时缓存 (异步方式)
        /// </summary>
        public async Task<SubscriptionStatus?> GetSubscribedValueAsync(string driverId, string address, CancellationToken ct = default)
        {
            var val = await _cacheProvider.GetAsync(driverId, address, ct);
            return val != null
                ? new SubscriptionStatus(driverId, address, val.Value, val.Status.ToString(), val.SourceTimestamp)
                : null;
        }

        #endregion

        #region 内部辅助方法

        private IUniconDriver CreateDriverInstance(string driverId, string driverType)
        {
            var typeNormalized = driverType.ToUpperInvariant();
            var logger = _loggerFactory.CreateLogger(driverType);

            return typeNormalized switch
            {
                "S7"          => new S7Driver(driverId, logger, _cacheProvider),
                "MODBUS"      => new ModbusDriver(driverId, logger, _cacheProvider),
                "OPCUA"       => new OpcUaDriver(driverId, logger, _cacheProvider),
                "MQTT"        => new MqttDriver(driverId, logger, _cacheProvider),
                "OPCUAPUBSUB" => new OpcUaPubSubDriver(driverId, logger, _cacheProvider),
                _ => throw new NotSupportedException($"Driver type '{driverType}' is currently not supported in the system.")
            };
        }

        private static DriverStatus GetDriverStatus(IUniconDriver driver)
        {
            var type = driver switch
            {
                S7Driver => "S7",
                ModbusDriver => "Modbus",
                OpcUaDriver => "OpcUa",
                MqttDriver => "Mqtt",
                OpcUaPubSubDriver => "OpcUaPubSub",
                _ => driver.GetType().Name
            };

            return new DriverStatus(
                driver.DriverId,
                type,
                driver.ConnectionString ?? string.Empty,
                driver.State.ToString(),
                driver.IsConnected
            );
        }

        private static Type MapTypeHint(string? typeHint)
        {
            return typeHint?.ToLowerInvariant() switch
            {
                "int" or "int32" or "dint" => typeof(int),
                "float" or "real" or "single" => typeof(float),
                "double" => typeof(double),
                "bool" or "boolean" => typeof(bool),
                "string" => typeof(string),
                "short" or "int16" => typeof(short),
                "byte" => typeof(byte),
                _ => typeof(object)
            };
        }

        #endregion

        public void Dispose()
        {
            _connectionManager.Dispose();
            _activeSubscriptions.Clear();
        }
    }

    #region 模型与数据契约 DTO (RULE 4.0)

    public record DriverStatus(
        string DriverId,
        string DriverType,
        string ConnectionString,
        string State,
        bool IsConnected
    );

    public record CreateDriverRequest(
        string DriverId,
        string DriverType,
        string ConnectionString
    );

    public record WriteAddressRequest(
        string Address,
        object Value,
        string TypeHint = "object"
    );

    public record SubscribeAddressRequest(
        string Address
    );

    public record SubscriptionStatus(
        string DriverId,
        string Address,
        object? Value,
        string Quality,
        DateTime Timestamp
    );

    #endregion
}
