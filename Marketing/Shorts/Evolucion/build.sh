#!/bin/bash
# Short vertical "Day 1 vs Day 152": cada toma en franja 16:9 sobre su propio fondo difuminado,
# contador DAY N arriba, musica de Artemisa y sonido del juego solo en el pago. Uso: bash build.sh
set -e
FF=/c/Users/delfa/Downloads/ffmpeg.exe; FP=/c/Users/delfa/Downloads/ffprobe.exe
SRC=/c/Users/delfa/Videos
MUSIC=/c/Users/delfa/Desktop/Videojogo/assets/audio/Musica/Artemisa/ArtemisaFase1.mp3
MOFF=1.5
cd "$(dirname "$0")/work"; rm -f list.txt clip_*.mp4
n=0; T=0
grep -v '^#' ../edl.txt | while IFS='|' read f s d box sc day vol; do
  [ -z "$f" ] && continue
  n=$((n+1)); out=$(printf "clip_%02d.mp4" $n)
  col=white; [ "$day" = "152" ] && col=0xFFD23C
  sub=""; [ $n -le 2 ] && sub=",drawtext=fontfile=Anton-Regular.ttf:text='MAKING MY METROIDVANIA':x=(w-tw)/2:y=560:fontsize=66:fontcolor=white:borderw=6:bordercolor=black"
  IFS=: read bx by bw bh <<< "$box"
  $FF -v error -y -ss "$s" -t "$d" -i "$SRC/$f" -filter_complex "\
[0:v]crop=$bw:$bh:$bx:$by,split[a][b];\
[a]scale=-2:1920,crop=1080:1920,boxblur=24:2,eq=brightness=-0.18:saturation=1.2[bg];\
[b]scale=1080:608:flags=$sc[fg];\
[bg][fg]overlay=0:656,setsar=1,fps=30,\
drawtext=fontfile=Anton-Regular.ttf:text='DAY $day':x=(w-tw)/2:y=330:fontsize=210:fontcolor=$col:borderw=12:bordercolor=black$sub[v];\
[0:a]volume=$vol,apad,atrim=0:$d,aresample=48000[a2]" \
    -map "[v]" -map "[a2]" -t "$d" -c:v libx264 -preset slow -crf 17 -pix_fmt yuv420p -c:a pcm_s16le "${out%.mp4}.mov" </dev/null 2>&1 | grep -v Fontconfig || true
  echo "file '${out%.mp4}.mov'" >> list.txt
done
$FF -v error -y -f concat -safe 0 -i list.txt -c copy joined.mov
DUR=$($FP -v error -show_entries format=duration -of csv=p=0 joined.mov)
FO=$(awk -v d=$DUR 'BEGIN{print d-0.3}')
$FF -v error -y -i joined.mov -ss $MOFF -i "$MUSIC" -filter_complex "\
[1:a]atrim=0:$DUR,asetpts=PTS-STARTPTS,afade=t=in:d=0.15,volume=0.8[m];\
[0:a][m]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000,apad=whole_dur=$DUR,atrim=0:$DUR,afade=t=out:st=$FO:d=0.3[a]" \
  -map 0:v -map "[a]" -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart -t "$DUR" ../Evolucion_Day1_Day152.mp4
echo "listo: $DUR s"
