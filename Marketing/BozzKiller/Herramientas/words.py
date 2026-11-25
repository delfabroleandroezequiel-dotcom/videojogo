import sys, wave, numpy as np, subprocess
from faster_whisper import WhisperModel
m = WhisperModel("small", device="cpu", compute_type="int8")
ff = r"C:\Users\delfa\Downloads\ffmpeg.exe"
for f in sys.argv[1:]:
    subprocess.run([ff,"-v","error","-i",f,"-ac","1","-ar","16000",f+".wav","-y"],check=True)
    w=wave.open(f+".wav"); a=np.frombuffer(w.readframes(w.getnframes()),np.int16).astype(np.float32)/32768
    segs,_=m.transcribe(a,language="es",word_timestamps=True)
    print("==",f, " ".join(f"{x.word.strip()}@{x.start:.2f}-{x.end:.2f}" for s in segs for x in s.words))
