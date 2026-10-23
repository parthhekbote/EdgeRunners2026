"""Synthesises the Titan-mech sound set into Assets/Resources/MechSfx (loaded by MechAudioSynth).

Compared with the old in-engine synth (a sine plus white noise per clip) these have layered, filtered bodies:
sub thump + gritty mid impact + inharmonic metal ring + debris + a short room tail, servo grind with
rising pitch, valve hiss with band-passed shaping, and seamless reactor / thruster loops.

Run:  python Tools/SfxGen/make_mech_sfx.py
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "MechSfx")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1337)


# ── helpers ────────────────────────────────────────────────────────────────

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


def env_ad(t, attack, decay):
    """Fast attack, exponential decay."""
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-t / decay)


def sat(x, drive=2.0):
    return np.tanh(x * drive) / np.tanh(drive)


def reverb(x, tail=0.6, wet=0.25, bright=3500):
    n = int(SR * tail)
    t = np.arange(n) / SR
    ir = lp(noise(n), bright) * np.exp(-t / (tail * 0.28))
    ir[0] = 0
    wetsig = signal.fftconvolve(x, ir)
    wetsig = wetsig / (np.max(np.abs(wetsig)) + 1e-9) * np.max(np.abs(x)) * wet
    out = np.zeros(len(wetsig))
    out[: len(x)] = x
    return out + wetsig


def metal_ring(t, base, decay, partials=(1.0, 2.76, 5.40, 8.93), amps=(1.0, 0.6, 0.35, 0.2)):
    out = np.zeros_like(t)
    for p, a in zip(partials, amps):
        out += a * np.sin(2 * np.pi * base * p * t + rng.uniform(0, 6.28)) * np.exp(-t / (decay / (0.6 + 0.4 * p)))
    return out


def periodic_noise(n, lo, hi):
    """Band-limited noise that tiles seamlessly (built in the frequency domain)."""
    spec = np.zeros(n // 2 + 1, dtype=complex)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    mask = (freqs >= lo) & (freqs <= hi)
    spec[mask] = np.exp(1j * rng.uniform(0, 2 * np.pi, mask.sum())) * (1.0 / np.sqrt(np.maximum(freqs[mask], 1)))
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def save(name, x, peak=0.9, fade_ms=3, loop=False):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x) if not loop else x
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    if not loop:
        f = int(SR * fade_ms / 1000)
        x[:f] *= np.linspace(0, 1, f)
        tail = int(SR * 0.02)
        x[-tail:] *= np.linspace(1, 0, tail)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.normpath(path), "%.2fs" % (len(x) / SR))


# ── sounds ─────────────────────────────────────────────────────────────────

def stomp(i):
    dur = 0.5
    t = T(dur)
    f = 30 + 55 * np.exp(-t * (28 + i * 4))                       # pitch-dropping sub
    phase = 2 * np.pi * np.cumsum(f) / SR
    sub = np.sin(phase) * env_ad(t, 0.002, 0.17)
    body = lp(noise(len(t)), 220 + i * 40) * env_ad(t, 0.001, 0.07) * 1.2   # chassis thud
    grit = bp(noise(len(t)), 900, 5200) * env_ad(t, 0.0005, 0.03) * 0.55    # gravel / deck crunch
    clank = metal_ring(t, 210 + i * 35, 0.18) * env_ad(t, 0.0008, 0.12) * 0.28
    plates = hp(noise(len(t)), 2500) * env_ad(t - 0.035, 0.002, 0.05) * 0.18 * (t > 0.035)
    x = sat(sub * 1.4 + body + grit + clank + plates, 1.6)
    x = reverb(x, 0.45, 0.22, 2500)
    save("Mech_Stomp_%02d" % (i + 1), x)


def hydraulic(i):
    dur = 0.55 + 0.06 * i
    t = T(dur)
    prog = t / dur
    f0 = 85 + 15 * i
    f = f0 * (1 + 1.1 * prog ** 1.5)                           # servo pitching up under load
    phase = 2 * np.pi * np.cumsum(f) / SR
    saw = signal.sawtooth(phase) * 0.5 + signal.square(phase * 2.01, 0.3) * 0.2
    grind = bp(saw, 180, 1900) * np.sin(np.pi * np.clip(prog, 0, 1)) ** 0.7
    whine = np.sin(2 * np.pi * np.cumsum(900 + 700 * prog) / SR) * 0.05 * np.sin(np.pi * prog)
    hiss = bp(noise(len(t)), 1800, 7500) * 0.28 * env_ad(t, 0.02, 0.18)
    clunk_t = T(0.12)
    clunk = np.zeros_like(t)
    c = (lp(noise(len(clunk_t)), 450) + metal_ring(clunk_t, 300, 0.07) * 0.4) * env_ad(clunk_t, 0.001, 0.04)
    start = int(SR * (dur - 0.14))
    clunk[start:start + len(c)] += c * 0.9
    x = sat(grind * 1.2 + whine + hiss + clunk, 1.5)
    save("Mech_Hydraulic_%02d" % (i + 1), reverb(x, 0.3, 0.12))


def pneumatic(i):
    dur = 0.6 + 0.1 * i
    t = T(dur)
    n = noise(len(t))
    hissy = bp(n, 1500 + i * 400, 9000) * env_ad(t, 0.012, 0.16 + 0.04 * i)
    puff = lp(n, 700) * env_ad(t, 0.004, 0.06) * 0.9
    tsss = hp(noise(len(t)), 5000) * env_ad(t - 0.05, 0.05, 0.22) * 0.35 * (t > 0.05)
    x = hissy + puff + tsss
    save("Mech_Pneumatic_%02d" % (i + 1), reverb(x, 0.35, 0.15))


def reactor_loop():
    dur = 2.0
    n = int(SR * dur)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for hz, a in ((40, 0.6), (80, 0.45), (120, 0.28), (160, 0.14), (240, 0.07)):   # integer cycles over 2 s
        x += a * np.sin(2 * np.pi * hz * t)
    chug = 0.78 + 0.22 * np.sin(2 * np.pi * 5 * t)                                 # 5 Hz piston chug
    gear = 0.06 * np.sin(2 * np.pi * 330 * t + 1.7 * np.sin(2 * np.pi * 2 * t))
    air = periodic_noise(n, 80, 1400) * 0.22
    whine = 0.03 * np.sin(2 * np.pi * 1210 * t) * (0.7 + 0.3 * np.sin(2 * np.pi * 3 * t))
    out = sat((x * chug) * 0.55 + gear + air + whine, 1.3)
    save("Mech_Reactor_Loop", out, peak=0.7, loop=True)


def joint_strain(i):
    dur = 0.6
    t = T(dur)
    prog = t / dur
    f = (150 + 45 * i) * (1 - 0.55 * prog) * (1 + 0.03 * np.sin(2 * np.pi * 23 * t))
    phase = 2 * np.pi * np.cumsum(f) / SR
    creak = signal.sawtooth(phase) * 0.6
    creak = bp(creak, 300, 2200) * np.sin(np.pi * prog) ** 0.8
    rattle = hp(noise(len(t)), 1800) * (0.5 + 0.5 * np.sin(2 * np.pi * 42 * t)) ** 3 * 0.12 * np.sin(np.pi * prog)
    low = np.sin(phase * 0.5) * 0.5 * np.sin(np.pi * prog)
    save("Mech_JointStrain_%02d" % (i + 1), reverb(sat(creak + rattle + low, 1.4), 0.3, 0.1))


def thruster_ignition():
    dur = 1.5
    t = T(dur)
    n = noise(len(t))
    cutoff = 250 + 5200 * (1 - np.exp(-t * 4.0))
    out = np.zeros(len(t))
    # sweep the low-pass: filter in blocks with rising cutoff
    block = 512
    state = None
    for s in range(0, len(t), block):
        fc = float(np.clip(cutoff[s], 100, 12000))
        sos = signal.butter(2, fc, "low", fs=SR, output="sos")
        if state is None:
            state = np.zeros((sos.shape[0], 2))
        seg, state = signal.sosfilt(sos, n[s:s + block], zi=state)
        out[s:s + block] = seg
    roar = out * np.clip(t / 0.08, 0, 1) * np.exp(-t / 1.1)
    boom = np.sin(2 * np.pi * np.cumsum(28 + 55 * np.exp(-t * 6)) / SR) * env_ad(t, 0.01, 0.45) * 1.4
    crackle = hp(noise(len(t)), 3000) * (rng.random(len(t)) > 0.985) * env_ad(t, 0.01, 0.5) * 2
    x = sat(roar * 1.6 + boom + crackle * 0.3, 1.6)
    save("Mech_ThrusterIgnition", reverb(x, 0.7, 0.2))


def thruster_loop():
    dur = 2.0
    n = int(SR * dur)
    t = np.arange(n) / SR
    roar = periodic_noise(n, 60, 3500) * 0.9
    sub = np.sin(2 * np.pi * 44 * t) * 0.35 + np.sin(2 * np.pi * 88 * t) * 0.18
    flutter = 0.82 + 0.18 * np.sin(2 * np.pi * 14 * t)
    out = sat((roar * flutter + sub) * 0.7, 1.4)
    save("Mech_ThrusterLoop", out, peak=0.7, loop=True)


def missile_launch(i):
    dur = 1.1
    t = T(dur)
    pop = (lp(noise(len(t)), 600) + np.sin(2 * np.pi * 120 * t)) * env_ad(t, 0.001, 0.05)
    burn_n = bp(noise(len(t)), 500 + i * 120, 6500)
    burn = burn_n * np.clip(t / 0.12, 0, 1) * np.exp(-t / 0.55)
    swoosh = np.sin(2 * np.pi * np.cumsum(180 + 900 * (t / dur) ** 1.2) / SR) * 0.12 * np.exp(-t / 0.5)
    crackle = hp(noise(len(t)), 4000) * (rng.random(len(t)) > 0.97) * 0.4 * np.exp(-t / 0.5)
    x = sat(pop * 1.2 + burn * 0.9 + swoosh + crackle, 1.5)
    save("Mech_MissileLaunch_%02d" % (i + 1), reverb(x, 0.6, 0.2))


def impact_heavy(i):
    dur = 1.1
    t = T(dur)
    body = np.sin(2 * np.pi * np.cumsum(32 + 70 * np.exp(-t * 14)) / SR) * env_ad(t, 0.001, 0.22)
    crunch = bp(noise(len(t)), 300, 4500) * env_ad(t, 0.0005, 0.07) * 1.1
    debris = hp(noise(len(t)), 1500) * (rng.random(len(t)) > 0.994) * env_ad(t - 0.05, 0.002, 0.3) * 2.0 * (t > 0.05)
    plate = metal_ring(t, 170 + 45 * i, 0.5) * env_ad(t, 0.001, 0.4) * 0.35
    x = sat(body * 1.5 + crunch + debris * 0.3 + plate, 1.7)
    save("Mech_ImpactHeavy_%02d" % (i + 1), reverb(x, 0.8, 0.28))


def seismic_slam(i):
    dur = 1.8
    t = T(dur)
    sub = np.sin(2 * np.pi * np.cumsum(24 + 60 * np.exp(-t * 7)) / SR) * env_ad(t, 0.003, 0.65) * 1.6
    shock = lp(noise(len(t)), 900) * env_ad(t, 0.001, 0.16) * 1.4
    crash = bp(noise(len(t)), 1200, 8000) * env_ad(t, 0.001, 0.08) * 0.7
    rumble = lp(noise(len(t)), 120) * env_ad(t - 0.04, 0.05, 0.7) * 1.4 * (t > 0.04)
    ping = metal_ring(t, 420 + i * 60, 0.35) * env_ad(t, 0.001, 0.25) * 0.2
    debris = hp(noise(len(t)), 2000) * (rng.random(len(t)) > 0.996) * env_ad(t - 0.12, 0.002, 0.7) * (t > 0.12)
    x = sat(sub + shock + crash * 0.6 + rumble + ping + debris * 0.4, 1.8)
    save("Mech_SeismicSlam_%02d" % (i + 1), reverb(x, 1.1, 0.3, 2000))


def blade_cleave(i):
    dur = 0.8
    t = T(dur)
    prog = t / dur
    swell = np.sin(np.pi * np.clip(prog ** 0.8, 0, 1)) ** 1.5
    n = noise(len(t))
    air = bp(n, 300 + 300 * i, 5200) * swell
    low = lp(n, 220) * swell * 0.9
    ring = np.sin(2 * np.pi * np.cumsum(500 + 1700 * prog ** 2) / SR) * 0.1 * swell
    hit = metal_ring(t - 0.5, 900, 0.2) * env_ad(np.clip(t - 0.5, 0, None), 0.001, 0.1) * 0.12 * (t > 0.5)
    x = sat(air * 0.9 + low + ring + hit, 1.3)
    save("Mech_TitanBladeCleave_%02d" % (i + 1), reverb(x, 0.4, 0.12))


def gatling(i):
    dur = 0.28
    t = T(dur)
    crack = bp(noise(len(t)), 1500, 9000) * env_ad(t, 0.0003, 0.012)
    punch = np.sin(2 * np.pi * np.cumsum(60 + 160 * np.exp(-t * 90)) / SR) * env_ad(t, 0.0005, 0.04)
    thunk = lp(noise(len(t)), 500) * env_ad(t, 0.0008, 0.03)
    clack = metal_ring(t, 1250 + 150 * i, 0.04) * env_ad(t, 0.0005, 0.03) * 0.15
    x = sat(crack * 0.9 + punch * 1.1 + thunk * 0.9 + clack, 2.0)
    save("Mech_Gatling_%02d" % (i + 1), reverb(x, 0.25, 0.14))


def overdrive_alarm():
    dur = 1.6
    t = T(dur)
    pulse = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 2.5 * t))
    pulse = lp(pulse, 30, 1)
    f = 196 * (1 + 0.5 * pulse)                       # two-tone klaxon
    phase = 2 * np.pi * np.cumsum(f) / SR
    horn = signal.sawtooth(phase) * 0.5 + signal.sawtooth(phase * 1.005) * 0.4 + np.sin(phase * 0.5) * 0.4
    horn = lp(horn, 2400, 2)
    env = np.clip(t / 0.05, 0, 1) * np.clip((dur - t) / 0.25, 0, 1)
    x = sat(horn * env, 2.2)
    save("Mech_OverdriveAlarm", reverb(x, 0.6, 0.18))


if __name__ == "__main__":
    for i in range(3):
        stomp(i)
        hydraulic(i)
        missile_launch(i) if i < 3 else None
    for i in range(2):
        pneumatic(i)
        joint_strain(i)
        impact_heavy(i)
        seismic_slam(i)
        blade_cleave(i)
        gatling(i)
    reactor_loop()
    thruster_ignition()
    thruster_loop()
    overdrive_alarm()
