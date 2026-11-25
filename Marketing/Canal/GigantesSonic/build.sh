#!/bin/bash
# Arma Gigantes_PrimerCorte.mp4 desde plan.txt + Voz/tiempos.txt. Correr desde esta carpeta.
set -e
FF=/c/Users/delfa/Downloads/ffmpeg.exe
FP=/c/Users/delfa/Downloads/ffprobe.exe
FONT="../../Recursos/Fuentes/Anton-Regular.ttf"
DL=C:/Users/delfa/Downloads
declare -A MUS=([Y]="C:/Users/delfa/Downloads/SonicOST/S1_SpringYard.mp3" [E]="C:/Users/delfa/Downloads/SonicOST/S2_EmeraldHill.mp3" [R]="C:/Users/delfa/Downloads/SonicOST/S1_Boss.mp3" [B]="C:/Users/delfa/Downloads/SonicOST/S2_Boss.mp3" [D]="C:/Users/delfa/Downloads/SonicOST/SK_Doomsday.mp3" [F]="C:/Users/delfa/Downloads/SonicOST/S3_FinalBoss.mp3" [M]="C:/Users/delfa/Downloads/SonicOST/ImHere_Instrumental [jeFjj2b9TAU].mp3")   # temas originales, bajos y en escalada de ritmo
calc() { awk "BEGIN{printf \"%.3f\", $1}"; }
ENC="-c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p -r 60 -c:a aac -b:a 192k -ar 48000 -ac 2"
mkdir -p tmp; rm -f tmp/*.mp4 tmp/*.txt tmp/*.wav
: > tmp/concat.txt; : > tmp/voices.txt; : > tmp/music.txt; : > tmp/mapa.txt

# Duración y comienzo de cada párrafo de voz.
declare -A PST PDU PEND
while IFS='|' read -r p k st rest; do
  st=${st//$'\r'/}
  if [[ "$k" == "end" ]]; then PEND[$p]=$st; continue; fi
  PST["$p:$k"]=$st
done < <(sed 's/^\xEF\xBB\xBF//' Voz/tiempos.txt)
for key in "${!PST[@]}"; do
  p=${key%%:*}; k=${key##*:}; nk=$((k+1))
  if [[ -n "${PST[$p:$nk]}" ]]; then PDU[$key]=$(calc "${PST[$p:$nk]}-${PST[$key]}"); else PDU[$key]=$(calc "${PEND[$p]}-${PST[$key]}"); fi
done

src_file() { if [[ -f "fuentes/$1.mp4" ]]; then echo "fuentes/$1.mp4"; else echo "clips/$1.mp4"; fi; }
has_audio() { [[ -n "$("$FP" -v error -select_streams a -show_entries stream=index -of csv=p=0 "$1")" ]]; }

n=0; t=0
shot() { # $1=src spec  $2=dur  $3=card file or -  $4=card style (top|center)
  local spec=$1 dur=$2 card=$3 style=$4; n=$((n+1)); local out=$(printf "tmp/s%03d.mp4" $n)
  local txt=""
  if [[ "$card" != "-" ]]; then
    if [[ "$style" == "center" ]]; then txt=",drawtext=fontfile=$FONT:textfile=$card:fontsize=120:fontcolor=white:x=(w-tw)/2:y=(h-th)/2"
    else txt=",drawtext=fontfile=$FONT:textfile=$card:fontsize=60:fontcolor=white:borderw=3:bordercolor=black:box=1:boxcolor=black@0.55:boxborderw=20:x=70:y=70"; fi
  fi
  local fit="scale=1920:1080:force_original_aspect_ratio=decrease:flags=lanczos,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=60"
  if [[ "$spec" == ESC:* ]]; then   # ESC:recorte:veces_sonic:TEXTO_CON_GUIONES_BAJOS[:0 sin borde]
    IFS=':' read -r _ e_img e_ratio e_label e_ol <<< "${spec%%+*}"
    ./escala.sh "ref/$e_img.png" "$e_ratio" "${e_label//_/ }" "$dur" "$out" "${e_ol:-1}" > /dev/null
  elif [[ "$spec" == "BLACK" ]]; then
    "$FF" -v error -y -f lavfi -i "color=c=black:s=1920x1080:r=60:d=$dur" -f lavfi -i "anullsrc=r=48000:cl=stereo" -t $dur -vf "null$txt" $ENC "$out"
  elif [[ "$spec" == IMG:* ]]; then
    local img="ref/${spec#IMG:}.png"
    "$FF" -v error -y -f lavfi -i "color=c=0x10162b:s=1920x1080:r=60:d=$dur" -loop 1 -t $dur -i "$img" -f lavfi -i "anullsrc=r=48000:cl=stereo" \
      -filter_complex "[1:v]scale=-1:860:flags=lanczos,format=rgba[i];[0:v][i]overlay=(W-w)/2:(H-h)/2+30:shortest=1,scale=w='trunc(1920*(1+0.05*t/$dur)/2)*2':h=-2:eval=frame,crop=1920:1080,setsar=1$txt[v]" \
      -map "[v]" -map 2:a -t $dur $ENC "$out"
  else
    local s=${spec%%:*} in=${spec#*:}; in=${in%%+*}; local f=$(src_file $s)
    local af="volume=0.35,afade=t=in:d=0.05,afade=t=out:st=$(calc "$dur-0.08"):d=0.08"
    if has_audio "$f"; then
      "$FF" -v error -y -ss $in -t $dur -i "$f" -vf "$fit$txt" -af "$af" $ENC "$out"
    else
      "$FF" -v error -y -ss $in -t $dur -i "$f" -f lavfi -i "anullsrc=r=48000:cl=stereo" -map 0:v -map 1:a -t $dur -vf "$fit$txt" $ENC "$out"
    fi
  fi
  echo "file '$(basename $out)'" >> tmp/concat.txt
}

while IFS='|' read -r id voz planos cartel mus; do
  [[ -z "$id" || "$id" == \#* ]] && continue
  mus=${mus//$'\r'/}
  card="-"; if [[ "$cartel" != "-" ]]; then card="tmp/card_$id.txt"; printf '%s' "$cartel" > "$card"; fi
  if [[ "$voz" == SEP:* ]]; then D=${voz#SEP:}; else
    p="P${voz%%:*}"; k=${voz##*:}; D=${PDU[$p:$k]}
    echo "Voz/Parte${voz%%:*}.mp3|${PST[$p:$k]}|$D|$t" >> tmp/voices.txt
  fi
  echo "$(calc "$t") $id $voz" >> tmp/mapa.txt
  echo "$mus|$t|$D" >> tmp/music.txt
  if [[ "$planos" == "-" ]]; then shot BLACK $D "$card" center; t=$(calc "$t+$D"); continue; fi
  read -ra arr <<< "$planos"
  fixed=0; free=0
  for s in "${arr[@]}"; do if [[ "$s" == *+* ]]; then fixed=$(calc "$fixed+${s##*+}"); else free=$((free+1)); fi; done
  each=0; [[ $free -gt 0 ]] && each=$(calc "($D-$fixed)/$free")
  acc=0; i=0
  for s in "${arr[@]}"; do
    i=$((i+1))
    if [[ "$s" == *+* ]]; then d=${s##*+}; else d=$each; fi
    [[ $i -eq ${#arr[@]} ]] && d=$(calc "$D-$acc")   # el último cierra exacto
    shot "$s" $d "$card" top
    acc=$(calc "$acc+$d")
  done
  t=$(calc "$t+$D")
done < plan.txt
echo "total $t s, $n planos"
(cd tmp && "$FF" -v error -y -f concat -safe 0 -i concat.txt -c copy video.mp4)

# Voz: cada párrafo en su lugar.
inputs=(); filt=""; k=0
while IFS='|' read -r f st d at; do
  inputs+=(-i "$f"); ms=$(awk "BEGIN{printf \"%d\", $at*1000}")
  filt="$filt[$k:a]atrim=$st:$(calc "$st+$d"),asetpts=PTS-STARTPTS,aresample=48000,aformat=channel_layouts=stereo,adelay=${ms}|${ms}[v$k];"; k=$((k+1))
done < tmp/voices.txt
mix=""; for ((i=0;i<k;i++)); do mix="$mix[v$i]"; done
"$FF" -v error -y "${inputs[@]}" -filter_complex "${filt}${mix}amix=inputs=$k:normalize=0,apad,atrim=0:$t[out]" -map "[out]" -c:a pcm_s16le tmp/voices.wav

# Música: tramos seguidos con la misma pista; cada tramo arranca el tema desde el principio.
awk -F'|' '{ if ($1!=prev) { if (NR>1) print prev"|"start"|"(s+0); prev=$1; start=$2; s=0 } s+=$3 } END { print prev"|"start"|"s }' tmp/music.txt > tmp/music_runs.txt
inputs=(); filt=""; k=0
while IFS='|' read -r m st d; do
  [[ "$m" == "0" ]] && continue
  # en loop: los temas clásicos duran ~2 min
  inputs+=(-stream_loop -1 -i "${MUS[$m]}"); ms=$(awk "BEGIN{printf \"%d\", $st*1000}")
  filt="$filt[$k:a]atrim=0:$d,asetpts=PTS-STARTPTS,aresample=48000,aformat=channel_layouts=stereo,afade=t=in:d=1.2,afade=t=out:st=$(calc "$d-1.5"):d=1.5,adelay=${ms}|${ms}[m$k];"; k=$((k+1))
done < tmp/music_runs.txt
mix=""; for ((i=0;i<k;i++)); do mix="$mix[m$i]"; done
"$FF" -v error -y "${inputs[@]}" -filter_complex "${filt}${mix}amix=inputs=$k:normalize=0,apad,atrim=0:$t[out]" -map "[out]" -c:a pcm_s16le tmp/music.wav

./mix.sh
echo listo
