# Introduction 
This project contains an identity translation edge module for Azure IoT Edge to connect leaf devices with their own identity to IoTHub via an edge device. This identity translation module is based on the IdentityTranslationLite design and implements the [Identity Translation Pattern](https://docs.microsoft.com/bs-latn-ba/azure/iot-edge/iot-edge-as-gateway#patterns). This pattern contains both protocol translation and identity translation. Because protocol translation functionality is heavily dependent on the exact protocol to be translated, while identity translation is generic, both functions are separated into diferent IoT Edge modules. To get started, a demo [Protocol Translation module](/src/edge/modules/ptm-mqtt) is provided with this sample. The rest of this document will describe the reusable [Identity Translation module](/src/edge/modules/IdentityTranslationLite) in detail.

# Leaf device messages
The Identity Translation Module will identify messages from downstream devices by looking for a property named "leafDeviceId" in the message.

The protocol translation module must add the ID of the connected leaf device as the "leafDeviceId" property to the message, before sending it back to the edge runtime. The Identity Translation Module will pass through all other properties and the message body as is.

Please note that the leafDeviceId will be used to create the device in IoTHub and therefore must be unique. A good candidate for the leafDeviceId is therefore serial # or MAC address.

# Message routing

The Identity Translation module should be configured in the routing table of the IoT Edge device to receive messages from the protocol translation module and send messages to the IoTHub (upstream). See example below.

```
"routes": {
    "LeafDeviceMessagesToIoTHub":"FROM /messages/* WHERE NOT IS_DEFINED($connectionModuleId) INTO $upstream",
    "IdentityTranslationToIoTHub": "FROM /messages/modules/IdentityTranslationLite/outputs/itmoutput INTO $upstream",
    "PtmMqttToIdentityTranslation": "FROM /messages/modules/ptm-mqtt/outputs/ptm_output INTO BrokeredEndpoint(\"/modules/IdentityTranslationLite/inputs/itminput\")"
},
```

# Message Processing

The Identity Translation Module will filter out the messages from leaf devices (containing the "leafDeviceId" property) and pass through all other messages.

The messages from leaf devices will be handled as follows.
- If this is the first messages received for this leaf device (the leafDeviceId cannot be found in the local device repository), a "LeafEvent" of type "create" is send to the IoTHub indicating that the device should be created by the [Azure Function](/src/cloud/functions) in Azure. The Identity Translation Module will wait for a Direct Method indicating that the device was created.
- If this is a message for a known device waiting to be created in IoTHub, the message will be cached.
- If this is a message for a known and confirmed device, the message will be sent to IoTHub using the DeviceClient of the leaf device.

On receiving the confirmation of the leaf device being added to IoTHub (by a Direct Method), the Identity Trsansdlation Module will send all cached messages for that device as a batch to IoTHub after which all new messages will be send directly to IoTHub using the created Device Client.

This means that the Identity Translation module has a auto-create functionality for leaf devices build in.

# Security
## Device Identity
The leaf devices are using a symetric key to authenticate with IoTHub. The symetric key that is used for this authentication is calculated from the id of the leaf device and the symetric key of the identity module using the following formula:

LeafDevice<sub>Key</sub> = HMAC_SHA256(key: IdentityTranslationModule<sub>Key</Sub>, tbs: {leafDeviceId})

To calculate the leaf device symetric key, the Identity Translation Module uses the Sign functionality of the [Workflow API](https://github.com/Azure/iotedge/blob/master/edgelet/api/workloadVersion_2019_01_30.yaml) of the [Edge Security deamon](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-security-manager). This can be found in the <code>SignAsync</code> function in the file [Program.cs](\modules\IdentityTranslationLite\Program.cs).
