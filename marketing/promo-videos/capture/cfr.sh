#!/bin/zsh
# Convert recordings to constant 30fps for Remotion and hold the last frame for 3s.
#   cfr.sh <name>...   (moves ../studio/public/footage/<name>.mp4 to raw/, writes the CFR copy back)
# The simulator only writes a frame when the screen changes, so a recording that ends on a
# still screen stops at its last change; the hold gives the final scene footage to cut from.
# Remotion's bundled ffmpeg has no tpad/concat filters, so the hold is joined with the
# concat demuxer.
set -e
HERE=${0:A:h}
FOOT="$HERE/../studio/public/footage"
TMP=$(mktemp -d)
cd "$HERE/../studio"
for NAME in "$@"; do
  [[ -f "$FOOT/$NAME.mp4" ]] && mv "$FOOT/$NAME.mp4" "$HERE/raw/$NAME.mp4"
  npx remotion ffmpeg -loglevel error -y -i "$HERE/raw/$NAME.mp4" -r 30 -fps_mode cfr \
    -c:v libx264 -crf 18 -pix_fmt yuv420p -an "$TMP/main.mp4"
  npx remotion ffmpeg -loglevel error -y -sseof -0.1 -i "$TMP/main.mp4" -frames:v 1 "$TMP/last.png"
  npx remotion ffmpeg -loglevel error -y -loop 1 -framerate 30 -t 3 -i "$TMP/last.png" -r 30 \
    -c:v libx264 -crf 18 -pix_fmt yuv420p "$TMP/hold.mp4"
  printf "file '%s'\nfile '%s'\n" "$TMP/main.mp4" "$TMP/hold.mp4" > "$TMP/list.txt"
  npx remotion ffmpeg -loglevel error -y -f concat -safe 0 -i "$TMP/list.txt" -c copy "$FOOT/$NAME.mp4"
  echo "converted $NAME"
done
