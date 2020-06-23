#!/bin/sh

# Run once, hold otherwise
if [ -f "already_ran" ]; then
    echo "Already ran the Entrypoint once. Holding indefinitely for debugging."
    rm already_ran
    cat
else
    echo "Not creating hold file"
    #touch already_ran
fi

cd /foggycam/src
echo Starting foggycam...
#bash
sudo python3 start.py
