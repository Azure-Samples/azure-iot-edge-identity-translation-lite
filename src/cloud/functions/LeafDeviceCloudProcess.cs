using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace IcaIdentityTranslation.LeafDeviceProcess
{
    public static class LeafDeviceCloudProcess
    {
        private static string iotHubConnectionString;
        private static ServiceClient serviceClient;
        private static RegistryManager registryManager;

        [FunctionName("LeafDeviceCloudEventGrid")]
        public static async Task EventGridProcess([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation("LeafDeviceCloudEventGrid function processing Event Grid trigger.");
            log.LogInformation(eventGridEvent.Data.ToString());
            
            iotHubConnectionString = System.Environment.GetEnvironmentVariable("iotHubConnectionString", EnvironmentVariableTarget.Process);
            dynamic data = JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
            //Note: we assume a JSON payload in the body, 'UTF-8' Encoded AND 'application/json' content type. Otherwise body will be base64 encoded
            LeafEvent deviceEvent = JsonConvert.DeserializeObject<LeafEvent>(data.body.ToString());

            registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

            log.LogInformation($"DeviceId: {deviceEvent.LeafDeviceId}");
            log.LogInformation($"Parent device id: {deviceEvent.EdgeDeviceId}");

            DmCallback callbackResult = new DmCallback();
            callbackResult.DeviceId = deviceEvent.LeafDeviceId;

            switch (deviceEvent.Operation)
            {
                case LeafEvent.Operations.create:

                    //check if device is whitelisted
                    string storageConnection = System.Environment.GetEnvironmentVariable("WhitelistStorageConnection", EnvironmentVariableTarget.Process);
                    string container = System.Environment.GetEnvironmentVariable("WhitelistContainerName", EnvironmentVariableTarget.Process);
                    string filename = System.Environment.GetEnvironmentVariable("WhitelistFilename", EnvironmentVariableTarget.Process);

                    bool isValid = await CheckWhiteListDeviceId(storageConnection, container, filename, deviceEvent.LeafDeviceId);

                    if (isValid)
                    {

                        var parentDevice = await registryManager.GetDeviceAsync(deviceEvent.EdgeDeviceId);
                        var parentModule = await registryManager.GetModuleAsync(deviceEvent.EdgeDeviceId, deviceEvent.EdgeModuleId);

                        if (parentDevice == null || parentModule == null)
                        {
                            log.LogError("Error in event grid processing, either module or parent device could not be loaded. Exiting function.");
                            break;
                        }
                        else
                        {
                            //Get module key to be used as master
                            string deviceSymmetricKey = ComputeDerivedSymmetricKey(
                                Convert.FromBase64String(parentModule.Authentication.SymmetricKey.PrimaryKey),
                                deviceEvent.LeafDeviceId);
                            string deviceSecondaryKey = ComputeDerivedSymmetricKey(
                                Convert.FromBase64String(parentModule.Authentication.SymmetricKey.SecondaryKey),
                                deviceEvent.LeafDeviceId);

                            //create new device in registry
                            Device newDevice = new Device(deviceEvent.LeafDeviceId)
                            {
                                Authentication = new AuthenticationMechanism()
                                {
                                    SymmetricKey = new SymmetricKey()
                                    {
                                        PrimaryKey = deviceSymmetricKey,
                                        SecondaryKey = deviceSecondaryKey
                                    }
                                },
                                Scope = parentDevice.Scope
                            };

                            Device leafDevice;
                            try
                            {
                                leafDevice = await registryManager.AddDeviceAsync(newDevice);
                                callbackResult.ResultCode = 200;
                                callbackResult.ResultDescriptionn = "Device successfully created in IoT Hub";
                                
                            }
                            catch (DeviceAlreadyExistsException)
                            {
                                log.LogWarning($"Device {deviceEvent.LeafDeviceId} already exists, updating only state to 'enabled' and keys");
                                leafDevice = await registryManager.GetDeviceAsync(deviceEvent.LeafDeviceId);
                                leafDevice.Authentication = new AuthenticationMechanism()
                                {
                                    SymmetricKey = new SymmetricKey()
                                    {
                                        PrimaryKey = deviceSymmetricKey,
                                        SecondaryKey = deviceSecondaryKey
                                    }
                                };
                                leafDevice.Scope = parentDevice.Scope;
                                leafDevice.Status = DeviceStatus.Enabled;
                                await registryManager.UpdateDeviceAsync(leafDevice);
                                callbackResult.ResultCode = 200;
                                callbackResult.ResultDescriptionn = "Device already existed, we updated the IoT Hub registration to 'enabled'";

                            }
                            catch (System.Exception exception)
                            {
                                log.LogError($"Exception when adding leaf device: {exception.Message}");
                            }

                        }
                    }
                    else
                    {
                        log.LogWarning($"Device '{deviceEvent.LeafDeviceId}' is not a valid device, not creating/activating");
                        callbackResult.ResultCode = 400;
                        callbackResult.ResultDescriptionn = "Device not whitelisted";
                    }
                    break;

                case LeafEvent.Operations.delete:
                    try
                    {
                        await registryManager.RemoveDeviceAsync(deviceEvent.LeafDeviceId);
                        callbackResult.ResultCode = 200;
                        callbackResult.ResultDescriptionn = "Device successfully deleted in IoT Hub";
                        log.LogInformation($"Device deleted '{deviceEvent.LeafDeviceId}'");
                    }
                    catch (DeviceNotFoundException dle)
                    {
                        callbackResult.ResultCode = 500;
                        callbackResult.ResultDescriptionn = "DeviceNotFoundException occurred when trying to delete the device";
                        log.LogError($"DeviceNotFoundException when trying to delete leaf device: {dle.Message}");
                        
                    }
                    catch (System.Exception exception)
                    {
                        callbackResult.ResultCode = 500;
                        callbackResult.ResultDescriptionn = $"Exception when trying to delete the device: {exception.Message}";
                        log.LogError($"Exception when trying to delete leaf device: {exception.Message}");
                    }

                    break;

                case LeafEvent.Operations.disable:
                    try
                    {
                        var device = await registryManager.GetDeviceAsync(deviceEvent.LeafDeviceId);
                        device.Status = DeviceStatus.Disabled;

                        await registryManager.UpdateDeviceAsync(device);
                        callbackResult.ResultCode = 200;
                        callbackResult.ResultDescriptionn = "Device successfully disabled in IoT Hub";

                        log.LogInformation($"Device disabled: '{deviceEvent.LeafDeviceId}'");

                    }catch(System.Exception exc)
                    {
                        callbackResult.ResultCode = 500;
                        callbackResult.ResultDescriptionn = $"Error when trying to disable device: {exc.Message}";
                    }

                    break;

            }

            //DM with result to Module (also if error, then Edge module can log locally)
            serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
            

            var methodInvocation = new CloudToDeviceMethod("ItmCallback") { ResponseTimeout = TimeSpan.FromSeconds(30) };
            methodInvocation.SetPayloadJson(JsonConvert.SerializeObject(callbackResult));

            try
            {            
                var response = await serviceClient.InvokeDeviceMethodAsync(deviceEvent.EdgeDeviceId, deviceEvent.EdgeModuleId, methodInvocation);
                log.LogInformation($"Response status: {response.Status}, payload: {response.GetPayloadAsJson()}");
            }
            catch
            {
                //TODO retry?
                log.LogWarning($"Error in calling DM 'ItmCallback' to module '{deviceEvent.EdgeModuleId}'");
            }

            log.LogInformation("Finished processing LeafDeviceCloudEventGrid function");

        }


        /// <summary>
        /// Generate the derived symmetric key for the provisioned device from the enrollment group symmetric key used in attestation
        /// </summary>
        /// <param name="masterKey">Symmetric key enrollment group primary/secondary key value</param>
        /// <param name="registrationId">the registration id to create</param>
        /// <returns>the primary/secondary key for the member of the enrollment group</returns>
        public static string ComputeDerivedSymmetricKey(byte[] masterKey, string registrationId)
        {
            using (var hmac = new HMACSHA256(masterKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
            }
        }

        public static async Task<bool> CheckWhiteListDeviceId(string connectionString,
            string containerName,
            string filename,
            string deviceId)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(filename);

            List<string> values = new List<string>();

            using (BlobDownloadInfo download = await blobClient.DownloadAsync())
            {
                string currentValue;
                
                using (var stream = new MemoryStream())
                {
                    await download.Content.CopyToAsync(stream);
                    stream.Position = 0;
                    using (StreamReader streamReader = new StreamReader(stream, Encoding.Default, true))
                    {
                        while ((currentValue = streamReader.ReadLine()) != null)
                        {
                            values.Add(currentValue);
                        }
                    }
                }
            }

            var result = values.Exists(e => e.Contains(deviceId));

            return result;
        }

    }




}


public class LeafEvent
{
    [JsonProperty("hubHostname")]
    public string HubHostName { get; set; }

    [JsonProperty("leafDeviceId")]
    // all leaf devices have a unique ID ex. serial #, MAC address etc.
    public string LeafDeviceId { get; set; }

    [JsonProperty("edgeDeviceId")]
    // Edge device id where an Identity Translation Module (ITM) runs
    public string EdgeDeviceId { get; set; }

    [JsonProperty("edgeModuleId")]
    public string EdgeModuleId { get; set; } // ITM module id

    [JsonProperty("operation")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Operations Operation { get; set; }

    public enum Operations
    {
        create,
        delete,
        disable
    }

}

public class DmCallback
{
    public string DeviceId {get;set;}
    
    public int ResultCode {get;set;}

    public string ResultDescriptionn {get;set;}

}