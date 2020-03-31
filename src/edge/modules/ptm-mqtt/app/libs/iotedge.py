# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

import paho.mqtt.client as mqtt
import time
import ssl
import json
import random
import os
#import logging

import threading
from azure.iot.device import IoTHubModuleClient

#https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-python-python-module-twin-getstarted
def twin_update_listener(client, callback):
    print("Listening to module twin updates...")
    while True:
        patch = client.receive_twin_desired_properties_patch()  # blocking call
        callback(patch)
        

# define behavior for receiving an input message on input1
def input_listener(client, module_input, callback):
    print("Listening to {}...".format(module_input))
    while True:
        input_message = client.receive_message_on_input(module_input)  # blocking call
        callback(input_message)

def init():
    # Inputs/Ouputs are only supported in the context of Azure IoT Edge and module client
    # The module client object acts as an Azure IoT Edge module and interacts with an Azure IoT Edge hub
    module_client = IoTHubModuleClient.create_from_edge_environment()

    # connect the client.
    module_client.connect()

    return module_client

def start_intput_listener(module_client, module_input, on_input):
    # Run INPUT listener thread in the background
    listen_thread = threading.Thread(target=input_listener, args=(module_client, module_input, on_input,))
    listen_thread.daemon = True
    listen_thread.start()

def start_device_twin_listener(module_client, on_device_twin):
    # Run MODULE TWIN listener thread in the background
    twin_update_listener_thread = threading.Thread(target=twin_update_listener, args=(module_client, on_device_twin,))
    twin_update_listener_thread.daemon = True
    twin_update_listener_thread.start()

def input_message_debug_callback(input_message):
    message = input_message.data
    size = len(message)
    message_text = message.decode('utf-8')
    print ( "    Data: <<<%s>>> & Size=%d" % (message_text, size) )
    custom_properties = input_message.custom_properties
    print ( "    Properties: %s" % custom_properties )
    RECEIVED_MESSAGES = 0
    print ( "    Total messages received: %d" % RECEIVED_MESSAGES )
    data = json.loads(message_text)

def device_twin_patch_debug_callback(patch):
    print("")
    print("Twin desired properties patch received:")
    print(patch)