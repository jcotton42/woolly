#!/bin/bash

BIN_DIR="/usr/local/sbin/"
CONFIG_DIR="/usr/local/etc/woolly"
SYSTEMD_DIR="/usr/local/lib/systemd/system/"

service_exists() {
    local name=$1
    if systemctl list-units --full --all | grep -Fq "$name.service"; then
        return 0
    else
        return 1
    fi
}

restart_service=n

if service_exists woolly; then
    systemctl stop woolly.service
    restart_service=y
fi

install -m755 woolly "$BIN_DIR"
install -d -m755 "$SYSTEMD_DIR"
install -d -m755 "$CONFIG_DIR"

if [[ ! -e "$CONFIG_DIR/appsettings.json" ]]; then
    install -m644 appsettings.json "$CONFIG_DIR"
fi
if [[ ! -e "$CONFIG_DIR/secrets.conf" ]]; then
    install -m600 secrets.conf "$CONFIG_DIR"
fi

install -m644 woolly.service "$SYSTEMD_DIR"

systemctl daemon-reload
systemctl enable woolly.service

if [[ "$restart_service" == "y" ]]; then
    # assume it's already configured
    systemctl start woolly.service
    echo "Woolly updated and restarted"
else
    echo "Woolly installed"
    echo "Edit appsettings.json and secrets.conf in $CONFIG_DIR accordingly, then start woolly"
fi
