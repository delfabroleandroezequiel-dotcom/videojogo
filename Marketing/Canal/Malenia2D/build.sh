#!/bin/bash
# Builds Malenia2D_PrimerCorte.mp4 from edl.txt. Run from this folder.
set -e
calc() { awk "BEGIN{printf \"%.3f\", $1}"; }
FF=/c/Users/delfa/Downloads/ffmpeg.exe
FP=/c/Users/delfa/Downloads/ffprobe.exe
O="/c/Users/delfa/Videos/2026-11-16 12-08-09.mp4"
E="/c/Users/delfa/Downloads/Malenia_NoHUD_otjNLTiBa4A.mp4"
ART=../../../assets/audio/Voces/en
SUBFONT="C\:/Windows/Fonts/georgiai.ttf"
BIGFONT="../../Recursos/Fuentes/Anton-Regular.ttf"
mkdir -p tmp
: > tmp/concat.txt
: > tmp/voices.txt
t=0; n=0
while IFS='|' read -r beat src in dur vol cover sub voice voff; do
  [[ -z "$beat" || "$beat" == \#* ]] && continue
  n=$((n+1)); out=$(printf "tmp/s%03d.mp4" $n)
  vf="scale=1920:1080:flags=lanczos,fps=60,setsar=1"
  [[ "$cover" == "1" ]] && vf="$vf,drawbox=x=0:y=852:w=1920:h=228:color=black:t=fill"
  if [[ "$sub" != "-" ]]; then
    if [[ "$src" == "K" || "$src" == "Z" ]]; then
      vf="$vf,drawtext=fontfile=$BIGFONT:textfile=$sub:fontsize=96:fontcolor=white:x=(w-tw)/2:y=(h-th)/2"
    else
      vf="$vf,drawtext=fontfile='$SUBFONT':textfile=$sub:fontsize=42:fontcolor=white:x=(w-tw)/2:y=930:borderw=2:bordercolor=black"
    fi
  fi
  af="volume=$vol,aresample=48000,aformat=channel_layouts=stereo,afade=t=in:d=0.04,afade=t=out:st=$(calc "$dur-0.06"):d=0.06"
  case $src in
    O) "$FF" -v error -y -ss $in -t $dur -i "$O" -vf "$vf" -af "$af" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k "$out" ;;
    E) "$FF" -v error -y -ss $in -t $dur -i "$E" -vf "$vf" -af "$af" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k "$out" ;;
    K) "$FF" -v error -y -f lavfi -i "color=black:s=1920x1080:r=60:d=$dur" -f lavfi -i "anullsrc=r=48000:cl=stereo" -t $dur -vf "$vf" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k "$out" ;;
    Z) "$FF" -v error -y -ss $in -t $dur -i "$O" -f lavfi -i "anullsrc=r=48000:cl=stereo" -map 0:v -map 1:a -t $dur -vf "$vf,boxblur=18:2,eq=brightness=-0.22,drawtext=fontfile=$BIGFONT:textfile=$sub:fontsize=96:fontcolor=white:x=(w-tw)/2:y=(h-th)/2" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k "$out" ;;
  esac
  echo "file '$(basename $out)'" >> tmp/concat.txt
  if [[ "$voice" != "-" ]]; then
    vfile=${voice/ART/$ART}
    echo "$vfile|$(calc "$t+$voff")" >> tmp/voices.txt
  fi
  t=$(calc "$t+$dur")
done < edl.txt
echo "total $t s, $n shots"
(cd tmp && "$FF" -v error -y -f concat -safe 0 -i concat.txt -c copy video.mp4)
# Voices track: every voice delayed to its start.
inputs=(); filt=""; k=0
while IFS='|' read -r f st; do
  inputs+=(-i "$f"); ms=$(awk "BEGIN{printf \"%d\", $st*1000}")
  filt="$filt[$k:a]aresample=48000,aformat=channel_layouts=stereo,adelay=${ms}|${ms}[v$k];"; k=$((k+1))
done < tmp/voices.txt
mix=""; for ((i=0;i<k;i++)); do mix="$mix[v$i]"; done
"$FF" -v error -y "${inputs[@]}" -filter_complex "${filt}${mix}amix=inputs=$k:normalize=0,apad,atrim=0:$t[out]" -map "[out]" -c:a pcm_s16le tmp/voices.wav
# Final mix: see mix.sh
./mix.sh
echo done
