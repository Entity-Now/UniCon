// Copyright 2020 Siemens AG
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using UniCon.OpcUaPubSub.binary;
using UniCon.OpcUaPubSub.binary.DataPoints;
using UniCon.OpcUaPubSub.binary.Decode;
using UniCon.OpcUaPubSub.client.common.Settings;
using UniCon.OpcUaPubSub.client.Interfaces;
using static UniCon.OpcUaPubSub.client.ProcessDataSet;

namespace UniCon.OpcUaPubSub.client
{
    public class TestClient : ITestClient
    {
        public TestClient(Settings settings, string clientId = null)
        {
            Settings = settings;
            ClientId = clientId;
            if (ClientId == null)
            {
                ClientId =
                        $"Client_{Assembly.GetEntryAssembly().FullName.Split(',')[0]}_{Environment.MachineName}";
                ;
            }
        }

        public uint AutomaticKeyAndMetaSendInterval { get; set; }
        public byte[] BrokerCACert { get; set; }
        public uint ChunkSize { get; set; }
#pragma warning disable 67
        public event EventHandler<string> ClientDisconnected;
#pragma warning restore 67
        public string ClientId { get; set; }

        public void Connect(ClientCredentials credentials = null)
        {
            IsConnected = !SimulateConnectError;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public ProcessDataSet GenerateDataSet(string name, ushort writerId, DataSetType dataSetType)
        {
            return new ProcessDataSet(ClientId, name, writerId, dataSetType);
        }

        public bool IsConnected { get; private set; }
        public Dictionary<uint, DateTime> LastKeyAndMetaSentTimes { get; set; }
        public event DecodeMessage.MessageDecodedEventHandler MessageReceived;
#pragma warning disable 67
        public event DecodeMessage.MessageDecodedEventHandler MetaMessageReceived;
#pragma warning restore 67
        public EncodingOptions Options { get; }

        public bool SendDataSet(ProcessDataSet dataSet, string m_TopicConfigRequest, bool delta)
        {
            ReceiveDataFromApp?.Invoke(this, dataSet);
            return true;
        }

        public bool SendDataSet(ProcessDataSet dataSet, bool delta)
        {
            ReceiveDataFromApp?.Invoke(this, dataSet);
            return true;
        }

        public bool SendFile(File file, ushort writerId)
        {
            return true;
        }

        public bool SendFile(File file, string topicPrefix, ushort writerId)
        {
            return true;
        }

        public void SendKeepAlive(ProcessDataSet dataSet) { }
        public Settings Settings { get; }
        public void Subscribe(string topic) { }
#pragma warning disable 67
        public event DecodeMessage.MessageDecodedEventHandler UnknownMessageReceived;
#pragma warning restore 67
        public event EventHandler<ProcessDataSet> ReceiveDataFromApp;

        public void SendDataToApp(ProcessDataSet dataSet, string topic, string publisherId = null)
        {
            if (!string.IsNullOrEmpty(publisherId))
            {
                ClientId = publisherId;
                dataSet.PublisherId = publisherId;
            }
            if (string.IsNullOrEmpty(topic))
            {
                topic = Settings.Client.DefaultPublisherTopicName;
                topic = Client.CreateTopicName(topic, ClientId, dataSet.GetWriterId(), "Meas", dataSet.GetDataSetType());
            }
            MessageDecodedEventArgs args = new MessageDecodedEventArgs(dataSet.GenerateDateFrame(), topic);
            MessageReceived?.Invoke(this, args);
        }

        public bool SimulateConnectError { get; set; }

        private void OnRecievedDataFromApp(object sender, EventArgs args)
        {
            Console.WriteLine(sender);
            Console.WriteLine(args);
        }

        public bool SendRawData(byte[] payload, string topic, bool retain)
        {
            Console.WriteLine(payload);
            Console.WriteLine(topic);
            return true;

        }

#pragma warning disable 67
        public event EventHandler<Exception> ExceptionCaught;
        public event FileReceivedEventHandler FileReceived;
        public event RawDataReceivedEventHandler RawDataReceived;
#pragma warning restore 67
    }
}