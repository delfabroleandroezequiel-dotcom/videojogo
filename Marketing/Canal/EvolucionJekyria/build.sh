#!/bin/bash
# Evolucion de Jekyria: recorta la ventana del juego de cada grabacion (768x432), la escala a 1080p
# con pixeles nitidos, le pone el dia y la fecha, une todo y suma la musica.
# Uso: bash build.sh   (retocar edl.txt y volver a correr)
set -e
FF=/c/Users/delfa/Downloads/ffmpeg.exe
SRC=/c/Users/delfa/Videos
MUSIC=/c/Users/delfa/Desktop/Videojogo/assets/audio/Musica/Arthur/EntrenamientoCalentamiento.mp3
cd "$(dirname "$0")/work"
rm -f list.txt
grep -v '^#' ../edl.txt | while IFS='|' read id f s d y day date; do
  [ -z "$id" ] && continue
  $FF -v error -y -ss "$s" -t "$d" -i "$SRC/$f" -an -vf "crop=768:432:256:$y,scale=1920:1080:flags=neighbor,setsar=1,fps=30,\
drawtext=fontfile=Anton-Regular.ttf:text='$day':x=70:y=h-230:fontsize=120:fontcolor=white:borderw=8:bordercolor=black,\
drawtext=fontfile=Anton-Regular.ttf:text='$date':x=74:y=h-100:fontsize=46:fontcolor=0xFFD23C:borderw=5:bordercolor=black" \
    -c:v libx264 -preset medium -crf 16 -pix_fmt yuv420p "clip_$id.mp4" </dev/null 2>&1 | grep -v Fontconfig || true
  echo "file 'clip_$id.mp4'" >> list.txt
done
$FF -v error -y -f concat -safe 0 -i list.txt -c copy video.mp4
DUR=$(/c/Users/delfa/Downloads/ffprobe.exe -v error -show_entries format=duration -of csv=p=0 video.mp4)
FO=$(awk -v d=$DUR 'BEGIN{print d-1.5}')
$FF -v error -y -i video.mp4 -i "$MUSIC" -filter_complex "[1:a]atrim=0:$DUR,afade=t=in:d=0.5,afade=t=out:st=$FO:d=1.5,loudnorm=I=-14:TP=-1:LRA=11,aresample=48000[a]" \
  -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 192k -movflags +faststart -t "$DUR" ../EvolucionJekyria_corte1.mp4
echo "listo: $DUR s"
