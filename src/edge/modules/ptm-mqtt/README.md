# Introduction 
This project contains an identity translation edge module for Azure IoT Edge to connect leaf devices with their own identity to IoTHub via the an edge device. This identity translation module is based on the IdentityTranslationLite design.

# Getting Started
TODO: Guide users through getting your code up and running on their own system. In this section you can talk about:
1.	Installation process
2.	Software dependencies
3.	Latest releases
4.	API references

# Connecting Leaf Devices
Messages from leaf devices are identified by the "leafDeviceId" property.

# Security
## Device Identity
The leaf devices are using a symetric key to authenticate with IoTHub. The symetric key that is used for this authentication is calculated from the id of the lead device and the symetric key of the identity module using the following formula:

LeafDevice<sub>Key</sub> = HMAC_SHA256(key: IdentityTranslationModule<sub>Key</Sub>, tbs: {leafDeviceId})

To calculate the leaf device symetric key, the Identity Translation Module uses the Sign functionality of the [Workflow API](https://github.com/Azure/iotedge/blob/master/edgelet/api/workloadVersion_2019_01_30.yaml) of the [Edge Security deamon](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-security-manager). This can be found in the <code>SignAsync</code> function in the file [Program.cs](\modules\IdentityTranslationLite\Program.cs).

# Build and Test
TODO: Describe and show how to build your code and run the tests. 

# Contribute
TODO: Explain how other users and developers can contribute to make your code better. 

If you want to learn more about creating good readme files then refer the following [guidelines](https://docs.microsoft.com/en-us/azure/devops/repos/git/create-a-readme?view=azure-devops). You can also seek inspiration from the below readme files:
- [ASP.NET Core](https://github.com/aspnet/Home)
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [Chakra Core](https://github.com/Microsoft/ChakraCore)