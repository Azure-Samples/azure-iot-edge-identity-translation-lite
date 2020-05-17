# Deploy the Demo

## Which Resources Get Deployed?

The following resources will be created:

- 1 Ubuntu VMs of size _Standard B2ms_ for hosting the IoT Edge Gateway
- 1 x IoT Hub of size _S1 - Standard_
- 1 x Azure Function
- 1 x Azure Storage for dependencies

Once provisioning of resources is complete, the script will configure the IoT Edge gateway on the Ubuntu VM. Finally it deploys the deployment manifest which contains the sample Identity Translation Lite module, a protocol translation module and a mosquito broker.

## Prerequisites

Deployment is done by a __PowerShell script__ which uses the [Az PowerShell module](https://docs.microsoft.com/en-us/powershell/azure/new-azureps-module-az?view=azps-3.8.0) _*Version 3.8.0*_.  
Make sure you have installed this module using [instructions here](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-3.8.0#install-the-azure-powershell-module)

## Run Deployment

```powershell
.\deploy.ps1
```

## Run sample client script

Once the installation is completed successfully, the client test script [sim_clients.py](/src/test/ptm-mqtt/sim_clients.py) can be used on the Edge VM to simulate leaf devices connecting over MQTT to the edge device.

Install Python 3 on the VM:
```
    sudo apt-get update
    sudo apt-get install -y python3-dev
    sudo apt-get install -y libffi-dev
    sudo apt-get install -y libssl-dev
    sudo apt install python3-pip
    sudo pip3 install paho-mqtt

```
Run the Python script

```
python3 sim_clients.py -c 10 -n device -i 1
```

See the [client test script documentation](/docs/sim_clients.md) for a detailed description and examples.