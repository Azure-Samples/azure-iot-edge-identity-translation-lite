
namespace IdentityTranslationLite
{
    using System.Collections.Concurrent;
    
    public class MemoryDeviceRepository : IDeviceRepository
    {
        protected ConcurrentDictionary<string, DeviceInfo> _deviceInfos;

        public MemoryDeviceRepository()
        {
            this._deviceInfos = new ConcurrentDictionary<string, DeviceInfo>();
        }

        public bool Contains(string id)
        {
            return this._deviceInfos.ContainsKey(id); 
        }   

        public DeviceInfo Get(string id)
        {
            DeviceInfo result = null;
            this._deviceInfos.TryGetValue(id, out result);
            return result; 
        }        

        public DeviceInfo GetOrAdd(string id)
        {
            DeviceInfo newEntity = new DeviceInfo(id);
            return this._deviceInfos.GetOrAdd(id, newEntity);
        }
    }
}