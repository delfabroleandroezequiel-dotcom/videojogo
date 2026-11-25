#!/bin/bash
# baja video (720p sin audio) + audio m4a de cada fuente, y la música. Rutas absolutas.
Y=/c/Users/delfa/Downloads/yt-dlp.exe
D=/c/Users/delfa/Desktop/Videojogo/Marketing/Canal/JefesOlvidadosSonic
cd "$D/fuentes"
while read id name; do
  [ -f "$name.mp4" ] || "$Y" -q -f "bv*[height<=720][ext=mp4]/bv*[height<=720]" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1 || echo "FAIL $name" >> dl.log
  [ -f "$name.m4a" ] || "$Y" -q -f "ba[ext=m4a]/ba" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1
  sleep 5
done < lista.txt
[ -f bk_bosses.mp4 ] || "$Y" -q -f "bv*[height<=720][ext=mp4]/bv*[height<=720]" --download-sections "*0:00-8:00" -o "bk_bosses.%(ext)s" "https://www.youtube.com/watch?v=WWUjHVPAj1Y" >> dl.log 2>&1
[ -f bk_bosses.m4a ] || "$Y" -q -f "ba[ext=m4a]/ba" --download-sections "*0:00-8:00" -o "bk_bosses.%(ext)s" "https://www.youtube.com/watch?v=WWUjHVPAj1Y" >> dl.log 2>&1
cd "$D/musica"
while read id name sec; do
  [ -f "$name.m4a" ] && continue
  if [ -n "$sec" ]; then "$Y" -q -f "ba[ext=m4a]/ba" --download-sections "*$sec" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1
  else "$Y" -q -f "ba[ext=m4a]/ba" -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1; fi
  sleep 5
done <<'L'
BpXv3VLT8nw m_tt_boss
SWMcIB7XIi0 m_unleashed
lTmpO5RJUVs m_erazor 0:00-2:30
ScvxT0RItYE m_heroes
pNB4Cy4mftI m_blackknight
amQqz7UsoQo m_colorsds
BXisDB21XKY m_spinball
oDRM1dZJyWc m_skypatrol 0:00-3:00
542m1P5vZqU m_rivals2
L
echo DONE >> "$D/fuentes/dl.log"
