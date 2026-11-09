#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  echo "The iOS bundle must be built on macOS with Xcode." >&2
  exit 1
fi
mode="${1:-simulator}"
project=mobile/SoloStone.iOS/SoloStone.iOS.csproj
case "$mode" in
  simulator)
    if [[ "$(uname -m)" == arm64 ]]; then rid=iossimulator-arm64; else rid=iossimulator-x64; fi
    dotnet build "$project" -c Debug -r "$rid" -p:EnableCodeSigning=false -p:GeneratePackageOnBuild=false
    ;;
  device)
    : "${SOLOSTONE_CODESIGN_KEY:?Set SOLOSTONE_CODESIGN_KEY to your installed signing identity}"
    : "${SOLOSTONE_PROVISION:?Set SOLOSTONE_PROVISION to the installed provisioning profile UUID or name}"
    dotnet publish "$project" -c Release -r ios-arm64 -p:BuildIpa=true -p:GeneratePackageOnBuild=false \
      "-p:CodesignKey=$SOLOSTONE_CODESIGN_KEY" "-p:CodesignProvision=$SOLOSTONE_PROVISION" \
      "-p:ApplicationId=${SOLOSTONE_BUNDLE_ID:-org.solostone.mobile}"
    ;;
  *) echo "Usage: bash mobile/build-ios.sh [simulator|device]" >&2; exit 2 ;;
esac
