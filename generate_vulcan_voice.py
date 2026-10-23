import asyncio
import edge_tts
import os
import subprocess

# VULCAN // FORGE-LORD - Heavy, gravelly, menacing. Contrast to Ido.
VOICE = "en-US-ChristopherNeural"
RATE = "-12%"   # slow, deliberate, threatening
PITCH = "-15Hz" # deep furnace rumble

OUT_DIR = "Assets/Audio/Voice/Vulcan"

LINES = {
    # 00 - INTRO: plays with title card after drop, 0:04-0:09
    "vulcan_01_intro.wav": "You broke my toys on the bridge. Stole my pass. Shamed my Pit before five hundred witnesses. I sent troops. You sent back scrap. So now... the Forge comes himself. Alita... I will unmake you.",

    # 01 - PHASE 2 at 66%: ReactorPowerUp + red lights
    "vulcan_02_phase2.wav": "Good! You bleed like a warrior! Now... burn brighter!",

    # 02 - PHASE 3 at 33%: drops honor, emissive x2
    "vulcan_03_phase3.wav": "Enough honor. Enough games. FORGE... TAKE YOU!",

    # 03 - DEATH: kneeling, before [E] FINISH prompt
    "vulcan_04_death.wav": "Impossible... a scrap-girl... fells... Vulcan... the Pit... is yours...",
}

async def generate():
    os.makedirs(OUT_DIR, exist_ok=True)
    for filename, text in LINES.items():
        base_name, _ = os.path.splitext(filename)
        temp_mp3 = os.path.join(OUT_DIR, f"{base_name}.mp3")
        final_wav = os.path.join(OUT_DIR, filename)
        
        communicate = edge_tts.Communicate(text, VOICE, rate=RATE, pitch=PITCH)
        await communicate.save(temp_mp3)
        
        # Convert to genuine 16-bit PCM WAV using macOS afconvert
        subprocess.run(["/usr/bin/afconvert", "-f", "WAVE", "-d", "LEI16", temp_mp3, final_wav], check=True)
        if os.path.exists(temp_mp3):
            os.remove(temp_mp3)
            
        print(f"Generated: {filename} -> {final_wav}")

if __name__ == "__main__":
    asyncio.run(generate())
