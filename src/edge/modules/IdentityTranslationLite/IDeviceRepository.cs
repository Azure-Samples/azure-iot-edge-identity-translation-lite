namespace IdentityTranslationLite
{
    public interface IDeviceRepository
    {
        bool Contains(string id);
        DeviceInfo Get(string id);
        DeviceInfo GetOrAdd(string id);    
    }
}