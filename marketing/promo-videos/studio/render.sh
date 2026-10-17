#!/bin/zsh
# Render every composition to ../out. App Store previews get a silent stereo AAC track,
# which App Store Connect expects on app previews.
set -e
cd ${0:A:h}
mkdir -p ../out/store ../out/social
for id in $(npx remotion compositions --quiet); do
  case $id in
    Store*) dir=store ;;
    *) dir=social ;;
  esac
  extra=()
  [[ $id == *-AppStore ]] && extra=(--enforce-audio-track --audio-codec=aac)
  npx remotion render $id ../out/$dir/receiptvault-$id.mp4 --codec=h264 --crf=18 --log=error $extra
  echo "rendered $id"
done
