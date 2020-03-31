# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

import paho.mqtt.client as mqtt
import time
import ssl
import json

class DeviceClient:
    def __init__(
      self, 
      device_name, 
      device_sas_token,
      path_to_root_cert,
      edge_hub_name,
      iot_hub_name
    ):
    
        self.device_name = device_name
        self.device_sas_token = device_sas_token
        self.path_to_root_cert = path_to_root_cert
        self.edge_hub_name = edge_hub_name
        self.iot_hub_name = iot_hub_name

        self.client = None

    def connect(self):
        self.client = mqtt.Client(client_id=self.device_name, protocol=mqtt.MQTTv311)
        self.client.connected_flag=False
        self.client.on_connect = self.__on_connect
        self.client.on_disconnect = self.__on_disconnect
        self.client.on_publish = self.__on_publish
        self.client.on_message = self.__on_message
        
        #...or ?api-version=2018-06-30
        self.client.username_pw_set(
            username=self.iot_hub_name + ".azure-devices.net/" + self.device_name + "/api-version=2016-11-14", 
            password=self.device_sas_token
            )

        self.client.tls_set(
            ca_certs=self.path_to_root_cert, 
            certfile=None, 
            keyfile=None,
            cert_reqs=ssl.CERT_REQUIRED, 
            tls_version=ssl.PROTOCOL_TLSv1_2, 
            ciphers=None
            )

        self.client.tls_insecure_set(False)
        self.client.loop_start()
        self.client.connect(self.edge_hub_name, port=8883)

    def send_message(self, json_obj):
        topic = "devices/{}/messages/events/".format(self.device_name)
        self.client.publish(topic, json.dumps(json_obj), qos=1)

    def __on_connect(self, client, userdata, flags, rc):
        #logging.info("Connected with result code {}".formaty(str(rc)))
        if rc==0:
            client.connected_flag=True #set flag
            print("{} connection: OK with returned code={}".format(self.device_name, rc))

        else:
            client.connected_flag=False #set flag
            print("{} connection: FAILED with returned code={}".format(self.device_name, rc))

        # Subscribing in on_connect() means that if we lose the connection and
        # reconnect then subscriptions will be renewed.
        self.client.subscribe("device/#")

    # The callback for when a PUBLISH message is received from the server.
    def __on_message(self, client, userdata, msg):
        print("Message received: topic={}, payload={}".format(msg.topic, str(msg.payload)))
                
    def __on_disconnect(self, client, userdata,rc=0):
        self.client.connected_flag=False #set flag
        print("Disconnected result code "+str(rc))
        self.client.loop_stop()

    def __on_publish(self, client, userdata, mid):
        print("Device sent message")

class DevicesManager():
    def __init__(
      self, 
      path_to_root_cert,
      edge_hub_name,
      iot_hub_name
    ):
        print("DevicesManager init...")
        print("path_to_root_cert = {}".format(path_to_root_cert))
        print("edge_hub_name = {}".format(edge_hub_name))
        print("iot_hub_name = {}".format(iot_hub_name))

        self.path_to_root_cert = path_to_root_cert
        self.edge_hub_name = edge_hub_name
        self.iot_hub_name = iot_hub_name

        self.devices = []

    #adds a new device:
    # creates a new client and connects it to IoT Device via the edgeHub
    def add(
        self,
        device_name, 
        device_sas_token
        ):

        device = DeviceClient(
            device_name, 
            device_sas_token,
            self.path_to_root_cert,
            self.edge_hub_name,
            self.iot_hub_name
        )
        device.connect()
        
        self.devices.append(device)

        #sends an hello message
        text = "Hello from device {}".format(device_name)
        msg = {
            "content": text
        }
        device.send_message(msg)

    def get(
        self,
        device_name,
        ):

        for device in self.devices:
            if device.device_name == device_name:
                print("[bridge_forward] found matching device {}".format(device_name))
                ret = device
                break
            else:
                print("[bridge_forward] no matching device for {}".format(device_name))
                ret = None

        return ret