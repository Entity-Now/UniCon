// Copyright 2020 Siemens AG
// SPDX-License-Identifier: MIT

using UniCon.OpcUaPubSub.binary.Messages;
using UniCon.OpcUaPubSub.binary.Messages.Meta;

namespace UniCon.OpcUaPubSub.binary.Storage
{
    public interface IStorage
    {
        byte[] GetMetaMessage(string publisherId, ConfigurationVersion cfgVersion);
        void StoreDataMessage(DataFrame dataFrame);
        void StoreMetaMessage(MetaFrame metaFrame);
    }
}