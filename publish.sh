#!/bin/bash
# Builds and packages the ODC External Library for upload to ODC Portal.
# Run: chmod +x publish.sh && ./publish.sh

set -e

OUT_DIR="./publish-output"
ZIP_NAME="AzureAISearchTutorialCompanion.zip"

echo "Cleaning previous output..."
rm -rf "$OUT_DIR" "$ZIP_NAME"

echo "Publishing (Release, self-contained)..."
dotnet publish \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output "$OUT_DIR"

echo "Creating zip for ODC upload..."
cd "$OUT_DIR"
zip -r "../$ZIP_NAME" .
cd ..

echo ""
echo "Done! Upload '$ZIP_NAME' to ODC Portal → External Logic → Upload."
