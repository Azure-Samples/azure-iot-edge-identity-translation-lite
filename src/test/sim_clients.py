import paho.mqtt.client as mqtt
import time
import random
import json

import argparse

#import logging

#broker
itm_broker_address = "127.0.0.1"
itm_broker_port = 1883
itm_broker_client_id = "itm-bridge"   #this client ID

#clients
clients=[]

def parse_cmdline_args():
    parser = argparse.ArgumentParser()
    
    parser.add_argument(
        '-c', 
        '--num_of_clients',
        default=20,
        dest='clients_num',
        required=True,
        help='Number of mqtt clients to be created'
    )

    parser.add_argument(
        '-n', 
        '--clients_root_name',
        default='client',
        dest='clients_root_name',
        required=True,
        help='root to be used to create client name'
    )

    parser.add_argument(
        '-p', 
        '--period',
        default=0.5,
        dest='period',
        required=True,
        help='period (seconds, float) of message transmission'
    )
    
    return parser.parse_args()

# The callback for when the client receives a CONNACK response from the server.
def on_connect(client, userdata, flags, rc):
    #logging.info("Connected with result code {}".formaty(str(rc)))
    if rc==0:
        print("connected OK Returned code=",rc)
    else:
        print("Bad connection Returned code=",rc)

# The callback for when a PUBLISH message is received from the server.
def on_message(client, userdata, msg):
    print(msg.topic+" "+str(msg.payload))

def on_disconnect(client, userdata,rc=0):
    print("DisConnected result code "+str(rc))
    client.loop_stop()


parser = parse_cmdline_args()
clients_num = int(parser.clients_num)
period = float(parser.period)
root_name = parser.clients_root_name


#creates a clients
#create clients
for i in range(clients_num):
    client_name = root_name + str(i)
    print("creating client with client_id = {}".format(client_name))
    client = mqtt.Client(client_name)
    client.client_name = client_name
    clients.append(client)

for client in clients:
    client.on_connect = on_connect
    client.on_message = on_message
    #client.on_disconnect = on_disconnect
    client.connect(itm_broker_address, itm_broker_port, 60)
    client.loop_start() #starts the client

while True:
    time.sleep(period)
    r = random.randint(0,clients_num-1)

    client = clients[r]
    topic = "device/{}/message".format(client.client_name)
    payload = {
        "param1": random.randrange(0,100),
        "param2": random.random()
    }
    client.publish(topic, json.dumps(payload))
    print("sending topic={},payload={}", topic, json.dumps(payload))
