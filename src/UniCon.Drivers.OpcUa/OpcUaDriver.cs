using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core;
using UniCon.Core.Helpers;
using UniCon.Core.Models;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;

namespace UniCon.Drivers.OpcUa
{
    public class OpcUaDriver : DriverBase
    {
        private ClientSessionChannel? _channel;

        public OpcUaDriver(string driverId, ILogger logger) : base(driverId, logger)
        {
        }

        private class OpcUaConnectionOptions
        {
            public string EndpointURL { get; set; } = string.Empty;
            public bool AnonymousIdentity { get; set; } = true;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool IsSecurityPolicy { get; set; } = false;
            public bool IsSecurityIdentity { get; set; } = false;
            public string PkiPath { get; set; } = "pki";
            public string SecurityPolicy { get; set; } = SecurityPolicyUris.None;
            public string ApplicationName { get; set; } = "UniCon Client";
            public string ApplicationUri { get; set; } = $"urn:unicon:client:{Guid.NewGuid():N}";

            public static OpcUaConnectionOptions Parse(string connectionString)
            {
                var options = new OpcUaConnectionOptions();
                if (string.IsNullOrWhiteSpace(connectionString)) return options;

                // 判断是否是标准的 key=value 键值对格式
                if (connectionString.Contains('=') && connectionString.Contains(';'))
                {
                    var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split('=', 2);
                        if (parts.Length != 2) continue;

                        var key = parts[0].Trim().ToUpperInvariant();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "ENDPOINTURL":
                            case "ENDPOINT":
                                options.EndpointURL = value;
                                break;
                            case "ANONYMOUSIDENTITY":
                            case "ANONYMOUS":
                                options.AnonymousIdentity = bool.TryParse(value, out var anon) ? anon : options.AnonymousIdentity;
                                break;
                            case "USERNAME":
                            case "USER":
                                options.Username = value;
                                options.AnonymousIdentity = false;
                                break;
                            case "PASSWORD":
                            case "PWD":
                                options.Password = value;
                                options.AnonymousIdentity = false;
                                break;
                            case "ISSECURITYPOLICY":
                            case "SECURITYPOLICYENABLED":
                                options.IsSecurityPolicy = bool.TryParse(value, out var secPol) ? secPol : options.IsSecurityPolicy;
                                break;
                            case "ISSECURITYIDENTITY":
                            case "CERTIFICATEAUTH":
                                options.IsSecurityIdentity = bool.TryParse(value, out var secId) ? secId : options.IsSecurityIdentity;
                                break;
                            case "PKIPATH":
                            case "CERTIFICATEPATH":
                                options.PkiPath = value;
                                break;
                            case "SECURITYPOLICY":
                                options.SecurityPolicy = MapSecurityPolicy(value);
                                break;
                            case "APPLICATIONNAME":
                            case "APPNAME":
                                options.ApplicationName = value;
                                break;
                            case "APPLICATIONURI":
                            case "APPURI":
                                options.ApplicationUri = value;
                                break;
                        }
                    }
                }
                else
                {
                    // 兼容模式：如果只是一个单纯的 URL（例如 opc.tcp://127.0.0.1:4840），则直接作为 Endpoint 终结点
                    options.EndpointURL = connectionString;
                }

                return options;
            }

            private static string MapSecurityPolicy(string policy)
            {
                var norm = policy.Trim().ToUpperInvariant();
                return norm switch
                {
                    "NONE" => SecurityPolicyUris.None,
                    "BASIC128RSA15" => SecurityPolicyUris.Basic128Rsa15,
                    "BASIC256" => SecurityPolicyUris.Basic256,
                    "BASIC256SHA256" => SecurityPolicyUris.Basic256Sha256,
                    _ => policy // 允许传入自定义的完整 URI 路径
                };
            }
        }

        private X509Identity GetCertIdentity(string pkiPath)
        {
            Org.BouncyCastle.X509.X509Certificate? userCert = null;
            Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters? userKey = null;

            var certParser = new Org.BouncyCastle.X509.X509CertificateParser();

            // 加载 DER 证书
            var certFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pkiPath, "own", "certs", "public.der");
            var certInfo = new FileInfo(certFile);
            if (certInfo.Exists)
            {
                using var crtStream = certInfo.OpenRead();
                var c = certParser.ReadCertificate(crtStream);
                if (c != null)
                {
                    userCert = c;
                }
            }

            // 加载 PEM 秘钥
            var keyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pkiPath, "own", "private", "private.pem");
            var keyInfo = new FileInfo(keyFile);
            if (keyInfo.Exists)
            {
                using var keyStream = new StreamReader(keyInfo.OpenRead());
                var keyReader = new Org.BouncyCastle.OpenSsl.PemReader(keyStream);
                if (keyReader.ReadObject() is Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair keyPair)
                {
                    userKey = keyPair.Private as Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters;
                }
            }

            if (userCert != null && userKey != null)
            {
                return new X509Identity(userCert, userKey);
            }

            throw new InvalidOperationException($"Failed to load required certificate assets from: {certFile}");
        }

        protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            var options = OpcUaConnectionOptions.Parse(connectionString);
            var (host, port) = NetworkHelper.EndpointUrlSplit(options.EndpointURL);

            if (!NetworkHelper.PingIsOk(host) || !NetworkHelper.TcpIsOk(host, port))
            {
                _logger.LogWarning($"OPC UA ping or TCP check failed for host '{host}' and port '{port}'.");
                return false;
            }

            var clientDescription = new ApplicationDescription
            {
                ApplicationName = options.ApplicationName,
                ApplicationUri = options.ApplicationUri,
                ApplicationType = ApplicationType.Client
            };

            ICertificateStore? certificateStore = null;
            IUserIdentity? identity = null;

            if (options.IsSecurityPolicy)
            {
                var storePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, options.PkiPath);
                certificateStore = new DirectoryStore(storePath);
                // 动态生成或提取本地应用程序证书
                await certificateStore.GetLocalCertificateAsync(clientDescription, null, default);
            }

            if (options.AnonymousIdentity)
            {
                identity = new AnonymousIdentity();
            }
            else if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                identity = new UserNameIdentity(options.Username, options.Password);
            }
            else if (options.IsSecurityIdentity)
            {
                identity = GetCertIdentity(options.PkiPath);
            }
            else
            {
                throw new InvalidOperationException("OPC UA Authentication failed: Credentials not configured. If anonymous, set Anonymous=True");
            }

            _channel = new ClientSessionChannel(
                clientDescription,
                certificateStore,
                identity,
                options.EndpointURL,
                options.SecurityPolicy
            );

            await _channel.OpenAsync(ct);
            return _channel.State == CommunicationState.Opened;
        }

        protected override async Task OnDisconnectAsync(CancellationToken ct)
        {
            if (_channel != null)
            {
                await _channel.CloseAsync(ct);
                _channel = null;
            }
        }

        protected override async Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
        {
            try
            {
                var readRequest = new ReadRequest
                {
                    NodesToRead = new[] { new ReadValueId { NodeId = NodeId.Parse(request.Address), AttributeId = AttributeIds.Value } }
                };
                var response = await _channel!.ReadAsync(readRequest, ct);
                var result = response.Results?[0];

                if (result == null || result.StatusCode != StatusCodes.Good)
                    return UniconResponse<T>.CreateFailure($"OPC Error: {result?.StatusCode}", (int)(result?.StatusCode.Value ?? 0u));

                return UniconResponse<T>.CreateSuccess((T)Convert.ChangeType(result.Value, typeof(T)));
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
                var writeRequest = new WriteRequest
                {
                    NodesToWrite = new[] { new WriteValue { NodeId = NodeId.Parse(request.Address), AttributeId = AttributeIds.Value, Value = new DataValue(new Variant(value)) } }
                };
                var response = await _channel!.WriteAsync(writeRequest, ct);
                var result = response.Results?[0];
                bool success = result == StatusCodes.Good;
                return success ? UniconResponse<bool>.CreateSuccess(true) : UniconResponse<bool>.CreateFailure($"Write failed: {result}", (int)(result?.Value ?? 0u));
            }
            catch (Exception ex)
            {
                return UniconResponse<bool>.CreateFailure(ex.Message, 500);
            }
        }

        public override void Dispose()
        {
            _channel?.AbortAsync().Wait();
            _channel?.CloseAsync().Wait();
            _syncLock.Dispose();
            _connectionLock.Dispose();
        }
    }
}
