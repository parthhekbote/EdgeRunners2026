import asyncio
import edge_tts
import os

VOICE = "en-US-AndrewMultilingualNeural" # Warm, Confident, Authentic, Honest (Christoph Waltz Dr. Ido tone)
RATE = "-6%"   # Deliberate, thoughtful doctor pacing
PITCH = "-2Hz" # Warm, resonant, mature tone

OUT_DIR = r"D:\vscodeprops\cyberpunk-dev-main\Assets\Audio\Voice\Ido"

LINES = {
    "ido_01_search.wav": "More salvage dumped from Zalem... Nothing intact in this storm.",
    "ido_02_notice.wav": "Wait... What is that faint glow beneath the wreckage?",
    "ido_03_examine.wav": "A Martian Berserker chassis... Discarded centuries ago.",
    "ido_04_core_warm.wav": "Her brain is intact... And her antimatter core is still warm.",
    "ido_05_stasis.wav": "She is in deep stasis. I need to take her back to the clinic.",
    "ido_06_carry_home.wav": "Hang on, little angel. Let's get you home."
}

async def generate():
    os.makedirs(OUT_DIR, exist_ok=True)
    for filename, text in LINES.items():
        out_path = os.path.join(OUT_DIR, filename)
        communicate = edge_tts.Communicate(text, VOICE, rate=RATE, pitch=PITCH)
        # edge-tts generates mp3/wav audio stream
        await communicate.save(out_path)
        print(f"Generated neural voice: {filename} -> {out_path}")

if __name__ == "__main__":
    asyncio.run(generate())
