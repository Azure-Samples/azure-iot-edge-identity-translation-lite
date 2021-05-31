#!/bin/sh
# Copyright (c) Microsoft. All rights reserved.
# configedge.sh
# updates connection string in edge config.yaml and restarts edge runtime
# accepts one parameter: a complete connection string
# must be executed with elevated privileges
set -e
logFile=/var/log/azure/configedge.log
configFile=/etc/iotedge/config.yaml

if [ -z "$1" ]
then
    echo "$(date) No connection string supplied. Exiting." >&2
    exit 1
fi

connectionString=$1

# wait to set connection string until config.yaml is available
until [ -f $configFile ]
do
    sleep 5
done

echo "$(date) Setting connection string to $connectionString" >> $logFile
sed -i "s#\(device_connection_string: \).*#\1\"$connectionString\"#g" $configFile
systemctl unmask iotedge
systemctl start iotedge

echo " $(date) Connection string set to $connectionString"