#!/usr/bin/env bash
# Regenerates src/D2ST.Protocol/Generated/*.cs from Valve's .proto files.
#
# The ref is pinned to the GameTracking-Dota2 commit for the Dota build this
# server targets (ClientVersion 6783, May 2026), so the generated contracts
# match what the client actually speaks. Pass a different ref to retarget.
#
# Usage: tools/regenerate-protos.sh [git-ref]
# Requires: .NET SDK (for the protogen dotnet tool) and internet access.
set -euo pipefail

REF="${1:-4b28dd7d49f7a1a4b073f3b5dcd22c1ad17423f0}" # build 6783, 2026-05-04
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/src/D2ST.Protocol/Generated"
WORK="$(mktemp -d)"
BASE_URL="https://raw.githubusercontent.com/SteamDatabase/GameTracking-Dota2/$REF/Protobufs"

trap 'rm -rf "$WORK"' EXIT

if ! command -v protogen >/dev/null 2>&1; then
    dotnet tool install --global protobuf-net.Protogen
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

mkdir -p "$WORK/google/protobuf" "$OUT"
# descriptor.proto is not vendored in the Dota tree; Valve's custom options
# (key_field, msgpool_*) extend it, so the contracts need it generated too.
# 3.9.x is the protobuf release current at the pinned Dota build.
DESCRIPTOR_URL="https://raw.githubusercontent.com/protocolbuffers/protobuf/v3.9.1/src/google/protobuf/descriptor.proto"
curl -fsSL "$DESCRIPTOR_URL" -o "$WORK/google/protobuf/descriptor.proto"

protos=()
while read -r line; do
    line="${line%%#*}"
    line="$(echo "$line" | xargs)"
    [ -n "$line" ] && protos+=("$line")
done < "$ROOT/tools/proto-inputs.txt"

for proto in "${protos[@]}"; do
    echo "  fetch $proto"
    curl -fsSL "$BASE_URL/$proto" -o "$WORK/$proto"
done

rm -rf "$OUT"
mkdir -p "$OUT"
cd "$WORK"
protogen --csharp_out="$OUT" -I. google/protobuf/descriptor.proto
for proto in "${protos[@]}"; do
    echo "  protogen $proto"
    protogen --csharp_out="$OUT" -I. "$proto"
done

echo "Generated $(ls "$OUT" | wc -l) file(s) from GameTracking-Dota2@$REF"
