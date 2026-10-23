"""Synthesises the Mars wind beds into Assets/Resources/MarsSfx (loaded by MartianDustStormSystem).

Both clips tile seamlessly: the noise is built in the frequency domain and every slow modulation has a whole
number of cycles over the clip, so there is no click at the loop point and no tonal whistle.

  Mars_Wind_Loop.wav   8 s  steady thin-atmosphere wind: low body, airy mid, faint grit, slow breathing
  Mars_Gust_Loop.wav  11 s  surging gusts: band-limited rushes that swell and fall, with a soft sand hiss on top

Run:  python Tools/SfxGen/make_mars_ambience.py
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "MarsSfx")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(404)


def periodic_noise(n, lo, hi, tilt=1.0):
    spec = np.zeros(n // 2 + 1, dtype=complex)
    f = np.fft.rfftfreq(n, 1 / SR)
    m = (f >= lo) & (f <= hi)
    spec[m] = np.exp(1j * rng.uniform(0, 2 * np.pi, m.sum())) / np.maximum(f[m], 1) ** (tilt * 0.5)
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def lfo(t, dur, cycles, phase=0.0):
    return np.sin(2 * np.pi * cycles * t / dur + phase)


def save(name, x, peak=0.85):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.normpath(path), "%.1fs" % (len(x) / SR))


def wind():
    dur = 8.0
    n = int(SR * dur)
    t = np.arange(n) / SR
    body = periodic_noise(n, 40, 420, 1.6)
    air = periodic_noise(n, 300, 3200, 1.2)
    grit = periodic_noise(n, 2500, 9000, 0.6)
    breath = 0.72 + 0.20 * lfo(t, dur, 2, 0.3) + 0.08 * lfo(t, dur, 5, 1.1)
    airmod = 0.55 + 0.45 * (0.5 + 0.5 * lfo(t, dur, 3, 2.0))
    x = body * 0.95 * breath + air * 0.55 * airmod * breath + grit * 0.10 * airmod
    save("Mars_Wind_Loop", x)


def gust():
    dur = 11.0
    n = int(SR * dur)
    t = np.arange(n) / SR
    rush = periodic_noise(n, 90, 1800, 1.0)
    hiss = periodic_noise(n, 1800, 7500, 0.7)
    # two out-of-step swells so the surge never repeats exactly within the clip
    swell = 0.5 + 0.5 * lfo(t, dur, 3, -1.2)
    swell2 = 0.5 + 0.5 * lfo(t, dur, 5, 0.6)
    env = (0.12 + 0.88 * swell ** 2.2) * (0.55 + 0.45 * swell2)
    x = rush * env + hiss * 0.22 * env ** 1.5
    save("Mars_Gust_Loop", x)


if __name__ == "__main__":
    wind()
    gust()
