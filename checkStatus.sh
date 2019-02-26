#!/bin/sh
cd /share/CACHEDEV1_DATA/Recordings/capture/4c0468d351074403b6093783423628b0 || exit
mkdir -p ./service_test
FILE="./service_test/error.txt"

LAST_IMG=`ls ./images/ -rt | grep [^_] | tail -n1`
# LAST_IMG=`find ./images -type f -print | sort -n | tail -1 | cut -f2- -d" "`
OLD=$(stat -c %Z ./images/"$LAST_IMG") || exit
NOW=$(date +%s)
DIFF=$((NOW-OLD))
echo $NOW - $OLD
if [ "$DIFF" -lt "500" ]
then
	echo "We are ok ($DIFF)"
	if [ -f "$FILE" ]
	then
		rm "$FILE"
	fi
else
	echo "alert ($DIFF)"
	if [ ! -f "$FILE" ]
	then
		touch $FILE
		docker restart 41c385f6405d
    else
		rm $FILE
		touch $FILE
		docker restart 41c385f6405d
	fi
fi

# Reboot garage camera at midnight daily
currenttime=$(date +%H:%M)
   if [[ "$currenttime" > "00:00" ]] && [[ "$currenttime" < "00:45" ]]
   then
        wget 192.168.11.154:32781/cgi-bin/CGIProxy.fcgi?cmd=rebootSystem&usr=jasonsf&pwd=PmH8ddek6QKO 2>/dev/null
   fi