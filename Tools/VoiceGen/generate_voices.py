"""Generates one neural-voice mp3 per dialogue line listed in manifest.json.

    python Tools/VoiceGen/extract_dialogue.py     # refresh the manifest from code, scenes and VoiceLines/missing.tsv
    python Tools/VoiceGen/generate_voices.py      # create missing clips in Assets/Resources/VoiceLines (needs internet)

Clips are named <key>.mp3 where key = sha1 of the normalised text (see voice_common.py / DialogueVoice.cs).
Existing clips are skipped, so re-running only produces new lines. Use --force to regenerate everything.
Requires: pip install edge-tts
"""
import asyncio
import hashlib
import json
import os
import re
import sys

import edge_tts

sys.path.insert(0, os.path.dirname(__file__))

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "VoiceLines")
MANIFEST = os.path.join(HERE, "manifest.json")

# (voice, rate, pitch)
# Natural-sounding casting: the expressive "Multilingual" voices carry the main cast, and pitch/rate stay
# close to neutral. Heavy pitch shifts are what make a TTS voice sound robotic, so they are avoided.
CAST = [
    (("ALITA",), ("en-US-AvaMultilingualNeural", "+0%", "+0Hz")),
    (("DYSON", "IDO", "DOCTOR"), ("en-US-AndrewMultilingualNeural", "-4%", "+0Hz")),
    (("VULCAN", "FORGE-LORD", "FORGE LORD"), ("en-US-ChristopherNeural", "-8%", "-3Hz")),
    (("KAELEN", "VOSS", "COLLECTOR"), ("en-US-GuyNeural", "-4%", "+0Hz")),
    (("CYNOSURE", "EREBUS", "BLACKWALL"), ("en-US-EmmaMultilingualNeural", "-4%", "+0Hz")),
    (("DRIFT CONTROL", "STATION", "TRAFFIC", "SENSORS", "MAINFRAME", "SYSTEM", "DISPATCH", "RADAR", "COMMS //", "TERMINAL"),
     ("en-GB-RyanNeural", "+0%", "+0Hz")),
    (("ENFORCER", "BOUNTY", "ROGUE", "RECOVERY", "VECTOR", "UNIT", "BOT", "DROID", "SQUAD", "PILOT", "COMMANDER", "CAPTAIN", "INSPECTOR"),
     ("en-IE-ConnorNeural", "+0%", "+0Hz")),
    (("SCALPER", "RAZOR", "PUNK", "THUG"), ("en-CA-LiamNeural", "+3%", "+0Hz")),
]

# Background NPCs: warm, human voices with different accents; the flat "rational" voices are left out
MALE_POOL = [
    ("en-US-BrianMultilingualNeural", "+0%", "+0Hz"), ("en-GB-ThomasNeural", "+0%", "+0Hz"), ("en-NZ-MitchellNeural", "+0%", "+0Hz"),
    ("en-ZA-LukeNeural", "+0%", "+0Hz"), ("en-AU-WilliamMultilingualNeural", "+0%", "+0Hz"), ("en-SG-WayneNeural", "+0%", "+0Hz"),
    ("en-IN-PrabhatNeural", "+0%", "+0Hz"), ("en-PH-JamesNeural", "+0%", "+0Hz"), ("en-HK-SamNeural", "+0%", "+0Hz"),
]
FEMALE_POOL = [
    ("en-US-AriaNeural", "+0%", "+0Hz"), ("en-US-MichelleNeural", "+0%", "+0Hz"), ("en-US-JennyNeural", "+0%", "+0Hz"),
    ("en-GB-SoniaNeural", "+0%", "+0Hz"), ("en-AU-NatashaNeural", "+0%", "+0Hz"), ("en-GB-LibbyNeural", "+0%", "+0Hz"),
    ("en-CA-ClaraNeural", "+0%", "+0Hz"), ("en-NZ-MollyNeural", "+0%", "+0Hz"), ("en-IE-EmilyNeural", "+0%", "+0Hz"),
]
FEMALE_NAMES = {
    "nyx", "zara", "madame", "astra", "bex", "cyra", "sora", "lexa", "zephyr", "vesper", "dex'", "gorb", "jett", "nova", "luna",
    "mara", "kira", "vera", "selene", "iris", "ember", "raven", "sasha", "tala", "yara", "mina",
}


def pick_voice(speaker: str, text: str):
    up = (speaker or "").upper()
    for words, voice in CAST:
        if any(w in up for w in words):
            return voice
    if not up:
        # unattributed line: stable choice from the text itself
        pool = MALE_POOL + FEMALE_POOL
        return pool[int(hashlib.sha1(text.encode("utf-8")).hexdigest(), 16) % len(pool)]
    first = re.sub(r"[^a-z' ]", "", speaker.lower()).split()
    female = any(w in FEMALE_NAMES for w in first)
    pool = FEMALE_POOL if female else MALE_POOL
    h = int(hashlib.sha1(speaker.lower().encode("utf-8")).hexdigest(), 16)
    return pool[h % len(pool)]


async def make(sem, item, force):
    path = os.path.join(OUT_DIR, item["key"] + ".mp3")
    if os.path.exists(path) and os.path.getsize(path) > 1000 and not force:
        return "skip", item
    voice, rate, pitch = pick_voice(item["speaker"], item["text"])
    async with sem:
        for attempt in range(4):
            try:
                comm = edge_tts.Communicate(item["text"], voice, rate=rate, pitch=pitch)
                await comm.save(path)
                if os.path.getsize(path) > 1000:
                    return "ok", item
            except Exception as ex:  # network hiccups: retry
                err = str(ex)
                await asyncio.sleep(1.5 * (attempt + 1))
        return "fail", item


async def main(force):
    os.makedirs(OUT_DIR, exist_ok=True)
    items = json.load(open(MANIFEST, encoding="utf-8"))
    workers = 3
    if "--workers" in sys.argv:
        workers = int(sys.argv[sys.argv.index("--workers") + 1])
    sem = asyncio.Semaphore(workers)
    results = await asyncio.gather(*(make(sem, it, force) for it in items))
    counts = {"ok": 0, "skip": 0, "fail": 0}
    for status, it in results:
        counts[status] += 1
        if status == "fail":
            print("FAILED:", it["speaker"], "|", it["text"][:70])
    print(f"done: {counts['ok']} generated, {counts['skip']} already existed, {counts['fail']} failed -> {OUT_DIR}")

    voices = {}
    for it in items:
        v = pick_voice(it["speaker"], it["text"])[0]
        voices.setdefault(it["speaker"] or "(unattributed)", v)
    with open(os.path.join(HERE, "voices_report.json"), "w", encoding="utf-8") as f:
        json.dump(voices, f, indent=1, ensure_ascii=False)


if __name__ == "__main__":
    asyncio.run(main("--force" in sys.argv))
