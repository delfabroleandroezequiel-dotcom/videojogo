#!/bin/bash
Y=/c/Users/delfa/Downloads/yt-dlp.exe
cd "$(dirname "$0")/fuentes"
while read id name; do
  [ -f "$name.mp4" ] && continue
  "$Y" -f "bv*[height<=720][ext=mp4]/bv*[height<=720]/b[height<=720]" --remux-video mp4 -o "$name.%(ext)s" "https://www.youtube.com/watch?v=$id" >> dl.log 2>&1 || echo "FAIL $id $name" >> dl.log
  sleep 8
done < lista.txt
# secciones de videos largos
[ -f smrpg_remake.mp4 ] || "$Y" -f "bv*[height<=720][ext=mp4]/bv*[height<=720]" --download-sections "*0:00-9:00" -o "smrpg_remake.%(ext)s" "https://www.youtube.com/watch?v=6gLo0kQq0dg" >> dl.log 2>&1
sleep 8
[ -f odyssey_arena.mp4 ] || "$Y" -f "bv*[height<=720][ext=mp4]/bv*[height<=720]" --download-sections "*1:00-3:00" -o "odyssey_arena.%(ext)s" "https://www.youtube.com/watch?v=ZSex5G_eOek" >> dl.log 2>&1
sleep 8
[ -f ddp_mamu.mp4 ] || "$Y" -f "bv*[height<=720][ext=mp4]/bv*[height<=720]" --download-sections "*10:00-18:19" -o "ddp_mamu.%(ext)s" "https://www.youtube.com/watch?v=WdmLtJCm4HA" >> dl.log 2>&1
echo DONE >> dl.log
