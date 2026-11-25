#!/bin/bash
# hoja.sh nombre [inicio] [dur] -> hojas/h_nombre.png con 1 cuadro cada N s (N segun duracion), 10 columnas
F=/c/Users/delfa/Downloads/ffmpeg.exe; P=/c/Users/delfa/Downloads/ffprobe.exe
n=$1; ss=${2:-0}; f=fuentes/$n.mp4
d=${3:-$($P -v error -show_entries format=duration -of csv=p=0 $f)}
step=$(py -3 -c "import math;print(max(1,math.ceil(float('$d')/60)))")
$F -hide_banner -loglevel error -y -ss $ss -t $d -i $f -vf "fps=1/$step,scale=192:-1,tile=10x6" -frames:v 1 hojas/h_$n.png
echo "$n step=$step start=$ss"
