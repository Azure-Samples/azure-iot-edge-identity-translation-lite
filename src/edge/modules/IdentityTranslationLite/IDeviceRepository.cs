// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IdentityTranslationLite
{
    public interface IDeviceRepository
    {
        bool Contains(string id);
        DeviceInfo Get(string id);
        DeviceInfo GetOrAdd(string id);    
    }
}