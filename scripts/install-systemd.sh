#!/usr/bin/env bash
set -euo pipefail

APP_DIR=/opt/memmon
SERVICE=/etc/systemd/system/memorymonitor.service

# Create user
id -u memmon >/dev/null 2>&1 || sudo useradd -r -s /usr/sbin/nologin memmon

# Publish
dotnet publish ./src/MemoryMonitor.csproj -c Release -r linux-x64 -o /tmp/memmon-pub --no-self-contained

sudo mkdir -p "$APP_DIR"
sudo cp -r /tmp/memmon-pub/* "$APP_DIR"/
sudo chown -R memmon:memmon "$APP_DIR"

# Install service
sudo mkdir -p /etc/systemd/system
sudo cp ./deploy/systemd/memorymonitor.service "$SERVICE"

sudo systemctl daemon-reload
sudo systemctl enable memorymonitor
sudo systemctl restart memorymonitor
systemctl status memorymonitor --no-pager
