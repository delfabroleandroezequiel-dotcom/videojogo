#!/bin/bash
# Final audio mix only (reuses tmp/video.mp4 and tmp/voices.wav from build.sh).
# No extra music bed: the game already carries Artemisa's music. Game/Elden Ring audio sits low
# and drops ~15 dB under the narrator; the voice is compressed and pushed forward.
FF=/c/Users/delfa/Downloads/ffmpeg.exe
"$FF" -v error -y -i tmp/video.mp4 -i tmp/voices.wav -filter_complex \
 "[0:a]volume=0.45[bed];[1:a]acompressor=threshold=0.1:ratio=3:attack=5:release=120:makeup=1.6,volume=1.4,asplit=2[vk][vm];[bed][vk]sidechaincompress=threshold=0.03:ratio=4:attack=10:release=450:makeup=1[duck];[duck][vm]amix=inputs=2:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=9[a]" \
 -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 192k Malenia2D_PrimerCorte.mp4
