"""Synthesises the Syndicate hover-tank sound set into Assets/Resources/TankSfx (loaded by SyndicateHoverTankAI).

Cannon blast, capacitor charge, laser bolt, armour hit, engine spool, a seamless hover loop, a shell impact
explosion and a long multi-stage death explosion. Each is layered (sub drop, filtered body, crack, debris, room tail)
instead of a single sine plus noise.

Run:  python Tools/SfxGen/make_tank_sfx.py
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "TankSfx")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(2077)


def T(sec):
    return np.arange(int(SR * sec)) / SR


def noise(n):
    return rng.standard_normal(n)


def lp(x, fc, order=2):
    return signal.sosfilt(signal.butter(order, fc, "low", fs=SR, output="sos"), x)


def hp(x, fc, order=2):
    return signal.sosfilt(signal.butter(order, fc, "high", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def ad(t, attack, decay):
    return np.clip(t / max(attack, 1e-4), 0, 1) * np.exp(-t / decay)


def sat(x, d=2.0):
    return np.tanh(x * d) / np.tanh(d)


def sweep(t, f0, f1, power=1.0):
    f = f0 + (f1 - f0) * (t / t[-1]) ** power
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def reverb(x, tail=0.7, wet=0.25, bright=3000):
    n = int(SR * tail)
    tt = np.arange(n) / SR
    ir = lp(noise(n), bright) * np.exp(-tt / (tail * 0.3))
    ir[0] = 0
    w = signal.fftconvolve(x, ir)
    w = w / (np.max(np.abs(w)) + 1e-9) * np.max(np.abs(x)) * wet
    out = np.zeros(len(w))
    out[: len(x)] = x
    return out + w


def crackle(n, density, hi=2500):
    return hp(noise(n), hi) * (rng.random(n) > (1 - density))


def periodic_noise(n, lo, hi, tilt=1.0):
    spec = np.zeros(n // 2 + 1, dtype=complex)
    f = np.fft.rfftfreq(n, 1 / SR)
    m = (f >= lo) & (f <= hi)
    spec[m] = np.exp(1j * rng.uniform(0, 2 * np.pi, m.sum())) / np.maximum(f[m], 1) ** (tilt * 0.5)
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def save(name, x, peak=0.9, loop=False):
    x = np.asarray(x, dtype=np.float64)
    if not loop:
        x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    if not loop:
        f = int(SR * 0.003)
        x[:f] *= np.linspace(0, 1, f)
        tail = int(SR * 0.03)
        x[-tail:] *= np.linspace(1, 0, tail)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.normpath(path), "%.2fs" % (len(x) / SR))


def cannon_blast():
    t = T(1.5)
    n = len(t)
    crack = bp(noise(n), 900, 9000) * ad(t, 0.0004, 0.02)
    boom = np.sin(2 * np.pi * np.cumsum(28 + 90 * np.exp(-t * 16)) / SR) * ad(t, 0.002, 0.34)
    body = lp(noise(n), 450) * ad(t, 0.001, 0.12)
    ring = bp(noise(n), 160, 700) * ad(t, 0.003, 0.3) * 0.5
    tail = lp(noise(n), 130) * ad(t - 0.05, 0.06, 0.55) * (t > 0.05)
    x = sat(crack * 0.9 + boom * 1.6 + body * 1.1 + ring + tail * 0.9, 1.8)
    save("Tank_Cannon_Blast", reverb(x, 0.9, 0.3, 2200))


def cannon_charge():
    t = T(0.95)
    n = len(t)
    prog = t / t[-1]
    whine = sweep(t, 140, 1500, 1.7) * 0.5 + sweep(t, 280, 3000, 1.7) * 0.18
    trem = 0.7 + 0.3 * np.sin(2 * np.pi * np.cumsum(8 + 40 * prog) / SR)
    hiss = bp(noise(n), 2500, 9000) * prog ** 2 * 0.3
    hum = np.sin(2 * np.pi * 60 * t) * 0.35 * prog
    env = (0.15 + 0.85 * prog ** 1.4) * np.clip((t[-1] - t) / 0.03, 0, 1)
    click_t = T(0.05)
    click = np.zeros(n)
    c = bp(noise(len(click_t)), 1500, 7000) * ad(click_t, 0.0005, 0.01)
    click[-len(c) - 40:-40] += c * 0.9
    x = sat((whine * trem + hiss + hum) * env + click, 1.4)
    save("Tank_Cannon_Charge", reverb(x, 0.3, 0.1))


def engine_spool():
    t = T(1.6)
    n = len(t)
    prog = t / t[-1]
    f = 55 + 260 * prog ** 1.6
    phase = 2 * np.pi * np.cumsum(f) / SR
    turbine = bp(signal.sawtooth(phase) * 0.6 + signal.square(phase * 2.01, 0.3) * 0.2, 90, 2200)
    whoosh = bp(noise(n), 200, 3500) * prog ** 1.5 * 0.5
    sub = np.sin(phase * 0.5) * 0.5 * prog
    env = np.clip(t / 0.05, 0, 1) * np.clip((t[-1] - t) / 0.2, 0, 1) * (0.2 + 0.8 * prog)
    x = sat((turbine + whoosh + sub) * env, 1.5)
    save("Tank_Engine_Spool", reverb(x, 0.4, 0.12))


def hover_loop():
    dur = 2.0
    n = int(SR * dur)
    t = np.arange(n) / SR
    hum = (np.sin(2 * np.pi * 55 * t) * 0.55 + np.sin(2 * np.pi * 110 * t) * 0.4
           + np.sin(2 * np.pi * 165 * t) * 0.18 + np.sin(2 * np.pi * 220 * t) * 0.08)
    wob = 0.8 + 0.2 * np.sin(2 * np.pi * 4 * t)
    air = periodic_noise(n, 120, 2800, 1.1) * 0.35
    whine = np.sin(2 * np.pi * 880 * t + 0.6 * np.sin(2 * np.pi * 3 * t)) * 0.04
    x = sat(hum * wob * 0.6 + air + whine, 1.3)
    save("Tank_Hover_Loop", x, peak=0.7, loop=True)


def laser_bolt():
    t = T(0.3)
    n = len(t)
    pew = sweep(t, 1900, 220, 0.5) * ad(t, 0.001, 0.07)
    pew2 = sweep(t, 3800, 440, 0.5) * ad(t, 0.001, 0.04) * 0.3
    zap = bp(noise(n), 2500, 11000) * ad(t, 0.0005, 0.02) * 0.5
    thump = np.sin(2 * np.pi * np.cumsum(140 * np.exp(-t * 30) + 55) / SR) * ad(t, 0.001, 0.05) * 0.6
    x = sat(pew + pew2 + zap + thump, 1.6)
    save("Tank_Laser_Bolt", reverb(x, 0.25, 0.12))


def armor_hit():
    t = T(0.5)
    n = len(t)
    ring = np.zeros(n)
    for p, a in ((1.0, 1.0), (2.76, 0.6), (5.4, 0.35), (8.9, 0.2)):
        ring += a * np.sin(2 * np.pi * 380 * p * t + rng.uniform(0, 6.28)) * np.exp(-t / (0.22 / (0.5 + 0.5 * p)))
    thud = lp(noise(n), 300) * ad(t, 0.0005, 0.05) * 1.2
    tick = hp(noise(n), 3000) * ad(t, 0.0003, 0.008) * 0.6
    x = sat(ring * 0.35 + thud + tick, 1.7)
    save("Tank_Armor_Hit", reverb(x, 0.3, 0.1))


def shell_explosion():
    t = T(2.2)
    n = len(t)
    crack = bp(noise(n), 600, 9000) * ad(t, 0.0005, 0.03)
    boom = np.sin(2 * np.pi * np.cumsum(26 + 80 * np.exp(-t * 11)) / SR) * ad(t, 0.003, 0.45)
    body = lp(noise(n), 700) * ad(t, 0.001, 0.2)
    roar = bp(noise(n), 100, 1500) * ad(t - 0.03, 0.05, 0.5) * (t > 0.03)
    debris = crackle(n, 0.01) * ad(t - 0.1, 0.01, 0.6) * (t > 0.1) * 2.0
    rumble = lp(noise(n), 90) * ad(t - 0.05, 0.1, 0.9) * (t > 0.05)
    x = sat(crack * 0.8 + boom * 1.5 + body + roar * 0.7 + debris * 0.35 + rumble * 1.0, 1.8)
    save("Tank_Shell_Explosion", reverb(x, 1.1, 0.32, 2000))


def death_explosion():
    dur = 5.0
    t = T(dur)
    n = len(t)

    def blast(at, size, low):
        tt = np.clip(t - at, 0, None)
        m = (t >= at).astype(float)
        c = bp(noise(n), 500, 8000) * ad(tt, 0.0005, 0.04 * size) * m
        b = np.sin(2 * np.pi * np.cumsum((22 + low) + 70 * size * np.exp(-tt * 9)) / SR) * ad(tt, 0.003, 0.5 * size) * m
        bd = lp(noise(n), 600) * ad(tt, 0.001, 0.22 * size) * m
        return c * 0.8 + b * 1.5 + bd

    x = blast(0.0, 1.0, 4)
    for at, size in ((0.18, 0.5), (0.34, 0.45), (0.52, 0.55), (0.7, 0.4)):    # chained pad detonations
        x += blast(at, size, 14) * 0.7
    x += blast(1.0, 1.7, 0) * 1.25                                              # main magazine blast
    fireball = bp(noise(n), 80, 1800) * ad(t - 1.0, 0.25, 1.1) * (t > 1.0) * 0.9
    rumble = lp(noise(n), 100) * ad(t - 0.2, 0.3, 1.8) * (t > 0.2) * 1.4
    debris = crackle(n, 0.012) * ad(t - 1.2, 0.05, 1.3) * (t > 1.2) * 2.0
    clangs = np.zeros(n)
    for at in (1.7, 2.1, 2.45, 2.9):
        tt = np.clip(t - at, 0, None)
        clangs += np.sin(2 * np.pi * rng.uniform(220, 520) * tt) * np.exp(-tt / 0.18) * (t > at) * 0.18
    x = sat(x * 0.55 + fireball + rumble + debris * 0.4 + clangs, 1.7)
    save("Tank_Death_Explosion", reverb(x, 1.6, 0.38, 1800))


if __name__ == "__main__":
    cannon_blast()
    cannon_charge()
    engine_spool()
    hover_loop()
    laser_bolt()
    armor_hit()
    shell_explosion()
    death_explosion()
