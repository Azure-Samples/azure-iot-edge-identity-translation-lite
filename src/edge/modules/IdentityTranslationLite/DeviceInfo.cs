// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IdentityTranslationLite
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client; 
    
    public enum DeviceInfoStatus {
        New,
        Initialize,
        WaitingConfirmation,
        Confirmed,
        Registered,
        NotRegistered
    }

    public class DeviceInfo
    {
        public string DeviceId;
        public DeviceInfoStatus Status;
        public string SourceModuleId;
        public DeviceClient DeviceClient = null;
        protected List<Message> _waitingMessages;

        public DeviceInfo(string id)
        {
            this.DeviceId = id;
            this.Status = DeviceInfoStatus.New;
            this._waitingMessages = new List<Message>();
        }

        /// <summary>
        /// This method returns the current list of waiting messages.
        /// </summary>
        public IList<Message> GetWaitingList()
        {
            return this._waitingMessages;
        }

        /// <summary>
        /// This method clears the memory cache for the device and dispose all messages in the cache.
        /// </summary>
        public void ClearWaitingList()
        {
            foreach(Message message in this._waitingMessages)
            {
                message.Dispose();
            }
            this._waitingMessages.Clear();
        }

        /// <summary>
        /// This method can be used to add a new message to the end of the waiting list if the device is registered.
        /// </summary>
        public bool TryAddToWaitingList(Message message)
        {
            if ((this.Status == DeviceInfoStatus.New) || (this.Status == DeviceInfoStatus.Initialize) ||
                (this.Status == DeviceInfoStatus.WaitingConfirmation) || (this.Status == DeviceInfoStatus.Confirmed))
            {
                this._waitingMessages.Add(message);
                return true;    
            }
            return false;
        }
    }
}