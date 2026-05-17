using System.Collections.Concurrent;
using System.Collections.Generic;

namespace UniCon.Core
{
    /// <summary>
    /// 驱动注册中心，供系统各组件调用 (RULE 3.2)
    /// </summary>
    public interface IDriverRegistry
    {
        void Register(IUniconDriver driver);
        bool Unregister(string driverId);
        IUniconDriver? Get(string driverId);
        IEnumerable<IUniconDriver> GetAll();
    }

    public class DriverRegistry : IDriverRegistry
    {
        private readonly ConcurrentDictionary<string, IUniconDriver> _drivers = new();

        public void Register(IUniconDriver driver)
        {
            _drivers[driver.DriverId] = driver;
        }

        public bool Unregister(string driverId)
        {
            return _drivers.TryRemove(driverId, out _);
        }

        public IUniconDriver? Get(string driverId)
        {
            return _drivers.TryGetValue(driverId, out var driver) ? driver : null;
        }

        public IEnumerable<IUniconDriver> GetAll()
        {
            return _drivers.Values;
        }
    }
}
