#!/bin/zsh
# Put the simulator app into a known demo state.
#   reset.sh <light|full> [premium] [us]
set -e
SIM=${SIM:-86764DF5-79EF-4020-8D7B-894C581FF504}   # iPhone 17 Pro Max
BUNDLE=com.farabove.receiptvault
HERE=${0:A:h}

xcrun simctl terminate $SIM $BUNDLE 2>/dev/null || true
# Opening an export in Preview leaves a "◀ Preview" back link in the status bar.
xcrun simctl terminate $SIM com.apple.Preview 2>/dev/null || true
DATA=$(xcrun simctl get_app_container $SIM $BUNDLE data)
TIER=0; REGION=uk; CURRENCY=GBP
for arg in "${@:2}"; do
  [[ $arg == premium ]] && TIER=1
  [[ $arg == us ]] && REGION=us && CURRENCY=USD
done
python3 -I "$HERE/seed.py" "$DATA" "$1" $REGION
xcrun simctl spawn $SIM defaults write $BUNDLE active_tier -int $TIER
xcrun simctl spawn $SIM defaults write $BUNDLE default_currency -string $CURRENCY
# Saving on camera would otherwise trigger the App Store rating prompt.
xcrun simctl spawn $SIM defaults write $BUNDLE review_last_prompt_version -string "$(
  /usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' "$(xcrun simctl get_app_container $SIM $BUNDLE app)/Info.plist")"

# The battery only redraws when its state changes, so flip it: otherwise the Mac's real
# charging state can show through as a green charging icon.
xcrun simctl status_bar $SIM override --batteryState charged
xcrun simctl status_bar $SIM override --time 9:41 --batteryState discharging --batteryLevel 100 \
  --cellularMode active --cellularBars 4 --wifiBars 3 --dataNetwork wifi
xcrun simctl launch $SIM $BUNDLE >/dev/null
sleep 5
