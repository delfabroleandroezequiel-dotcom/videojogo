#!/bin/bash
# Plano de escala en fondo negro: arranca cerca de Sonic y la cámara se aleja hasta que entra el jefe entero.
# Uso: ./escala.sh <ref/jefe.png> <veces_sonic> "<texto medida>" <duración> <salida.mp4>
# La medida va sobre el lado más largo del recorte (la regla de "lo más grande del cuerpo").
set -e
FF=/c/Users/delfa/Downloads/ffmpeg.exe
FP=/c/Users/delfa/Downloads/ffprobe.exe
FONT="../../Recursos/Fuentes/Anton-Regular.ttf"
IMG=$1; RATIO=$2; LABEL=$3; DUR=$4; OUT=$5; OL=${6:-1}   # 6º arg 0 = sin borde blanco (jefes claros o semitransparentes)
mkdir -p escalas
calc() { awk "BEGIN{printf \"%.4f\", $1}"; }
ceil2() { awk "BEGIN{v=int($1); if (v%2) v++; if (v<2) v=2; print v}"; }

# Recorte con borde blanco (como en las miniaturas), para que los jefes oscuros se lean sobre negro.
outline() { # $1=in $2=out — borde blanco solo POR FUERA de la silueta (las partes semitransparentes no se blanquean)
  "$FF" -v error -y -i "$1" -filter_complex "[0:v]format=rgba,pad=iw+24:ih+24:12:12:color=black@0,split=3[a][c][b];[a]alphaextract,lut=y='if(gt(val\,128)\,255\,0)',dilation,dilation,dilation,dilation,dilation,dilation[d];[c]alphaextract,lut=y='if(gt(val\,8)\,255\,0)'[in];[d][in]blend=all_mode=subtract,format=gray[m];color=white:s=16x16[w];[w][m]scale2ref[w2][m2];[w2][m2]alphamerge[ol];[ol][b]overlay=format=auto,format=rgba" -frames:v 1 "$2"
}
base=$(basename "$IMG" .png)
if [[ "$OL" == 0 ]]; then cp "$IMG" escalas/${base}_ol.png; else [[ -f escalas/${base}_ol.png ]] || outline "$IMG" escalas/${base}_ol.png; fi
[[ -f escalas/sonic_ol.png ]] || outline ref/sonic.png escalas/sonic_ol.png
BOSS=escalas/${base}_ol.png; SON=escalas/sonic_ol.png

read BW BH < <("$FP" -v error -show_entries stream=width,height -of csv=p=0 "$BOSS" | tr ',' ' ')
read SW SH < <("$FP" -v error -show_entries stream=width,height -of csv=p=0 "$SON" | tr ',' ' ')

# Cuadro final: el jefe entra en 1250x700, apoyado en el piso.
GY=930
K=$(awk "BEGIN{a=1250/$BW; b=700/$BH; print (a<b)?a:b}")
BW1=$(calc "$BW*$K"); BH1=$(calc "$BH*$K")
BIG=$(awk "BEGIN{print ($BW1>$BH1)?$BW1:$BH1}")
SH1=$(calc "$BIG/$RATIO"); SW1=$(calc "$SH1*$SW/$SH")
# Zoom inicial: Sonic siempre arranca a ~480 px. El jefe nunca se dibuja a más de 5000 px (ZC):
# en los gigantes, Sonic se achica solo y el jefe entra desde la derecha cuando ya cabe.
Z0=$(awk "BEGIN{z=480/$SH1; if (z<1) z=1; print z}")
ZC=$(awk "BEGIN{z=5000/$BIG; if (z<1) z=1; print z}")
GAP=$(calc "$SW1*0.4+14")
if awk "BEGIN{exit !($Z0>$ZC)}"; then         # separar a Sonic lo justo para que el jefe esté fuera de cuadro hasta ZC
  GAP=$(awk "BEGIN{g=$GAP; while (g<700) { x0=(1920-($SW1+g+$BW1))/2; ax=x0+$SW1/2; if (ax+($SW1/2+g)*$ZC>=1980) break; g+=4 } print g}")
fi
TOT=$(calc "$SW1+$GAP+$BW1"); X0=$(calc "(1920-$TOT)/2")
SX1=$X0; SY1=$(calc "$GY-$SH1"); BX1=$(calc "$X0+$SW1+$GAP"); BY1=$(calc "$GY-$BH1")
AX=$(calc "$SX1+$SW1/2")                     # la cámara se aleja con Sonic como centro
T=$(calc "$DUR*0.62")                        # el resto del plano queda quieto con la medida
P="min(t/$T\,1)"; E="($P*$P*(3-2*$P))"; Z="pow($Z0\,1-$E)"; ZB="min($Z\,$ZC)"

sw="max(2\,trunc($SW1*$Z/2)*2)"; sh="max(2\,trunc($SH1*$Z/2)*2)"
bw="max(2\,trunc($BW1*$ZB/2)*2)"; bh="max(2\,trunc($BH1*$ZB/2)*2)"
AA="(760+($AX-760)*$E)"                       # al principio Sonic va cerca del centro; termina en su lugar
sx="$AA+($SX1-$AX)*$Z"; sy="$GY+($SY1-$GY)*$Z"
bx="$AA+($BX1-$AX)*$Z"; by="$GY+($BY1-$GY)*$Z"

# Regla sobre el lado largo, aparece cuando termina el alejamiento.
SHOW="gte(t\,$T)"
if awk "BEGIN{exit !($BW1>$BH1)}"; then
  RX=$(calc "$BX1"); RW=$(calc "$BW1"); RY=$((GY+34))
  RULE="drawbox=x=$RX:y=$RY:w=$RW:h=5:color=white:t=fill:enable='$SHOW',drawbox=x=$RX:y=$((RY-14)):w=5:h=33:color=white:t=fill:enable='$SHOW',drawbox=x=$(calc "$RX+$RW-5"):y=$((RY-14)):w=5:h=33:color=white:t=fill:enable='$SHOW'"
  LX="$(calc "$RX+$RW/2")-tw/2"; LY="$(calc "$BY1-125")"
else
  RX=$(calc "$BX1+$BW1+40"); RY=$(calc "$BY1"); RH=$(calc "$BH1")
  RULE="drawbox=x=$RX:y=$RY:w=5:h=$RH:color=white:t=fill:enable='$SHOW',drawbox=x=$(calc "$RX-14"):y=$RY:w=33:h=5:color=white:t=fill:enable='$SHOW',drawbox=x=$(calc "$RX-14"):y=$(calc "$RY+$RH-5"):w=33:h=5:color=white:t=fill:enable='$SHOW'"
  LX="$(calc "$RX+30")"; LY="$(calc "$RY+$RH/2")-th/2"
  # si no entra a la derecha, el número va arriba de la regla
  if awk "BEGIN{exit !($RX+30+420>1900)}"; then LX="$(calc "$RX")-tw+2"; LY="$(calc "$RY-120")"; fi
fi
printf '%s' "$LABEL" > escalas/_label.txt
printf '%s' "SONIC · 1 m" > escalas/_sonic.txt
# Etiqueta de Sonic: sigue su cabeza mientras se aleja la cámara; con Sonic diminuto, una línea que lo señala.
lx="$AA-tw/2"; ly="($sy)-142"

"$FF" -v error -y -f lavfi -i "color=c=black:s=1920x1080:r=60:d=$DUR" -loop 1 -t $DUR -i "$BOSS" -loop 1 -t $DUR -i "$SON" -f lavfi -i "anullsrc=r=48000:cl=stereo" \
 -f lavfi -i "color=c=white:s=4x56:r=60:d=$DUR" -filter_complex "\
[1:v]scale=w='$bw':h='$bh':eval=frame:flags=bicubic[b];\
[2:v]scale=w='$sw':h='$sh':eval=frame:flags=bicubic[s];\
[0:v]drawbox=x=0:y=$GY:w=1920:h=2:color=0x3a3a3a:t=fill[g];\
[g][b]overlay=x='$bx':y='$by':eval=frame:shortest=1:enable='lte($Z,$ZC*1.001)'[gb];\
[gb][s]overlay=x='$sx':y='$sy':eval=frame[gs0];\
[gs0][4:v]overlay=x='$AA-2':y='($sy)-66':eval=frame:shortest=1[gs];\
[gs]drawtext=fontfile=$FONT:textfile=escalas/_sonic.txt:fontsize=46:fontcolor=white:x='$lx':y='$ly',\
$RULE,drawtext=fontfile=$FONT:textfile=escalas/_label.txt:fontsize=96:fontcolor=white:x='$LX':y='$LY':alpha='min(1\,max(0\,(t-$T)*4))',setsar=1,format=yuv420p[v]" \
 -map "[v]" -map 3:a -t $DUR -c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p -r 60 -c:a aac -b:a 192k -ar 48000 -ac 2 "$OUT"
echo "ok $OUT (Z0=$Z0, Sonic final ${SH1}px)"
