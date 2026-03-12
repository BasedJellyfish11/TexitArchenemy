#!/bin/bash
set -e

HETZNER_IP="204.168.147.227"
HETZNER_USER="root"
DEPLOY_PATH="/root/archenemy-bot/publish"
SERVICE_NAME="archenemy-bot"
PROJECT_PATH="TexitArchenemy/TexitArchenemy.csproj"
BUILD_OUTPUT="TexitArchenemy/bin/Release/net10.0/linux-x64/publish"

echo "==> Building..."
dotnet publish $PROJECT_PATH -c Release -r linux-x64 --self-contained true

echo "==> Stopping service..."
ssh $HETZNER_USER@$HETZNER_IP "systemctl stop $SERVICE_NAME"

echo "==> Uploading..."
rsync -av $BUILD_OUTPUT/ $HETZNER_USER@$HETZNER_IP:$DEPLOY_PATH/

echo "==> Uploading assets..."
rsync -av Skeletons/ $HETZNER_USER@$HETZNER_IP:$DEPLOY_PATH/Skeletons/
rsync -av Venom/ $HETZNER_USER@$HETZNER_IP:$DEPLOY_PATH/Venom/

echo "==> Restarting service..."
ssh $HETZNER_USER@$HETZNER_IP "chmod +x $DEPLOY_PATH/TexitArchenemy && systemctl start $SERVICE_NAME"

echo "==> Done! Tailing logs (Ctrl+C to exit)..."
ssh $HETZNER_USER@$HETZNER_IP "journalctl -u $SERVICE_NAME -f"