public class WDS_OPC
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;

        private static readonly object opcLook = new { };
        public ClientSessionChannel? OPC { get; set; }

        public OPC_Option OPC_Option = new OPC_Option();

        public WDS_OPC(ILogger logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            configuration.GetSection(OPC_Option.Name).Bind(OPC_Option);
            logger.Information("Creating OPC service.");
            if (CheckNetwork())
            {
                OPC = Create();
            }
            else
            {
                logger.Error("Network connect failed, please restart program");
            }
        }

        private X509Identity GetCertIdentity()
        {
            // read x509Identity
            var userCert = default(X509Certificate);
            var userKey = default(RsaKeyParameters);

            var certParser = new X509CertificateParser();
            var userCertInfo = new FileInfo(Path.Combine(Constants.ApplicationCurrentPath, "pki", "own", "certs", "public.der"));
            if (userCertInfo.Exists)
            {
                using (var crtStream = userCertInfo.OpenRead())
                {
                    var c = certParser.ReadCertificate(crtStream);
                    if (c != null)
                    {
                        userCert = c;
                    }
                }
            }
            var userKeyInfo = new FileInfo(Path.Combine(Constants.ApplicationCurrentPath, "pki", "own", "private", "private.pem"));
            if (userKeyInfo.Exists)
            {
                using (var keyStream = new StreamReader(userKeyInfo.OpenRead()))
                {
                    var keyReader = new PemReader(keyStream);
                    if (keyReader.ReadObject() is AsymmetricCipherKeyPair keyPair)
                    {
                        userKey = keyPair.Private as RsaKeyParameters;
                    }
                }
            }
            if (userCert != null && userKey != null)
            {
                return new X509Identity(userCert, userKey);
            }
            throw new BusinessException("-1","An error occurred while obtaining the OPC certificate");
        }
        /// <summary>
        /// 检查网络状态
        /// </summary>
        /// <returns>是一个布尔值，返回网络状态结果</returns>
        public bool CheckNetwork()
        {
            var (url, port) = NetworkHelper.EndpointUrlSplit(OPC_Option.EndpointURL);
            if (!NetworkHelper.PingIsOk(url))
            {
                logger.Error($"Ping failed, IP Address={url}");
                return false;
            }
            if (!NetworkHelper.TcpIsOk(url, port))
            {
                logger.Error($"TCP Connection failed, IP Address={url}");
                return false;
            }
            logger.Information("Network status is normal");
            return true;
        }
        /// <summary>
        /// 创建OPC服务
        /// </summary>
        /// <returns></returns>
        /// <exception cref="BusinessException"></exception>
        private ClientSessionChannel Create()
        {
            lock (opcLook)
            {
                if (OPC != null && OPC.State == CommunicationState.Opened)
                {
                    return OPC;
                }
            }
            while (true)
            {
                try
                {
                    lock (opcLook)
                    {
                        // describe this client application.
                        var clientDescription = new ApplicationDescription
                        {
                            ApplicationName = Constants.ApplicationName,
                            ApplicationUri = Constants.ApplicationURI,
                            ApplicationType = ApplicationType.Client
                        };
                        ICertificateStore? certificateStore = null;
                        IUserIdentity? identity = null;
                        if (OPC_Option.IsSecurityPolicy)
                        {
                            certificateStore = new DirectoryStore(Path.Combine(Constants.ApplicationCurrentPath, "pki"));
                            // 生成一个证书
                            certificateStore.GetLocalCertificateAsync(clientDescription, null, default);
                        }
                        if (OPC_Option.AnonymousIdentity)
                        {
                            identity = new AnonymousIdentity();
                        }
                        else if (!string.IsNullOrEmpty(OPC_Option.Username) && !string.IsNullOrEmpty(OPC_Option.Password))
                        {
                            identity = new UserNameIdentity(OPC_Option.Username, OPC_Option.Password);
                        }
                        else if (OPC_Option.IsSecurityIdentity)
                        {
                            identity = GetCertIdentity();
                        }
                        else
                        {
                            throw new BusinessException("-1", "Authentication parameters not provided. if anonymous login is required, please set AnonymousIdentity to True");
                        }
                        var channel = new ClientSessionChannel(
                                clientDescription, // 程序描述,
                                certificateStore, // 无 x509 certificates证书
                                identity,
                                OPC_Option.EndpointURL,
                                SecurityPolicyUris.Basic256Sha256
                            ); ;
                        channel.Opened += (sender, e) =>
                        {
                            logger.Information("OPC service created successfully, about to check network status");
                        };
                        channel.Faulted += async (sender, e) =>
                        {
                            logger.Error($"An error occurred with OPC, attempting to reconnect.");
                            try
                            {
                                Reconnect();
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex.Message);
                            }
                        };
                        return channel;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                }
            }
        }
        /// <summary>
        /// 重新连接服务
        /// </summary>
        public void Reconnect()
        {
            if (OPC != null && OPC.State == CommunicationState.Faulted)
            {
                lock (opcLook)
                {
                    if (OPC != null && OPC.State == CommunicationState.Faulted)
                    {
                        logger.Information("Session faulted. Attempting to reconnect...");
                        OPC.AbortAsync().ConfigureAwait(true);
                        OPC = null;
                        OPC = Create();
                    }
                }
            }
        }
        /// <summary>
        /// 获取OPC的状态
        /// </summary>
        /// <returns></returns>
        public CommunicationState GetState()
        {
            return OPC == null ? CommunicationState.Created : OPC.State;
        }
        /// <summary>
        /// 打开OPC的连接
        /// </summary>
        /// <returns></returns>
        public async Task OpenAsync()
        {
            if (OPC != null && OPC.State == CommunicationState.Faulted)
            {
                await Task.Delay(100);
                await OpenAsync();
                return;
            }
            if (OPC != null && OPC.State == CommunicationState.Opening)
            {
                //logger.Information("OPC establishing a connection...");
                await Task.Delay(100);
                await OpenAsync();
                return;
            }
            if (OPC != null && OPC.State == CommunicationState.Created)
            {
                if (!CheckNetwork())
                {
                    logger.Error("Failed to connect to the OPC service, will retry in 5000 milliseconds");
                    await Task.Delay(5000);
                    await OpenAsync();
                    return;
                }
                await OPC.OpenAsync();
            }
        }
        /// <summary>
        /// 根据Node节点信息获取OPC的Tag值
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public async Task<ReadResponse> GetNodeValueAsync(List<string> nodes)
        {
            try
            {
                await OpenAsync();

                // build a ReadRequest. See 'OPC UA Spec Part 4' paragraph 5.10.2
                var readRequest = new ReadRequest
                {
                    // set the NodesToRead to an array of ReadValueIds.5
                    NodesToRead = nodes.Select(x =>  // construct a ReadValueId from a NodeId and AttributeId.
                        new ReadValueId
                        {
                            // you can parse the nodeId from a string.
                            // e.g. NodeId.Parse("ns=2;s=Demo.Static.Scalar.Double")
                            NodeId = NodeId.Parse(x),
                            // variable class nodes have a Value attribute.
                            AttributeId = AttributeIds.Value
                        }).ToArray()
                };

                return await OPC.ReadAsync(readRequest);
            }
            catch (ServiceResultException ex)
            {
                ex.ServiceResultExceptionHandle(this, nodes.JoinAsString(";"));
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }
        }
        /// <summary>
        /// 根据节点信息设置OPC的Tag值
        /// </summary>
        /// <param name="nodes">一个节点字典，Key是NodeId，Value是值</param>
        /// <returns></returns>
        public async Task<WriteResponse> SetNodeValueAsync(Dictionary<string, object> nodes)
        {
            try
            {
                await OpenAsync();

                var writerequest = new WriteRequest
                {
                    NodesToWrite = nodes.Select(x =>
                        new WriteValue
                        {
                            AttributeId = AttributeIds.Value,
                            NodeId = NodeId.Parse(x.Key),
                            Value = new DataValue(new Variant(x.Value))
                        }
                    ).ToArray()
                };
                return await OPC.WriteAsync(writerequest);
            }
            catch(ServiceResultException ex)
            {
                ex.ServiceResultExceptionHandle(this, nodes.Keys.JoinAsString(","));
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }
        }

        ~WDS_OPC()
        {
            OPC.AbortAsync();
            OPC.CloseAsync();
        }
    }