using System;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace UniCon.Core.Network
{
    /// <summary>
    /// 全局系统级网络可用性监测实现（基于 NetworkChange 事件）
    /// </summary>
    public sealed class NetworkMonitor : INetworkMonitor, IDisposable
    {
        private readonly ILogger<NetworkMonitor> _logger;
        private bool _isDisposed;
        private bool _isNetworkAvailable;

        public bool IsNetworkAvailable
        {
            get => _isNetworkAvailable;
            private set => _isNetworkAvailable = value;
        }

        public event EventHandler<bool>? NetworkAvailabilityChanged;

        public NetworkMonitor(ILogger<NetworkMonitor> logger)
        {
            _logger = logger;
            _isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            _logger.LogInformation("[NetworkMonitor] Initialized. Initial availability: {Available}", _isNetworkAvailable);
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            UpdateAvailability(e.IsAvailable, "AvailabilityChanged");
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            // 在网络切换（如切换 WIFI 或重新插拔网线）时，IP 地址会改变，此处主动进行状态更新
            var current = NetworkInterface.GetIsNetworkAvailable();
            UpdateAvailability(current, "AddressChanged");
        }

        private void UpdateAvailability(bool current, string trigger)
        {
            var old = _isNetworkAvailable;
            if (old != current)
            {
                _logger.LogWarning("[NetworkMonitor] Availability changed via {Trigger}: {Old} -> {New}", trigger, old, current);
                _isNetworkAvailable = current;
                NetworkAvailabilityChanged?.Invoke(this, current);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            _logger.LogDebug("[NetworkMonitor] Disposed.");
        }
    }
}
