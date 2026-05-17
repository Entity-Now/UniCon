using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Attributes;
using UniCon.Core.Models;

namespace UniCon.Core.Odm
{
    /// <summary>
    /// 物联网对象-设备映射引擎 (Object-Device Mapper - ODM Engine)
    /// 提供结构化实体与通讯点位之间的反射式解析与统一读写。 (RULE 2.1)
    /// </summary>
    public class OdmEngine
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IDriverRegistry _driverRegistry;

        /// <summary>
        /// 动态驱动实例化委托：由于 Core 模块不直接依赖具体驱动程序集，
        /// 故将具体的物理驱动实例化职责通过委托暴露给外层主应用配置 (WebServer)
        /// </summary>
        public static Func<string, string, IUniconDriver>? DriverFactory { get; set; }

        public OdmEngine(IConnectionManager connectionManager, IDriverRegistry driverRegistry)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _driverRegistry = driverRegistry ?? throw new ArgumentNullException(nameof(driverRegistry));
        }

        /// <summary>
        /// 场景 1：反射扫描实体类结构，自动提取自定义特性并构建出通讯点位列表
        /// </summary>
        public IEnumerable<TagMappingInfo> GenerateTags<T>()
        {
            return GenerateTags(typeof(T));
        }

        /// <summary>
        /// 反射扫描实体类型结构并提取通讯点位
        /// </summary>
        public IEnumerable<TagMappingInfo> GenerateTags(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var addrAttr = prop.GetCustomAttribute<UniconAddressAttribute>();
                if (addrAttr != null)
                {
                    yield return new TagMappingInfo(
                        prop.Name,
                        addrAttr.Address,
                        addrAttr.Type.ToString(),
                        prop.PropertyType,
                        addrAttr.IsWritable
                    );
                }
            }
        }

        /// <summary>
        /// 场景 2：反射解析实体类所标记的连接配置，自动拉起/注入驱动，并批量读取物理点位值填充回实体对象
        /// </summary>
        public async Task<T> ReadEntityAsync<T>(CancellationToken ct = default) where T : class, new()
        {
            var type = typeof(T);
            var deviceAttr = type.GetCustomAttribute<UniconDeviceAttribute>();
            if (deviceAttr == null)
            {
                throw new InvalidOperationException($"Type '{type.Name}' is not marked with [UniconDeviceAttribute].");
            }

            string driverId = deviceAttr.DriverId ?? type.Name;

            // 1. 自适应拉起驱动连接
            var driver = _driverRegistry.Get(driverId);
            if (driver == null)
            {
                if (DriverFactory == null)
                {
                    throw new InvalidOperationException("ODM dynamic factory failure: OdmEngine.DriverFactory delegate is not configured.");
                }

                driver = DriverFactory(driverId, deviceAttr.DriverType.ToString());
                _driverRegistry.Register(driver);
                await _connectionManager.RegisterDriverAsync(driver, deviceAttr.ConnectionString, ct);
            }

            // 2. 反射式读取属性
            var entity = new T();
            var mappings = GenerateTags<T>().ToList();

            foreach (var mapping in mappings)
            {
                var response = await ReadAddressReflectiveAsync(driver, mapping.Address, mapping.TypeHint, ct);
                if (response.Success && response.Data != null)
                {
                    var prop = type.GetProperty(mapping.PropertyName)!;
                    try
                    {
                        var converted = ConvertValue(response.Data.Value, prop.PropertyType);
                        prop.SetValue(entity, converted);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidCastException($"ODM Cast Error: Cannot map raw read value from address '{mapping.Address}' to property '{prop.Name}' ({prop.PropertyType.Name}). Detail: {ex.Message}", ex);
                    }
                }
            }

            return entity;
        }

        /// <summary>
        /// 场景 2：反射提取实体类的属性值，自动转换为强类型并批量下发写入到物理设备
        /// </summary>
        public async Task<UniconResponse<bool>> WriteEntityAsync<T>(T entity, CancellationToken ct = default) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var type = typeof(T);
            var deviceAttr = type.GetCustomAttribute<UniconDeviceAttribute>();
            if (deviceAttr == null)
            {
                return UniconResponse<bool>.CreateFailure($"Type '{type.Name}' is not marked with [UniconDeviceAttribute].", 400);
            }

            string driverId = deviceAttr.DriverId ?? type.Name;
            var driver = _driverRegistry.Get(driverId);
            if (driver == null)
            {
                return UniconResponse<bool>.CreateFailure($"Driver with ID '{driverId}' is not active or registered in connection manager.", 404);
            }

            var mappings = GenerateTags<T>().Where(m => m.IsWritable).ToList();
            var errors = new List<string>();

            // 批量反射写入
            foreach (var mapping in mappings)
            {
                var prop = type.GetProperty(mapping.PropertyName)!;
                var val = prop.GetValue(entity);
                if (val == null) continue;

                var response = await WriteAddressReflectiveAsync(driver, mapping.Address, val, mapping.TypeHint, ct);
                if (!response.Success)
                {
                    errors.Add($"[{mapping.PropertyName}] Address: {mapping.Address} Write Failed: {response.ErrorMessage}");
                }
            }

            if (errors.Any())
            {
                return UniconResponse<bool>.CreateFailure(string.Join(" | ", errors), 500);
            }

            return UniconResponse<bool>.CreateSuccess(true);
        }

        #region 底层反射式通用网络交互器

        private async Task<UniconResponse<object>> ReadAddressReflectiveAsync(IUniconDriver driver, string address, string typeHint, CancellationToken ct)
        {
            var targetType = MapTypeHint(typeHint);
            try
            {
                // 反射调用 IUniconDriver.ReadAsync<T>()
                var readMethod = typeof(IUniconDriver)
                    .GetMethod(nameof(IUniconDriver.ReadAsync))?
                    .MakeGenericMethod(targetType);

                if (readMethod == null)
                {
                    return UniconResponse<object>.CreateFailure("ODM system internal error resolving dynamic ReadAsync.", 500);
                }

                var request = new UniconRequest { Address = address };
                var task = (Task)readMethod.Invoke(driver, new object[] { request, ct })!;
                await task;

                var resultProperty = task.GetType().GetProperty("Result")!;
                var responseObj = resultProperty.GetValue(task);

                if (responseObj == null)
                {
                    return UniconResponse<object>.CreateFailure("南向驱动未能返回响应报文", 500);
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
                var inner = ex.InnerException ?? ex;
                return UniconResponse<object>.CreateFailure($"ODM反射读取异常: {inner.Message}", 500);
            }
        }

        private async Task<UniconResponse<bool>> WriteAddressReflectiveAsync(IUniconDriver driver, string address, object value, string typeHint, CancellationToken ct)
        {
            var targetType = MapTypeHint(typeHint);
            try
            {
                // 反射调用 IUniconDriver.WriteAsync<T>()
                var writeMethod = typeof(IUniconDriver)
                    .GetMethod(nameof(IUniconDriver.WriteAsync))?
                    .MakeGenericMethod(targetType);

                if (writeMethod == null)
                {
                    return UniconResponse<bool>.CreateFailure("ODM system internal error resolving dynamic WriteAsync.", 500);
                }

                var request = new UniconRequest { Address = address };
                var task = (Task)writeMethod.Invoke(driver, new object[] { request, value, ct })!;
                await task;

                var resultProperty = task.GetType().GetProperty("Result")!;
                return (UniconResponse<bool>)resultProperty.GetValue(task)!;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return UniconResponse<bool>.CreateFailure($"ODM反射写入异常: {inner.Message}", 500);
            }
        }

        private static object? ConvertValue(object? val, Type targetType)
        {
            if (val == null) return null;
            if (targetType.IsAssignableFrom(val.GetType())) return val;
            return Convert.ChangeType(val, targetType);
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
    }
}
