# Introduction 
This project contains some guidance and the source code for the cloud function side of managing leaf devices for identity translation concept with IoT Edge.

## Setting up the cloud function processing
- Create Event Grid subscription for telemetry messages and filter the route on Query: `messageType = 'LeafEvent'`
- Azure Function to process the LeafEvent message

Schema for the LeafEvent event:
```
Header:
    message.Properties.Add("messageType", "LeafEvent");

Payload sample:
{
    "hubHostname": "myhub",
    "leafDeviceId":"myleafDevice3",
    "edgeDeviceId":"simulateEdgeItm",
    "edgeModuleId":"itm-bridge",
    "operation":"create"
}
```

### Running locally

To run the function locally you will need Visual Studio Code with the Azure Functions tools.

1. Please update or update a file in this folder named `local.settings.json` and add content as follows:

```
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "[YOURSTORAGECONNECTION_STRING]",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "IoTHubConnectionString" : "YOURHUBCONNECTION_STRING(owner)",
        "WhitelistStorageConnection": "YOURSTORAGECONNECTION_STRING",
        "WhitelistContainerName": "whitelist",
        "WhitelistFilename": "whitelistitm.txt"
    }
}
```
2. There is a sample of the whitelist devices list format, simply add one line per device name and upload to your storage account.
3. For testing you can use the device simulator in folder 'simulator'. In terminal, browse to the folder, then run `dotnet run "YOURDEVICECONNECTIONSTRING"`, but first change the code of the payloed in the method `SendDeviceToCloudMessagesAsync`.

## Setting up the function to process Event Grid messages
- Deploy the function in the folder /functions
- Update the Application Properties (Environment variables) in the Function (app) settings
- Create the Event Grid subscription in IoT Hub
    - New subscription > Azure function and connect to your Azure function you deployed
- Update the automatically created route:
    - Go to IoT Hub - Endpoint routing > query `itmtype = 'LeafEvent'`.

## Resources for running Event Grid webhooks and Functions

https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-event-grid (in this document you will also find how to debug Event Grid locally with a sandbox and Postman)
https://docs.microsoft.com/bs-latn-ba/azure/event-grid/event-schema-iot-hub

## About Event Grid with IoT Hub Telemetry messages
A few things to know:
- When you create an event grid subscription for the IoT Hub, there will a new route automatically created. This wwill be named 'RouteToEventGrid'. This cannot be create manually. You cannot edit it other than adding a query to filter the telemetry messages going to the Event Grid.
- The Route's query will apply to all messages. There is another way of filtering, this is by using the properties or the body of the telemetry message in the Event Grid Subscription filter itself.
- Event schema used is described here: https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-event-grid
- To receive the event body as JSON your telemetry message needs both Content-Type set to `'application/json'`, and it's Encoding to `'UTF-8'`. If these are not set the body will alwasy be Base64 encoded.
- Your webhook Function must return 

At the time of writing, pricing for ingress and egress to Event Grid is charged by million messages. For this reason it is useful using filter queries in the IoT Hub Route, rather than sending everything to Event Grid and then filtering at the subscription level.

