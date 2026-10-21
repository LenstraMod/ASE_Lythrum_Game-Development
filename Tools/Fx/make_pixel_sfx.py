"""
Synthesises 8-bit style combat sounds (square-wave sweeps, noise bursts, bit-crush), like an old sfxr:

    python Tools/Fx/make_pixel_sfx.py

writes Assets/Audio/Combat/pixel_*.wav. Nothing is sampled, so there is no licence to worry about.
In Unity: Lythrum > Setup > Audio (build SFX library) maps them to the slash / area slash / hit sounds.
"""
import os
import wave

import numpy as np

SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Audio", "Combat"))


def n_samples(seconds):
    return int(SR * seconds)


def sweep(f0, f1, seconds, curve=1.0):
    t = np.linspace(0.0, 1.0, n_samples(seconds)) ** curve
    return f0 + (f1 - f0) * t


def square(freq, duty=0.5):
    phase = np.cumsum(freq) / SR
    return np.where((phase % 1.0) < duty, 1.0, -1.0)


def triangle(freq):
    phase = np.cumsum(freq) / SR
    return 2.0 * np.abs(2.0 * (phase % 1.0) - 1.0) - 1.0


def noise(seconds, rate, rng):
    """Sample-and-hold noise: `rate` new random values per second (lower = crunchier)."""
    n = n_samples(seconds)
    step = max(1, int(SR / rate))
    values = rng.uniform(-1.0, 1.0, n // step + 2)
    return np.repeat(values, step)[:n]


def envelope(n, attack=0.003, power=2.5):
    t = np.linspace(0.0, 1.0, n)
    a = np.minimum(1.0, t * n / max(1.0, attack * SR))
    return a * (1.0 - t) ** power


def crush(x, bits):
    levels = 2 ** (bits - 1)
    return np.round(x * levels) / levels


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def save(name, x, volume=0.85):
    x = x / max(1e-6, np.max(np.abs(x))) * volume
    data = (np.clip(x, -1.0, 1.0) * 32767).astype("<i2")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())
    print("wrote", name, "%.2fs" % (len(x) / SR))


def slash(start, seconds, seed, bits=6, grit=0.55):
    """A fast downward whoosh: square sweep + a puff of noise."""
    rng = np.random.default_rng(seed)
    n = n_samples(seconds)
    tone = square(sweep(start, start * 0.14, seconds, 0.8), 0.35)
    puff = noise(seconds, 9000, rng)
    return crush((tone * (1 - grit) + puff * grit) * envelope(n, 0.002, 2.2), bits)


def main():
    rng = np.random.default_rng(7)

    for i, start in enumerate((1700, 2000, 2350), 1):
        save("pixel_slash_%d" % i, slash(start, 0.13, seed=i))
    save("pixel_slash_heavy", mix(slash(1000, 0.22, seed=11, bits=5, grit=0.5),
                                  np.concatenate([np.zeros(n_samples(0.05)), slash(1500, 0.15, seed=12) * 0.6])))

    # Q: a fiery sweep up then down, crackling noise and a low whoomp underneath
    n = n_samples(0.5)
    up = np.concatenate([sweep(250, 1300, 0.15, 1.4), sweep(1300, 160, 0.35, 0.7)])
    flame = square(up, 0.25) * envelope(len(up), 0.004, 1.6)
    crackle = noise(0.5, 3500, rng) * (rng.random(n) < 0.35) * envelope(n, 0.01, 1.2)
    whoomp = triangle(sweep(180, 45, 0.45, 0.6)) * envelope(n_samples(0.45), 0.004, 1.4)
    save("pixel_fire_slash", crush(mix(flame * 0.7, crackle * 0.8, whoomp * 0.9), 5))

    # hits landing on something
    for i, f0 in enumerate((260, 300, 220), 1):
        n = n_samples(0.09)
        thump = square(sweep(f0, f0 * 0.4, 0.09), 0.5) * envelope(n, 0.001, 2.0)
        crack = noise(0.06, 7000, rng) * envelope(n_samples(0.06), 0.001, 2.5)
        save("pixel_hit_%d" % i, crush(mix(thump * 0.8, crack * 0.7), 4))
    n = n_samples(0.18)
    thump = square(sweep(150, 38, 0.18, 0.7), 0.5) * envelope(n, 0.001, 1.6)
    crack = noise(0.12, 5000, rng) * envelope(n_samples(0.12), 0.001, 2.0)
    save("pixel_hit_heavy", crush(mix(thump * 0.9, crack * 0.8), 4))


if __name__ == "__main__":
    main()
