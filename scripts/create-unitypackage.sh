#!/bin/bash
# Creates a .unitypackage file from a folder without Unity Editor
# Usage: ./create-unitypackage.sh <source-folder> <output-file>

set -e

SOURCE_FOLDER="$1"
OUTPUT_FILE="$2"

if [ -z "$SOURCE_FOLDER" ] || [ -z "$OUTPUT_FILE" ]; then
    echo "Usage: $0 <source-folder> <output-file>"
    exit 1
fi

if [ ! -d "$SOURCE_FOLDER" ]; then
    echo "Error: Source folder not found: $SOURCE_FOLDER"
    exit 1
fi

# Create temp directory
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

echo "Creating unitypackage from: $SOURCE_FOLDER"
echo "Output: $OUTPUT_FILE"

# Find all files (not directories) and their .meta files
find "$SOURCE_FOLDER" -type f ! -name "*.meta" | while read -r file; do
    META_FILE="${file}.meta"

    if [ ! -f "$META_FILE" ]; then
        echo "Warning: No .meta file for $file, skipping..."
        continue
    fi

    # Extract GUID from .meta file
    GUID=$(grep "^guid:" "$META_FILE" | sed 's/guid: //')

    if [ -z "$GUID" ]; then
        echo "Warning: No GUID found in $META_FILE, skipping..."
        continue
    fi

    # Create GUID folder
    GUID_DIR="$TEMP_DIR/$GUID"
    mkdir -p "$GUID_DIR"

    # Copy asset file
    cp "$file" "$GUID_DIR/asset"

    # Copy meta file content
    cp "$META_FILE" "$GUID_DIR/asset.meta"

    # Create pathname file (relative path from Assets)
    echo "$file" > "$GUID_DIR/pathname"

    echo "Added: $file (GUID: $GUID)"
done

# Also include .meta files for folders
find "$SOURCE_FOLDER" -type d | while read -r dir; do
    META_FILE="${dir}.meta"

    if [ -f "$META_FILE" ]; then
        GUID=$(grep "^guid:" "$META_FILE" | sed 's/guid: //')

        if [ -n "$GUID" ]; then
            GUID_DIR="$TEMP_DIR/$GUID"
            mkdir -p "$GUID_DIR"

            cp "$META_FILE" "$GUID_DIR/asset.meta"
            echo "$dir" > "$GUID_DIR/pathname"

            echo "Added folder: $dir (GUID: $GUID)"
        fi
    fi
done

# Create tar.gz (unitypackage format)
cd "$TEMP_DIR"
tar -czf "$OLDPWD/$OUTPUT_FILE" ./*

echo ""
echo "Successfully created: $OUTPUT_FILE"
