using System;

namespace UniCon.Core
{
    public class Tag
    {
        public string Name { get; set; } = string.Empty;
        public string DriverId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public Type DataType { get; set; } = typeof(object);
        public object? LastValue { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
