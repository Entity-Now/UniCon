using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace UniCon.Core.Helpers
{
    public static class NetworkHelper
    {
        public static bool PingIsOk(string ipOrHost)
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send(ipOrHost, 2000);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public static bool TcpIsOk(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                if (!success) return false;
                client.EndConnect(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static (string Host, int Port) EndpointUrlSplit(string url)
        {
            try
            {
                var uri = new Uri(url);
                return (uri.Host, uri.Port);
            }
            catch
            {
                return (url, 0);
            }
        }
    }
}
