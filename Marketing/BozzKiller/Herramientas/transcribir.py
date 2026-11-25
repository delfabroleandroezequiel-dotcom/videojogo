import sys, json
from faster_whisper import WhisperModel
m = WhisperModel(sys.argv[3] if len(sys.argv)>3 else "medium", device="cpu", compute_type="int8")
import wave, numpy as np
w=wave.open(sys.argv[1]); a=np.frombuffer(w.readframes(w.getnframes()),np.int16).astype(np.float32)/32768
segs, _ = m.transcribe(a, language="es", word_timestamps=True, vad_filter=True)
out=[]
with open(sys.argv[2],"w",encoding="utf-8") as f:
    for s in segs:
        f.write(f"[{s.start:7.2f}-{s.end:7.2f}] {s.text.strip()}\n"); f.flush()
        out.append({"s":s.start,"e":s.end,"t":s.text,"w":[(w.start,w.end,w.word) for w in s.words]})
json.dump(out,open(sys.argv[2]+".json","w",encoding="utf-8"),ensure_ascii=False)
