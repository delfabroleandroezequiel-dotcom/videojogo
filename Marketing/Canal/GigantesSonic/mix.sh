#!/bin/bash
# Mezcla final (reusa tmp/video.mp4, tmp/voices.wav y tmp/music.wav de build.sh).
# Sin audio del juego (tapaba al narrador): solo voz + temas originales de Sonic, bajos, que se agacha bajo la voz.
FF=/c/Users/delfa/Downloads/ffmpeg.exe
"$FF" -v error -y -i tmp/video.mp4 -i tmp/voices.wav -i tmp/music.wav -filter_complex \
 "[2:a]volume=0.14[bed];[1:a]acompressor=threshold=0.1:ratio=3:attack=5:release=120:makeup=1.6,volume=1.4,asplit=2[vk][vm];[bed][vk]sidechaincompress=threshold=0.03:ratio=4:attack=10:release=450:makeup=1[duck];[duck][vm]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=9,alimiter=limit=0.84:level=false[a]" \
 -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 192k Gigantes_PrimerCorte.mp4
