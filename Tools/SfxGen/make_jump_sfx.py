"""Synthesises Alita's jump sounds into Assets/Audio/Combat.

Takeoff  (Jump_Hop_Servo_01..03):  a short servo "hup": soft pneumatic thump, a quick rising servo chirp and a
                                   tiny metallic click. No noise sweep, so nothing whooshes.
Landing  (Jump_Land_Armor_01..03): armoured boots on deck: low thud, mid body, a brief plate rattle and a light
                                   room tail; the harder variants are for longer falls.
Run:  python Tools/SfxGen/make_jump_sfx.py
"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Audio", "Combat")
rng = np.random.default_rng(7)


def t_axis(sec):
    return np.arange(int(SR * sec)) / SR


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def comb_tail(x, delay_ms, fb, repeats):
    out = x.copy()
    d = int(SR * delay_ms / 1000)
    for r in range(1, repeats + 1):
        shifted = np.zeros_like(x)
        shifted[d * r:] = x[:len(x) - d * r] * (fb ** r)
        out += shifted
    return out


def save(name, x, peak=0.85):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    # 4 ms fade in/out to avoid clicks
    f = int(SR * 0.004)
    x[:f] *= np.linspace(0, 1, f)
    x[-f:] *= np.linspace(1, 0, f)
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.normpath(path))


def takeoff(variant):
    dur = 0.26
    t = t_axis(dur)
    base = [330, 380, 300][variant]
    top = [760, 880, 690][variant]
    thump = np.sin(2 * np.pi * np.cumsum(np.linspace(78, 46, t.size)) / SR) * np.exp(-t * 34) * 0.9
    sweep_f = np.linspace(base, top, t.size) * (1 + 0.015 * np.sin(2 * np.pi * 38 * t))
    phase = 2 * np.pi * np.cumsum(sweep_f) / SR
    env = np.clip(t / 0.012, 0, 1) * np.exp(-np.maximum(t - 0.02, 0) * 14)
    servo = (np.sin(phase) * 0.55 + np.sin(2 * phase) * 0.18 + np.sin(3 * phase) * 0.06) * env * 0.55
    click = np.zeros_like(t)
    n = int(SR * 0.0035)
    click[:n] = rng.standard_normal(n) * np.linspace(1, 0, n) * 0.5
    ring = np.sin(2 * np.pi * (2300 + 150 * variant) * t) * np.exp(-t * 90) * 0.18
    x = thump + servo + click + ring
    return comb_tail(x, 11, 0.22, 2)


def landing(variant):
    dur = [0.30, 0.34, 0.40][variant]
    t = t_axis(dur)
    heavy = [0.8, 1.0, 1.25][variant]
    sub = np.sin(2 * np.pi * np.cumsum(np.linspace(60, 38, t.size)) / SR) * np.exp(-t * (16 - 2 * variant)) * 1.0 * heavy
    body = np.sin(2 * np.pi * 118 * t) * np.exp(-t * 28) * 0.55
    slap = lowpass(rng.standard_normal(t.size), 2600) * np.exp(-t * 95) * 0.9
    rattle = np.zeros_like(t)
    for k, (f, d) in enumerate([(1250, 55), (1870, 70), (2650, 85)]):
        start = int(SR * (0.010 + 0.012 * k))
        seg = t[:t.size - start]
        rattle[start:] += np.sin(2 * np.pi * f * seg) * np.exp(-seg * d) * (0.22 - 0.05 * k)
    x = sub + body + slap + rattle
    return comb_tail(x, 17, 0.28, 3)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for i in range(3):
        save(f"Jump_Hop_Servo_{i + 1:02d}.wav", takeoff(i), peak=0.70)
        save(f"Jump_Land_Armor_{i + 1:02d}.wav", landing(i), peak=0.92)
