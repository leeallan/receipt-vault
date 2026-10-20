#!/bin/zsh
# Record one Maestro flow as footage.
#   record.sh <flow-name> <light|full> [premium] [us]
# Writes ../studio/public/footage/<flow-name>.mp4 (<flow-name>_us.mp4 for the US set).
# Picker positions of the receipt photos are passed in, since the US set sits in other slots.
set -e
SIM=${SIM:-86764DF5-79EF-4020-8D7B-894C581FF504}
HERE=${0:A:h}
NAME=$1; shift
OUT="$HERE/../studio/public/footage/$NAME.mp4"
# The picker shows the newest photos first. Added in README order (UK pair, then US pair):
# freshway_us, brickwell_us, freshway, brickwell — the last on the second row.
PICK=(-e FRESHWAY_POINT="${FRESHWAY_POINT:-83%,25%}" -e BRICKWELL_POINT="${BRICKWELL_POINT:-17%,39%}")
if (( ${@[(Ie)us]} )); then
  OUT="$HERE/../studio/public/footage/${NAME}_us.mp4"
  PICK=(-e FRESHWAY_POINT="${FRESHWAY_POINT_US:-17%,25%}" -e BRICKWELL_POINT="${BRICKWELL_POINT_US:-50%,25%}")
fi
export MAESTRO_CLI_NO_ANALYTICS=1 MAESTRO_CLI_ANALYSIS_NOTIFICATION_DISABLED=true

"$HERE/reset.sh" "$@"
# Maestro's first command after a relaunch can lag; warm it up off-camera.
[[ -f "$HERE/flows/$NAME.pre.yaml" ]] && maestro --device $SIM test "$HERE/flows/$NAME.pre.yaml" >/dev/null

# The battery only redraws when its state changes, so flip it: otherwise the Mac's real
# charging state can show through as a green charging icon.
xcrun simctl status_bar $SIM override --batteryState charged
xcrun simctl status_bar $SIM override --time 9:41 --batteryState discharging --batteryLevel 100 \
  --cellularMode active --cellularBars 4 --wifiBars 3 --dataNetwork wifi
rm -f "$OUT"
xcrun simctl io $SIM recordVideo --codec=h264 --force "$OUT" 2>/dev/null &
REC=$!
sleep 1.5
maestro --device $SIM test $PICK "$HERE/flows/$NAME.yaml" 2>&1 | grep -E "FAILED|Flow .* (passed|failed)|COMPLETED" | tail -3 || true
# Hold the last screen so the final scene has footage to cut from.
sleep 3
kill -INT $REC
wait $REC 2>/dev/null || true
echo "wrote $OUT"
