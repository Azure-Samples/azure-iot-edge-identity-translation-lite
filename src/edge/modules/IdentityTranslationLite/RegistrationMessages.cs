
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IdentityTranslationLite
{
    using System;
    
    public class RegistrationRequest
    {
        public string hubHostname; 
        public string leafDeviceId; // all leaf devices have a unique ID ex. serial #, MAC address etc. 
        public string edgeDeviceId; // Edge device id where an Identity Translation Module (ITM) runs 
        public string edgeModuleId; // ITM module id 
        public string operation; // "create", "delete", "disable".         
    }
    
    public class RegistrationResponse
    {
        public string DeviceId;
        public int ResultCode;
        public string ResultDescription;
    }
}