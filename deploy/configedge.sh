#!/bin/sh
# Copyright (c) Microsoft. All rights reserved.
# configedge.sh
# updates connection string in edge config.toml and restarts edge runtime
# accepts one parameter: a complete connection string
# must be executed with elevated privileges
set -e
logFile=/var/log/azure/configedge.log
configFile=/etc/aziot/config.toml

if [ -z "$1" ]
then
    echo "$(date) No connection string supplied. Exiting." >&2
    exit 1
fi

connectionString=$1

# wait to set connection string until config.toml is available
until [ -f $configFile ]
do
    sleep 5
done

echo "$(date) Setting connection string to $connectionString" >> $logFile
sudo sed -i -z "s|## Manual provisioning with connection string\(.*\)## Manual provisioning with symmetric key|## Manual provisioning with connection string\n[provisioning]\nsource = \"manual\"\nconnection_string = \"$connectionString\"\n\n## Manual provisioning with symmetric key|" $configFile

iotedge config apply
iotedge system restart

echo " $(date) Connection string set to $connectionString"