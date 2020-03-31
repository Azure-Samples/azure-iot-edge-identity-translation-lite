# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

import time
import ssl
import json
import random
#import logging

import libs.iotedge as iotedge
import libs.utils as utils
import libs.broker as broker_client

from azure.iot.device import Message

import os

global module_client

global module_id
module_id = os.environ["IOTEDGE_MODULEID"]

# broker's details
itm_broker_address = "127.0.0.1"
itm_broker_port = 1883
itm_broker_client_id = module_id + "_client"

#ptm configuration
global ptm_output_name
ptm_output_name = "ptm_output" #will be "ptmoutput"

def ptm_forward(device_id, json_obj):
    global ptm_output_name
    global module_client
    
    #PTM: ptmoutput  
    #Contract: 
    #    Properties: leafdeviceid 
    #    moduleid 
    #    Payload: agnostic 

    msg = Message(json.dumps(json_obj))
    #msg.message_id = uuid.uuid4()
    msg.custom_properties["leafdeviceid"] = device_id
    msg.custom_properties["moduleid"] = module_id

    module_client.send_message_to_output(msg, ptm_output_name)

#bridge's LOGIC
# -listens to messages with topic "device/{device_id}/message" from the broker
# -forward the message to the corresponding {device_id} IoT Device if found
def ptm_logic(msg):
    #extracts device_id (expects a topic like: device/{device_id}/message)
    device_id = msg.topic.split("/")[1]
    print("Device ID: {}".format(device_id))

    #sending to output
    ptm_output_obj = {
        "topic": msg.topic,
        "payload": json.loads(msg.payload.decode('utf-8'))
    }
    ptm_forward(device_id, ptm_output_obj)


#----------------------------------------------
#connects to iot edge and start listeners
module_client = iotedge.init()
iotedge.start_device_twin_listener(module_client, iotedge.device_twin_patch_debug_callback)
#iotedge.start_intput_listener(module_client, "input1", iotedge.input_message_debug_callback)
#module_client.send_message_to_output("Hello", ptm_output_name)

#----------------------------------------------
#creates a client and connects it to the broker
broker_client = broker_client.BrokerClient(
    itm_broker_address, 
    itm_broker_port, 
    itm_broker_client_id,
    ptm_logic)

#connects to the MQTT broker
while True:
    try:
        broker_client.connect()
    except:
        print("ERROR, not connected to the MQTT broker. Waits for 1 second before re-trying")
        time.sleep(1)
        continue
    else:
        print("OK, connected to the MQTT broker.")
        break

while True:
    time.sleep(10)

