// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IdentityTranslationLite
{
    using System;
    using System.Net.Http;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;

    class Program
    {
        static string _edgeDeviceId;
        static string _edgeModuleId;
        static string _iothubHostName;
        static string _gatewayHostName;
        static bool _useTransparentGateway = true;
        static bool _cacheMessagesDuringRegistration = true;
        static IDeviceRepository _leafDevices;

        const string WorkloadApiVersion = "2019-01-30";
        const string WorkloadUriVariableName = "IOTEDGE_WORKLOADURI";
        const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";
        const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        const string ModuleIdVariableName = "IOTEDGE_MODULEID";
        const string ModuleGenerationIdVariableName = "IOTEDGE_MODULEGENERATIONID";

        const string ItmMessageInputName = "itminput";
        const string ItmMessageOutputName = "itmoutput";
        const string ItmCallbackMethodName = "ItmCallback";
        const string LeafDeviceIdPropertyName = "leafDeviceId";
        const string LeafDeviceModuleIdPropertyName = "moduleId";

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Get environment variables scoped for this module
            _edgeDeviceId = Environment.GetEnvironmentVariable(DeviceIdVariableName);
            _edgeModuleId = Environment.GetEnvironmentVariable(ModuleIdVariableName);
            _iothubHostName = Environment.GetEnvironmentVariable(IotHubHostnameVariableName);
            _gatewayHostName = Environment.GetEnvironmentVariable(GatewayHostnameVariableName);

            // Initialize leaf device cache
            _leafDevices = new MemoryDeviceRepository();

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetMethodHandlerAsync(ItmCallbackMethodName, DeviceRegistered, ioTHubModuleClient);
            await ioTHubModuleClient.SetInputMessageHandlerAsync(ItmMessageInputName, PipeMessage, ioTHubModuleClient);
        }

        /// <summary>
        /// Sign the given payload using the Worload API of the locasl Security Deamon.async
        /// The signed payload is returned as Base64 string.
        /// </summary>
        static async Task<string> SignAsync(string payload)
        {
            string generationId = Environment.GetEnvironmentVariable(ModuleGenerationIdVariableName);
            Uri workloadUri = new Uri(Environment.GetEnvironmentVariable(WorkloadUriVariableName));

            string signedPayload = string.Empty;
            using (HttpClient httpClient = Microsoft.Azure.Devices.Edge.Util.HttpClientHelper.GetHttpClient(workloadUri)) {
                httpClient.BaseAddress = new Uri(Microsoft.Azure.Devices.Edge.Util.HttpClientHelper.GetBaseUrl(workloadUri));

                var workloadClient = new WorkloadClient(httpClient);
                var signRequest = new SignRequest() {
                    KeyId = "primary", // or "secondary"
                    Algo = SignRequestAlgo.HMACSHA256,
                    Data = Encoding.UTF8.GetBytes(payload)
                };

                var signResponse = await workloadClient.SignAsync(WorkloadApiVersion, _edgeModuleId, generationId, signRequest);
                signedPayload = Convert.ToBase64String(signResponse.Digest);
            }

            return signedPayload;
        }

        public static void DeviceConnectionChanged(Microsoft.Azure.Devices.Client.ConnectionStatus status, Microsoft.Azure.Devices.Client.ConnectionStatusChangeReason reason)
        {
            Console.WriteLine($"Device connection status changed to '{status}', because of {reason}.");
        }

        /// <summary>
        /// This method is called when a device registration response direct method call is received from IoTHub.
        /// On succesful registration, it creates a device client and sends all waiting messages.
        /// </summary>
        static async Task<MethodResponse> DeviceRegistered(MethodRequest methodRequest, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            var methodResponse = new MethodResponse(200);

            // Process device registration
            var response = JsonConvert.DeserializeObject<RegistrationResponse>(methodRequest.DataAsJson);
            string leafDeviceId = response.DeviceId;
            var leafDevice = _leafDevices.Get(leafDeviceId);
            if (leafDevice == null)
            {
                methodResponse = new MethodResponse(404); // Not Found
            }
            else if (leafDevice.Status == DeviceInfoStatus.WaitingConfirmation)
            {
                leafDevice.Status = DeviceInfoStatus.Confirmed;
                if ((response.ResultCode == 200) || (response.ResultCode == 201)) // OK, Created
                {
                    Console.WriteLine($"Leaf device '{leafDeviceId}' registered with IoTHub: {response.ResultDescription}");

                    // Create new DeviceClient for the leaf device
                    string signedKey = await SignAsync(leafDeviceId);
                    IAuthenticationMethod authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(leafDeviceId, signedKey);
                    leafDevice.DeviceClient = _useTransparentGateway ? DeviceClient.Create(_iothubHostName, _gatewayHostName, authMethod) : DeviceClient.Create(_iothubHostName, authMethod);
                    leafDevice.DeviceClient.SetConnectionStatusChangesHandler(DeviceConnectionChanged);

                    // Send waiting messages
                    leafDevice.Status = DeviceInfoStatus.Registered;
                    var waitingMessages = leafDevice.GetWaitingList();
                    Console.WriteLine($"Sending {waitingMessages.Count} waiting messages from leaf device '{leafDeviceId}' to IoTHub.");
                    await leafDevice.DeviceClient.SendEventBatchAsync(waitingMessages);
                    leafDevice.ClearWaitingList();
                }
                else if ((response.ResultCode == 401) || (response.ResultCode == 403) || (response.ResultCode == 404)) // Unauthorized, Forbidden, Not Found
                {
                    Console.WriteLine($"Registration of leaf device '{leafDeviceId}' not allowed: {response.ResultDescription}");
                    leafDevice.Status = DeviceInfoStatus.NotRegistered;
                }
                else
                {
                    Console.WriteLine($"ERROR: Unsuccesful registration response (code {response.ResultCode}: {response.ResultDescription}) for leaf device '{leafDeviceId}' received.");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Registration response (code {response.ResultCode}: {response.ResultDescription}) for leaf device '{leafDeviceId}' with invalid status {leafDevice.Status} received.");
            }

            return methodResponse;
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            // Handle messages from leaf devices
            if (message.Properties.Keys.Contains(LeafDeviceIdPropertyName))
            {
                DeviceInfo leafDevice;
                string leafDeviceId = message.Properties[LeafDeviceIdPropertyName];

                // Register the leaf device if not already registered
                if (!_leafDevices.Contains(leafDeviceId))
                {
                    // Not registered yet, so start new registration
                    leafDevice = _leafDevices.GetOrAdd(leafDeviceId);
                    if (leafDevice.Status == DeviceInfoStatus.New)
                    {
                        await RegisterLeafDevice(moduleClient, leafDevice, message.Properties[LeafDeviceModuleIdPropertyName]);
                    }
                }

                // Add message from leaf device to cache if not yet registered, otherwise pipe to IoTHub
                leafDevice = _leafDevices.Get(leafDeviceId);
                var leafDeviceMessage = CloneMessage(message);
                if (_cacheMessagesDuringRegistration && leafDevice.TryAddToWaitingList(leafDeviceMessage))
                {
                    Console.WriteLine($"Adding message from leaf device '{leafDeviceId}' on waiting list while waiting for registration completion.");
                }
                else if (leafDevice.Status == DeviceInfoStatus.Registered)
                {
                    try {
                        Console.WriteLine($"Sending message from leaf device '{leafDeviceId}' to IoTHub.");
                        await leafDevice.DeviceClient.SendEventAsync(leafDeviceMessage);
                    }
                    catch (Microsoft.Azure.Devices.Client.Exceptions.UnauthorizedException ex) {
                        Console.WriteLine($"ERROR: Leaf defice '{leafDeviceId}' unauthorized while sending event to IoTHub: {ex.Message}.");
                    }
                    finally {
                        leafDeviceMessage.Dispose();
                    }
                }
            }
            else
            {
                // Not a leaf device message so just pipe message to output
                using (var pipeMessage = CloneMessage(message))
                {
                    Console.WriteLine($"Pipe message from (non-leaf) device.");
                    await moduleClient.SendEventAsync("itmoutput", pipeMessage);
                }
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// Send registration request message to register leaf device.
        /// </summary>
        static async Task RegisterLeafDevice(ModuleClient moduleClient, DeviceInfo leafDevice, string sourceModuleId)
        {
            leafDevice.Status = DeviceInfoStatus.Initialize;
            leafDevice.SourceModuleId = sourceModuleId;

            // Create new request and fill message body
            RegistrationRequest request = new RegistrationRequest() {
                hubHostname = _iothubHostName, 
                leafDeviceId = leafDevice.DeviceId,
                edgeDeviceId = _edgeDeviceId,
                edgeModuleId = _edgeModuleId, 
                operation = "create"   
            };

            // Send message to register leaf device in IoTHub
            string requestText = JsonConvert.SerializeObject(request);
            using (Message registrationMessage = new Message(Encoding.UTF8.GetBytes(requestText)))
            {
                registrationMessage.ContentEncoding = "utf-8";
                registrationMessage.ContentType = "application/json";
                registrationMessage.Properties.Add("itmtype", "LeafEvent");
                await moduleClient.SendEventAsync(ItmMessageOutputName, registrationMessage);
                Console.WriteLine($"Registering leaf device '{leafDevice.DeviceId}' with IoTHub.");
            }

            leafDevice.Status = DeviceInfoStatus.WaitingConfirmation;
        }

        /// <summary>
        /// Clone the given message, removing only the leaf device specific properties if exists.
        /// </summary>
        static Message CloneMessage(Message message)
        {
            Message newMessage = null;

            byte[] messageBytes = message.GetBytes();

            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"DEBUG: Original message body: {messageString}");

            newMessage = new Message(messageBytes);
            foreach (var prop in message.Properties)
            {
                if (!prop.Key.Equals(LeafDeviceIdPropertyName, StringComparison.InvariantCultureIgnoreCase) &&
                    !prop.Key.Equals(LeafDeviceModuleIdPropertyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    newMessage.Properties.Add(prop.Key, prop.Value);
                }
            }

            return newMessage;
        }
    }
}
