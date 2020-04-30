---
page_type: sample
languages:
- csharp
- python
products:
- dotnet
- azure-iot
- azure-iot-edge
- azure-iot-hub
- azure-functions
- vs-code
---

# Azure IoT Edge Identity Translation Lite: Sample on implementing Identity Translation at the edge

<!-- 
Guidelines on README format: https://review.docs.microsoft.com/help/onboard/admin/samples/concepts/readme-template?branch=master

Guidance on onboarding samples to docs.microsoft.com/samples: https://review.docs.microsoft.com/help/onboard/admin/samples/process/onboarding?branch=master

Taxonomies for products and languages: https://review.docs.microsoft.com/new-hope/information-architecture/metadata/taxonomies?branch=master
-->

This sample builds the required components to support the Idenity Translation pattern with Azure IoT Edge. The [Identity Translation Pattern](https://docs.microsoft.com/bs-latn-ba/azure/iot-edge/iot-edge-as-gateway#patterns) is a pattern by which you have to implement both protocol translation as well as having the devices behind the protocol adopt an identity in IoT Hub. However, the devices themselves don't talk directly to IoT Hub, but rather get their identities impersonated by a custom module in IoT Edge.

This sample is a 'lite' implementation of the Identity Translation pattern as it only supports the basic identity translation flows based on symmetric keys. 

## Prerequisites

- An Azure account and access to a subscription where you can provision new resources (IoT Hub, Functions, Storage, Container Registry).
- Visual Studio Code if you want to run any parts of the sample locally.
- Docker Desktop for building the container modules (if not using the pre-built images).

## Setup

Explain how to prepare the sample once the user clones or downloads the repository. The section should outline every step necessary to install dependencies and set up any settings (for example, API keys and output folders).

Automated sample setup can be found [here](/ITM-ARM/README.md).

There is also an option to setting up everything manually, including building the container images. You can find a [step-by-step guide here](/docs/stepbystep.md).


## Running the sample

Outline step-by-step instructions to execute the sample and see its output. Include steps for executing the sample from the IDE, starting specific services in the Azure portal or anything related to the overall launch of the code.

## Key concepts

For this Identity Translation Lite sample we are relying on a protocol translation module which is to be seen as something you can replace with your own module. This protocol translation module generates messages into edgeHub with certain headers. These messages can then be picked up by the second module: the [Identity Translation module](/docs/identitytranslationmodule.md).

The solution also comprises of a cloud section that takes care of provisioning the device on the IoT Hub and assigning it as a child of the IoT Edge device. To prevent any type of child device to be provisioned, there is a whitelisting file that validates whether the child device can be provisioned. This cloud solution leverages an [Azure Function](docs/functions.md) that gets triggered by an Event Grid subscription.

![Registration flow diagram](docs/media/registrationflow.png)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
