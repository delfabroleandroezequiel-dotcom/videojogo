#!/bin/bash
Y=/c/Users/delfa/Downloads/yt-dlp.exe
D="$(dirname "$0")"
cd "$D/musica"
while read id name sec; do
  [ -f "$name.m4a" ] && continue
  if [ -n "$sec" ]; then "$Y" -f "ba[ext=m4a]/ba" --download-sections "*$sec" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1
  else "$Y" -f "ba[ext=m4a]/ba" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1; fi
  sleep 6
done <<'L'
P9Ee4TevHfA m1_smb2
V-AQuJ5pQp0 m2_bowserjr
kasePUUDdK8 m3_sm64boss
BK8FxZv2mmM m4_yiboss
-xbWvXkI6ZQ m6_chai
mMXM0qw0duM m5_birabuto 0:00-3:00
5IoS_wonD3E m7_tatanga
5mWp0g-lwzk m8_smithy 0:00-2:30
L
# audio del juego para respiros
cd "$D/fuentes"
for n in smb2_wart smb2_clawgrip sml_longplay smg_megaleg smg_kingfin sm64_eyerok yi_hookbill smrpg_smithy sml2_tatanga mkds_eyerok; do
  id=$(grep " $n$" lista.txt | cut -d' ' -f1)
  [ -f "$n.m4a" ] || "$Y" -f "ba[ext=m4a]/ba" -o "$n.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1
  sleep 6
done
echo DONE2 >> dl.log
