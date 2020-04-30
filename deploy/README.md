# Deploy the Demo

## Which Resources Get Deployed?

The following resources will be created:

- 1 Ubuntu VMs of size _Standard B2ms_
- 1 x IoT Hub of size _F1 - Free_
- 1 x Azure Function
- __TODO ADD MORE?__

## Prerequisites

Deployment is done by a __PowerShell script__ which uses the [Az PowerShell module](https://docs.microsoft.com/en-us/powershell/azure/new-azureps-module-az?view=azps-3.7.0) _*Version 3.7.0*_.  
Make sure you have installed this module using [instructions here](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-3.7.0#install-the-azure-powershell-module)

## Run Deployment

```powershell
.\deploy.ps1
```