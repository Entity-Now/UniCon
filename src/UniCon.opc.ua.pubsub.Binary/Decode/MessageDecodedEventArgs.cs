// Copyright 2020 Siemens AG
// SPDX-License-Identifier: MIT

using System;
using UniCon.OpcUaPubSub.binary.Messages;

namespace UniCon.OpcUaPubSub.binary.Decode
{
    public class MessageDecodedEventArgs : EventArgs
    {
        public MessageDecodedEventArgs(NetworkMessage message, string topic)
        {
            Message = message;
            Topic = topic;
        }

        public NetworkMessage Message { get; set; }
        public string Topic { get; set; }
    }
}