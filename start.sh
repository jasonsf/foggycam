#!/bin/sh

# Run once, hold otherwise
if [ -f "already_ran" ]; then
    echo "Already ran the Entrypoint once. Holding indefinitely for debugging."
    rm already_ran
    cat
else
    touch already_ran
fi

cd /foggycam/src
echo Starting foggycam...
#bash
sudo python3 start.py
