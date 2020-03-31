# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

import paho.mqtt.client as mqtt
import ssl
import json
#import logging

class BrokerClient():
    def __init__(
      self, 
      broker_address, 
      broker_port,
      client_id,
      callback
    ):
    
        self.broker_address = broker_address
        self.broker_port = broker_port
        self.client_id = client_id
        self.callback = callback

        self.client = None

    def connect(self):
        self.client = mqtt.Client(self.client_id)
        self.client.connected_flag=False
        self.client.on_connect = self.__on_connect
        self.client.on_message = self.__on_message
        self.client.on_disconnect = self.__on_disconnect
        self.client.on_publish = self.__on_publish
        self.client.loop_start() #starts the client
        self.client.connect(self.broker_address, self.broker_port, 60)

    def __on_connect(self, client, userdata, flags, rc):
        if rc==0:
            self.client.connected_flag=True #set flag
            print("itm-bridge connection to broker: OK with returned code={}".format(rc))

        else:
            self.client.connected_flag=False #set flag
            print("itm-bridge connection to broker: FAILED with returned code={}".format(rc))
        
        # Subscribing in on_connect() means that if we lose the connection and
        # reconnect then subscriptions will be renewed.
        self.client.subscribe("device/#")

    def __on_message(self, client, userdata, msg):
        print("Message received: topic={}, payload={}".format(msg.topic, str(msg.payload)))
        self.callback(msg)
        
    def __on_disconnect(self, client, userdata,rc=0):
        self.client.connected_flag=False #set flag
        print("Disconnected result code "+str(rc))
        self.client.loop_stop()

    def __on_publish(self, client, userdata, mid):
        print("Device sent message")