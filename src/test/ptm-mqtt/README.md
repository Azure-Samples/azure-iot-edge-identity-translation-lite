# Overview
"sim_clients.py" is a script to create multiple MQTT clients sending random data.

Usage:
```
python sim_clients.py [-h] -c CLIENTS_NUM -n CLIENTS_ROOT_NAME -i INTERVAL [--broker-ip BROKER_IP] [--broker-port BROKER_PORT]
```

Required arguments:
| Parameter | Description|
|-------------------|--------------------------------------------|
| -c CLIENTS_NUM      | Number of mqtt clients to be created. Default is 20.|
| -n CLIENTS_ROOT_NAME  | client ID is built by appending an incremental number "i" to this root name.|
| -i INTERVAL      | interval (in seconds) at which the client is sending messages. |   

Optional arguments:
| Parameter | Description|
|-------------------|--------------------------------------------|
| --broker-ip BROKER_IP      | ip address of the MQTT broker. Default is 127.0.0.1|
| --broker-port BROKER_PORT  | port of the MQTT broker. Default is 1883|

### Examples:
The following:
```
python sim_clients.py -c 10 -n device -i 0.5
```
will:
* create 10 clients with names "device0", "device1", ..., "device9"
* each client will send a message every 0.5 seconds
* the default broker ip (127.0.0.1) and port (1883) are used



The same but also specifying a different broker:
```
python sim_clients.py -c 10 -n device -i 0.5 --broker-ip 192.168.2.96 --broker-port 1884
```

### Message structure
Each client will send a message with the following structure:
```
{
    "param1": random int the range [0,100],
    "param2": random float
}
```

Here's an example:
```json
{
    "param1": 27, 
    "param2": 0.8243972510356947
}
```

## Pre-requisites
This script requires:
* python 3.x
* paho-mqtt

To install python:
```
***
```

To install the paho mqtt client:
```
pip install paho-mqtt
```