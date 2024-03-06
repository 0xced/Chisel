#!/bin/bash -xeo pipefail

# Bought https://thenounproject.com/icon/chisel-1211331/ by Creative Mahira for $2.99

SCRIPT_DIR=$(dirname "$BASH_SOURCE")

rsvg-convert --version > /dev/null 2>/dev/null || (echo "Please install librsvg with \`brew install librsvg\` and try again." && exit 1)

rsvg-convert --width 256 --output "${SCRIPT_DIR}/icon.png" "${SCRIPT_DIR}/icon.svg"
