#!/bin/zsh
# Record one Maestro flow as footage.
#   record.sh <flow-name> <light|full> [premium]
# Writes ../studio/public/footage/<flow-name>.mp4
set -e
SIM=${SIM:-86764DF5-79EF-4020-8D7B-894C581FF504}
HERE=${0:A:h}
NAME=$1; shift
OUT="$HERE/../studio/public/footage/$NAME.mp4"
export MAESTRO_CLI_NO_ANALYTICS=1 MAESTRO_CLI_ANALYSIS_NOTIFICATION_DISABLED=true

"$HERE/reset.sh" "$@"
# Maestro's first command after a relaunch can lag; warm it up off-camera.
[[ -f "$HERE/flows/$NAME.pre.yaml" ]] && maestro --device $SIM test "$HERE/flows/$NAME.pre.yaml" >/dev/null

xcrun simctl status_bar $SIM override --time 9:41 --batteryState discharging --batteryLevel 100 \
  --cellularMode active --cellularBars 4 --wifiBars 3 --dataNetwork wifi
rm -f "$OUT"
xcrun simctl io $SIM recordVideo --codec=h264 --force "$OUT" 2>/dev/null &
REC=$!
sleep 1.5
maestro --device $SIM test "$HERE/flows/$NAME.yaml" 2>&1 | grep -E "FAILED|Flow .* (passed|failed)|COMPLETED" | tail -3 || true
sleep 1
kill -INT $REC
wait $REC 2>/dev/null || true
echo "wrote $OUT"
