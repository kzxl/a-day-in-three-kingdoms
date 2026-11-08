#!/usr/bin/env python3
"""
Deterministic procedural audio synthesizer for Three Kingdoms upgrades (Prompt 27 - Part B).
Generates ancient Chinese/Vietnamese traditional percussion, string, weapon combat SFX
(22050 Hz, mono, 16-bit WAV, each < 1.5s) and a seamless loopable market ambience
(assets/audio/ambience/market.ogg via ffmpeg).

Timbre palette:
- Phách (hardwood/bamboo clapper)
- Mõ (carved hollow temple woodblock)
- Chiêng / Đại la (bronze gong with nonlinear warm pitch-bloom)
- Khánh (L-shaped limestone/jade chime) & Biên chung (bronze bell)
- Đại cổ (war drum with coupled membrane pitch-drop and wooden shell resonance)
- Dây gảy (plucked silk-string bow / cổ cầm via damped modal string synthesis)
No chiptune waveforms (no square/saw/triangle bleeps).
"""

from __future__ import annotations

import argparse
import base64
import html
import math
import shutil
import subprocess
import tempfile
import wave
from pathlib import Path
import numpy as np

SR = 22050
TAU = 2.0 * math.pi


def make_time(duration: float) -> np.ndarray:
    n = int(round(duration * SR))
    return np.arange(n, dtype=np.float64) / SR


def env_ad(t: np.ndarray, attack: float, decay: float, t0: float = 0.0, curve: float = 1.0) -> np.ndarray:
    u = t - t0
    out = np.zeros_like(t)
    mask = u >= 0.0
    if not np.any(mask):
        return out
    um = u[mask]
    att = np.minimum(1.0, um / max(1e-5, attack)) ** curve
    dec = np.exp(-np.maximum(0.0, um - attack) / max(1e-4, decay))
    out[mask] = att * dec
    return out


def biquad_bandpass(x: np.ndarray, fc: float, q: float) -> np.ndarray:
    """Direct-form II transposed 2nd-order bandpass filter."""
    fc = float(np.clip(fc, 30.0, SR * 0.45))
    q = max(0.25, float(q))
    w0 = TAU * fc / SR
    alpha = math.sin(w0) / (2.0 * q)
    b0 = alpha
    b1 = 0.0
    b2 = -alpha
    a0 = 1.0 + alpha
    a1 = -2.0 * math.cos(w0)
    a2 = 1.0 - alpha

    b0 /= a0
    b1 /= a0
    b2 /= a0
    a1 /= a0
    a2 /= a0

    y = np.zeros_like(x, dtype=np.float64)
    z1 = 0.0
    z2 = 0.0
    for i in range(len(x)):
        xi = float(x[i])
        yi = b0 * xi + z1
        z1 = b1 * xi - a1 * yi + z2
        z2 = b2 * xi - a2 * yi
        y[i] = yi
    return y


def biquad_lowpass(x: np.ndarray, fc: float, q: float = 0.707) -> np.ndarray:
    fc = float(np.clip(fc, 40.0, SR * 0.45))
    w0 = TAU * fc / SR
    alpha = math.sin(w0) / (2.0 * q)
    cosw = math.cos(w0)
    b0 = (1.0 - cosw) * 0.5
    b1 = 1.0 - cosw
    b2 = (1.0 - cosw) * 0.5
    a0 = 1.0 + alpha
    a1 = -2.0 * cosw
    a2 = 1.0 - alpha

    b0 /= a0
    b1 /= a0
    b2 /= a0
    a1 /= a0
    a2 /= a0

    y = np.zeros_like(x, dtype=np.float64)
    z1 = 0.0
    z2 = 0.0
    for i in range(len(x)):
        xi = float(x[i])
        yi = b0 * xi + z1
        z1 = b1 * xi - a1 * yi + z2
        z2 = b2 * xi - a2 * yi
        y[i] = yi
    return y


def swept_wind_whoosh(
    t: np.ndarray,
    rng: np.random.Generator,
    t0: float,
    dur: float,
    f_start: float,
    f_peak: float,
    f_end: float,
    peak_pos: float = 0.35,
    bw_ratio: float = 0.28,
) -> np.ndarray:
    """Synthesizes natural air-cutting whoosh via time-varying resonant formant filtering of noise."""
    n = len(t)
    raw = rng.standard_normal(n)
    out = np.zeros(n, dtype=np.float64)
    i0 = max(0, int(t0 * SR))
    i1 = min(n, int((t0 + dur) * SR))
    if i1 <= i0:
        return out

    z1 = 0.0
    z2 = 0.0
    for i in range(i0, i1):
        u = (i - i0) / max(1, i1 - i0)
        if u < peak_pos:
            k = u / max(1e-4, peak_pos)
            fc = f_start + (f_peak - f_start) * (1.0 - (1.0 - k) ** 2)
            amp = math.sin(0.5 * math.pi * k)
        else:
            k = (u - peak_pos) / max(1e-4, 1.0 - peak_pos)
            fc = f_peak + (f_end - f_peak) * (k ** 1.3)
            amp = math.cos(0.5 * math.pi * k) ** 1.6

        fc = float(np.clip(fc, 60.0, SR * 0.43))
        q = max(1.2, 1.0 / max(0.08, bw_ratio))
        w0 = TAU * fc / SR
        alpha = math.sin(w0) / (2.0 * q)
        b0 = alpha / (1.0 + alpha)
        b2 = -b0
        a1 = (-2.0 * math.cos(w0)) / (1.0 + alpha)
        a2 = (1.0 - alpha) / (1.0 + alpha)

        xi = float(raw[i])
        yi = b0 * xi + z1
        z1 = -a1 * yi + z2
        z2 = b2 * xi - a2 * yi
        out[i] = yi * amp
    return out


def modal_resonator(
    t: np.ndarray,
    f0: float,
    modes: list[tuple[float, float, float]],
    t0: float = 0.0,
    attack: float = 0.002,
    pitch_bend_hz: float = 0.0,
    bend_decay: float = 0.04,
) -> np.ndarray:
    """
    Sum of damped modal partials:
    modes = [(freq_ratio, relative_amp, decay_seconds), ...]
    """
    u = t - t0
    mask = u >= 0.0
    out = np.zeros_like(t)
    if not np.any(mask):
        return out
    um = u[mask]
    att = np.minimum(1.0, um / max(1e-5, attack))
    sig = np.zeros_like(um)
    for ratio, amp, dec in modes:
        freq = f0 * ratio
        if freq >= SR * 0.47:
            continue
        phase = TAU * freq * um
        if abs(pitch_bend_hz) > 1e-4:
            phase += TAU * (pitch_bend_hz * ratio) * bend_decay * (1.0 - np.exp(-um / max(1e-4, bend_decay)))
        sig += amp * np.sin(phase) * np.exp(-um / max(1e-4, dec))
    out[mask] = sig * att
    return out


def synth_war_drum_hit(
    t: np.ndarray,
    rng: np.random.Generator,
    t0: float = 0.0,
    f_base: float = 72.0,
    pitch_drop: float = 42.0,
    decay: float = 0.38,
    rim_mix: float = 0.25,
) -> np.ndarray:
    """Chinese/Vietnamese war drum (Đại cổ): circular membrane modes + wooden shell + mallet skin transient."""
    # Circular membrane modes (01, 11, 21, 02, 31)
    drum_modes = [
        (1.00, 1.00, decay),
        (1.59, 0.48, decay * 0.62),
        (2.14, 0.28, decay * 0.45),
        (2.30, 0.22, decay * 0.36),
        (2.65, 0.12, decay * 0.25),
    ]
    body = modal_resonator(
        t,
        f0=f_base,
        modes=drum_modes,
        t0=t0,
        attack=0.0025,
        pitch_bend_hz=pitch_drop,
        bend_decay=0.045,
    )
    # Wooden barrel cavity resonance
    wood = modal_resonator(
        t,
        f0=f_base * 2.45,
        modes=[(1.0, 0.32, 0.065), (1.42, 0.20, 0.045), (1.88, 0.14, 0.03)],
        t0=t0,
        attack=0.001,
    )
    # Mallet impact transient
    noise = biquad_bandpass(rng.standard_normal(len(t)), f_base * 4.2, 1.6)
    trans = noise * env_ad(t, 0.001, 0.018, t0=t0) * rim_mix
    return body + wood * rim_mix + trans


def synth_bronze_gong(
    t: np.ndarray,
    rng: np.random.Generator,
    t0: float = 0.0,
    f0: float = 196.0,
    decay: float = 0.95,
    bloom: float = 0.18,
) -> np.ndarray:
    """
    Ancient bronze gong (Chiêng đồng / Đại la):
    Warm fundamental + inharmonic plate partials + characteristic nonlinear bloom after strike.
    """
    u = t - t0
    mask = u >= 0.0
    out = np.zeros_like(t)
    if not np.any(mask):
        return out
    um = u[mask]

    # Low-mid bronze plate modes
    base_modes = [
        (1.000, 0.95, decay),
        (1.498, 0.55, decay * 0.85),
        (1.985, 0.42, decay * 0.72),
        (2.440, 0.34, decay * 0.64),
        (2.975, 0.25, decay * 0.55),
        (3.560, 0.18, decay * 0.48),
    ]
    # Shimmering upper modes that bloom slightly after initial mallet hit
    bloom_modes = [
        (4.18, 0.15, decay * 0.45),
        (5.05, 0.12, decay * 0.38),
        (6.12, 0.09, decay * 0.32),
        (7.34, 0.06, decay * 0.26),
    ]

    sig = np.zeros_like(um)
    att_main = np.minimum(1.0, um / 0.006)
    for ratio, amp, dec in base_modes:
        freq = f0 * ratio
        if freq >= SR * 0.46:
            continue
        # Subtle warm pitch swell characteristic of bronze gongs
        swell = bloom * ratio * np.sin(TAU * 2.2 * um) * np.exp(-um / 0.35)
        phase = TAU * freq * um + swell
        sig += amp * np.sin(phase) * np.exp(-um / dec) * att_main

    att_bloom = (1.0 - np.exp(-um / 0.045)) ** 2
    for ratio, amp, dec in bloom_modes:
        freq = f0 * ratio
        if freq >= SR * 0.46:
            continue
        phase = TAU * freq * um + 0.4 * ratio * np.sin(TAU * 4.5 * um) * np.exp(-um / 0.25)
        sig += amp * np.sin(phase) * np.exp(-um / dec) * att_bloom

    # Soft padded mallet thump + metallic wash
    shimmer_noise = biquad_bandpass(rng.standard_normal(len(t)), f0 * 4.8, 3.2)[mask]
    sig += shimmer_noise * 0.08 * att_bloom * np.exp(-um / (decay * 0.35))
    out[mask] = sig
    return out


def synth_stone_khanh(
    t: np.ndarray,
    f0: float,
    t0: float = 0.0,
    decay: float = 0.65,
) -> np.ndarray:
    """
    Ancient L-shaped stone/jade chime (Khánh đá / Ngọc khánh) & Biên chung (bronze bell):
    Pure, crystalline modal partials (1 : 1.50 : 2.28 : 3.01 : 4.12).
    """
    modes = [
        (1.000, 1.00, decay),
        (1.502, 0.58, decay * 0.78),
        (2.284, 0.36, decay * 0.56),
        (3.015, 0.22, decay * 0.42),
        (4.125, 0.12, decay * 0.28),
    ]
    return modal_resonator(t, f0=f0, modes=modes, t0=t0, attack=0.0015)


def synth_phach(t: np.ndarray, rng: np.random.Generator, t0: float = 0.0, f0: float = 1120.0) -> np.ndarray:
    """Hardwood/bamboo clapper (Phách): very sharp woody transient + short hollow bar modes."""
    modes = [
        (1.00, 1.00, 0.028),
        (1.74, 0.68, 0.019),
        (2.62, 0.45, 0.014),
        (3.48, 0.25, 0.009),
    ]
    wood = modal_resonator(t, f0=f0, modes=modes, t0=t0, attack=0.0006, pitch_bend_hz=45.0, bend_decay=0.005)
    click = biquad_bandpass(rng.standard_normal(len(t)), f0 * 1.8, 2.2) * env_ad(t, 0.0004, 0.008, t0=t0)
    return wood + click * 0.45


def synth_mo(t: np.ndarray, rng: np.random.Generator, t0: float = 0.0, f0: float = 640.0) -> np.ndarray:
    """Hollow carved temple woodblock (Mõ): warm rounded cavity resonance + wooden mallet tap."""
    modes = [
        (1.00, 1.00, 0.065),
        (1.48, 0.46, 0.038),
        (2.19, 0.24, 0.022),
        (2.92, 0.12, 0.014),
    ]
    cavity = modal_resonator(t, f0=f0, modes=modes, t0=t0, attack=0.001, pitch_bend_hz=28.0, bend_decay=0.008)
    knock = biquad_bandpass(rng.standard_normal(len(t)), f0 * 1.35, 3.0) * env_ad(t, 0.0006, 0.012, t0=t0)
    return cavity + knock * 0.35


def synth_plucked_string(
    t: np.ndarray,
    f0: float,
    t0: float = 0.0,
    decay: float = 0.48,
    n_harmonics: int = 12,
    stiffness: float = 0.0004,
) -> np.ndarray:
    """Plucked silk string (Cổ cầm / Dây cung): harmonic series with natural high-frequency damping & wood body."""
    modes: list[tuple[float, float, float]] = []
    for k in range(1, n_harmonics + 1):
        ratio = k * math.sqrt(1.0 + stiffness * (k ** 2))
        if f0 * ratio >= SR * 0.46:
            break
        # Pluck position ~ 1/5 along string suppresses 5th harmonic naturally
        pluck_profile = abs(math.sin(k * math.pi * 0.22)) + 0.15
        amp = (1.0 / (k ** 1.15)) * pluck_profile
        h_decay = decay / (1.0 + 0.28 * (k - 1))
        modes.append((ratio, amp, h_decay))
    return modal_resonator(
        t,
        f0=f0,
        modes=modes,
        t0=t0,
        attack=0.0018,
        pitch_bend_hz=f0 * 0.03,
        bend_decay=0.025,
    )


def finalize_signal(sig: np.ndarray, peak: float = 0.88, fade_in: float = 0.003, fade_out: float = 0.025) -> np.ndarray:
    """DC removal, soft saturation, fade in/out, and 16-bit PCM conversion."""
    y = np.asarray(sig, dtype=np.float64).copy()
    y -= np.mean(y)

    n = len(y)
    fi = min(n // 4, max(1, int(fade_in * SR)))
    fo = min(n // 3, max(1, int(fade_out * SR)))
    if fi > 1:
        y[:fi] *= np.linspace(0.0, 1.0, fi)
    if fo > 1:
        y[-fo:] *= np.cos(np.linspace(0.0, 0.5 * math.pi, fo)) ** 2

    max_abs = float(np.max(np.abs(y)))
    if max_abs > 1e-8:
        y = y / max_abs
    # Gentle analog-style tanh warmth
    y = np.tanh(y * 1.15) / math.tanh(1.15)
    y *= peak
    return y


def write_wav(path: Path, samples: np.ndarray) -> float:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(np.round(samples * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())
    return len(pcm) / SR


# ---------------------------------------------------------------------------
# 15 Synthesized SFX generators (all < 1.5s, matched to pixelfx & scenes)
# ---------------------------------------------------------------------------

def gen_fx_sword_slash() -> np.ndarray:
    """
    Matches pixelfx/src/fx/melee/sword-slash.js (duration 0.75s, sweep 0.06s, echo 0.017s, hold 0.06s, trail 0.24s):
    Fast crescent air-cut whoosh + secondary echo + forged steel blade ring at t = 0.055s.
    """
    rng = np.random.default_rng(2701)
    t = make_time(0.68)
    whoosh_main = swept_wind_whoosh(t, rng, t0=0.0, dur=0.22, f_start=260.0, f_peak=1380.0, f_end=380.0, peak_pos=0.27, bw_ratio=0.30)
    whoosh_echo = swept_wind_whoosh(t, rng, t0=0.017, dur=0.19, f_start=340.0, f_peak=1650.0, f_end=480.0, peak_pos=0.30, bw_ratio=0.25)
    blade_modes = [
        (1.00, 0.85, 0.19),
        (1.68, 0.55, 0.15),
        (2.42, 0.40, 0.12),
        (3.19, 0.28, 0.09),
        (4.05, 0.18, 0.07),
    ]
    blade_ring = modal_resonator(t, f0=860.0, modes=blade_modes, t0=0.052, attack=0.001, pitch_bend_hz=40.0, bend_decay=0.015)
    glint = synth_stone_khanh(t, f0=1720.0, t0=0.060, decay=0.16) * 0.28
    sig = whoosh_main * 0.95 + whoosh_echo * 0.45 + blade_ring * 0.62 + glint
    return finalize_signal(sig, peak=0.88)


def gen_fx_thrust() -> np.ndarray:
    """
    Matches pixelfx/src/fx/melee/thrust.js (duration 0.75s, OUT = 0.07s, HOLD = 0.06s, PULL = 0.20s):
    Focused forward stab streak + wooden spear shaft snap + sharp point pierce ring at t = 0.07s.
    """
    rng = np.random.default_rng(2702)
    t = make_time(0.62)
    lunge_air = swept_wind_whoosh(t, rng, t0=0.0, dur=0.15, f_start=320.0, f_peak=1850.0, f_end=620.0, peak_pos=0.45, bw_ratio=0.22)
    shaft_snap = synth_phach(t, rng, t0=0.015, f0=520.0) * 0.55
    pierce_modes = [
        (1.00, 0.90, 0.20),
        (1.54, 0.62, 0.16),
        (2.31, 0.42, 0.12),
        (3.12, 0.25, 0.08),
    ]
    tip_strike = modal_resonator(t, f0=1080.0, modes=pierce_modes, t0=0.068, attack=0.0008, pitch_bend_hz=65.0, bend_decay=0.012)
    low_punch = synth_war_drum_hit(t, rng, t0=0.068, f_base=98.0, pitch_drop=38.0, decay=0.14, rim_mix=0.2) * 0.55
    sig = lunge_air * 0.85 + shaft_snap + tip_strike * 0.72 + low_punch
    return finalize_signal(sig, peak=0.88)


def gen_fx_spear_sweep() -> np.ndarray:
    """
    Matches pixelfx/src/fx/melee/spear-sweep.js (sweep 0.13s, hold 0.05s, trail 0.30s, ground dust & grit):
    Wide heavy polearm arc + floor dust/stone grit + heavy bronze halberd ring at t = 0.13s.
    """
    rng = np.random.default_rng(2703)
    t = make_time(1.08)
    wide_sweep = swept_wind_whoosh(t, rng, t0=0.0, dur=0.42, f_start=140.0, f_peak=680.0, f_end=180.0, peak_pos=0.31, bw_ratio=0.35)
    # Stone grit & dust kicked along the ground from t = 0.03 to 0.38s
    grit_bed = biquad_bandpass(rng.standard_normal(len(t)), 920.0, 1.4) * env_ad(t, 0.08, 0.22, t0=0.02)
    for k in range(8):
        gt = 0.035 + k * 0.028 + float(rng.uniform(-0.005, 0.005))
        gf = float(rng.uniform(750.0, 1450.0))
        grit_bed += synth_phach(t, rng, t0=gt, f0=gf) * float(rng.uniform(0.12, 0.24))
    halberd_clang = synth_bronze_gong(t, rng, t0=0.13, f0=310.0, decay=0.48, bloom=0.08) * 0.65
    heavy_thud = synth_war_drum_hit(t, rng, t0=0.125, f_base=76.0, pitch_drop=34.0, decay=0.26, rim_mix=0.25) * 0.65
    sig = wide_sweep * 0.95 + grit_bed * 0.48 + halberd_clang + heavy_thud
    return finalize_signal(sig, peak=0.88)


def gen_fx_arrow() -> np.ndarray:
    """
    Matches pixelfx/src/fx/projectiles/arrow.js (duration 0.60s, fluttering fletching & wind streaks):
    Plucked silk bowstring twang + wooden bow body + whistling feathered shaft in flight.
    """
    rng = np.random.default_rng(2704)
    t = make_time(0.60)
    # Silk bowstring release + wooden bow stave resonance
    bow_string = synth_plucked_string(t, f0=156.0, t0=0.0, decay=0.22, n_harmonics=10, stiffness=0.0008)
    bow_wood = synth_mo(t, rng, t0=0.002, f0=340.0) * 0.48
    # Arrow flight whistle with 30 Hz feather-flutter modulation
    whistle_raw = swept_wind_whoosh(t, rng, t0=0.035, dur=0.50, f_start=1680.0, f_peak=1420.0, f_end=920.0, peak_pos=0.25, bw_ratio=0.14)
    flutter = 0.78 + 0.22 * np.sin(TAU * 28.0 * t)
    sig = bow_string * 0.85 + bow_wood + whistle_raw * flutter * 0.78
    return finalize_signal(sig, peak=0.88)


def gen_fx_hit_flash() -> np.ndarray:
    """
    Matches pixelfx/src/fx/impacts/hit-flash.js (duration 0.50s, hot burst < 0.07s, cool off by 0.36s):
    Compact armor/body impact thud + hardwood phách crack + brief bronze lamellar plate rattle.
    """
    rng = np.random.default_rng(2705)
    t = make_time(0.48)
    body_thud = synth_war_drum_hit(t, rng, t0=0.0, f_base=92.0, pitch_drop=48.0, decay=0.16, rim_mix=0.35)
    armor_crack = synth_phach(t, rng, t0=0.003, f0=940.0) * 0.68
    plate_rattle = modal_resonator(
        t,
        f0=620.0,
        modes=[(1.0, 0.5, 0.09), (1.46, 0.38, 0.07), (2.18, 0.25, 0.05)],
        t0=0.022,
        attack=0.001,
    )
    sig = body_thud * 0.90 + armor_crack + plate_rattle * 0.45
    return finalize_signal(sig, peak=0.88)


def gen_fx_critical_hit() -> np.ndarray:
    """
    Matches pixelfx/src/fx/impacts/critical-hit.js (duration 0.90s, starburst 0..0.125s, broken ring 0.03..0.40s):
    Heavy thunderous war drum + bronze gong/blade crash + ringing jade/metal shards.
    """
    rng = np.random.default_rng(2706)
    t = make_time(0.92)
    deep_drum = synth_war_drum_hit(t, rng, t0=0.0, f_base=64.0, pitch_drop=46.0, decay=0.42, rim_mix=0.40)
    bronze_crash = synth_bronze_gong(t, rng, t0=0.015, f0=240.0, decay=0.62, bloom=0.22)
    wood_snap = synth_phach(t, rng, t0=0.002, f0=820.0) * 0.65
    # Tumbling shard glints matching bits.add(B.SHARD) at t = 0.08, 0.19, 0.31s
    shards = (
        synth_stone_khanh(t, f0=1320.0, t0=0.035, decay=0.28) * 0.35
        + synth_stone_khanh(t, f0=1760.0, t0=0.14, decay=0.22) * 0.22
        + synth_stone_khanh(t, f0=1568.0, t0=0.26, decay=0.18) * 0.16
    )
    sig = deep_drum * 0.95 + bronze_crash * 0.78 + wood_snap + shards
    return finalize_signal(sig, peak=0.90)


def gen_fx_crit_number() -> np.ndarray:
    """
    Matches pixelfx/src/fx/ui/crit-number.js (slam at 0.0s, label stamp at 0.09s, shine sweep 0.36..0.60s):
    Bright jade khánh & biên chung chime + plucked silk string harmonic shimmer.
    """
    rng = np.random.default_rng(2707)
    t = make_time(1.08)
    # Initial slam: clear stone khánh fifth (D6 + A6)
    khanh_1 = synth_stone_khanh(t, f0=1174.66, t0=0.0, decay=0.52) * 0.72
    khanh_2 = synth_stone_khanh(t, f0=1760.00, t0=0.012, decay=0.46) * 0.48
    # Label stamp at t = 0.09s
    stamp_clack = synth_phach(t, rng, t0=0.09, f0=1046.5) * 0.45
    khanh_stamp = synth_stone_khanh(t, f0=1318.51, t0=0.092, decay=0.45) * 0.55
    # Shine sweep across the number at t = 0.36s (pentatonic silk string + high chime glint)
    silk_pluck = synth_plucked_string(t, f0=587.33, t0=0.02, decay=0.55, n_harmonics=10) * 0.45
    shine_chime = synth_stone_khanh(t, f0=2349.32, t0=0.36, decay=0.42) * 0.36
    sig = khanh_1 + khanh_2 + stamp_clack + khanh_stamp + silk_pluck + shine_chime
    return finalize_signal(sig, peak=0.88)


def gen_duel_start() -> np.ndarray:
    """
    duel.start: Trống mở màn (two rapid rim/skin strikes leading into a deep resonant war drum boom).
    """
    rng = np.random.default_rng(2708)
    t = make_time(1.26)
    hit1 = synth_war_drum_hit(t, rng, t0=0.02, f_base=92.0, pitch_drop=28.0, decay=0.22, rim_mix=0.45) * 0.62
    hit2 = synth_war_drum_hit(t, rng, t0=0.19, f_base=96.0, pitch_drop=32.0, decay=0.24, rim_mix=0.45) * 0.74
    main_boom = synth_war_drum_hit(t, rng, t0=0.38, f_base=68.0, pitch_drop=45.0, decay=0.68, rim_mix=0.32) * 1.05
    bronze_accent = synth_bronze_gong(t, rng, t0=0.385, f0=220.0, decay=0.62, bloom=0.14) * 0.38
    sig = hit1 + hit2 + main_boom + bronze_accent
    return finalize_signal(sig, peak=0.90)


def gen_duel_win() -> np.ndarray:
    """
    duel.win: Chiêng thắng (majestic bronze gong with pentatonic biên chung & silk string chord).
    """
    rng = np.random.default_rng(2709)
    t = make_time(1.38)
    gong = synth_bronze_gong(t, rng, t0=0.02, f0=196.0, decay=1.05, bloom=0.22) * 0.95
    drum = synth_war_drum_hit(t, rng, t0=0.02, f_base=73.4, pitch_drop=32.0, decay=0.45, rim_mix=0.25) * 0.55
    bell_1 = synth_stone_khanh(t, f0=587.33, t0=0.04, decay=0.85) * 0.42
    bell_2 = synth_stone_khanh(t, f0=783.99, t0=0.18, decay=0.80) * 0.48
    bell_3 = synth_stone_khanh(t, f0=1174.66, t0=0.34, decay=0.72) * 0.40
    sig = gong + drum + bell_1 + bell_2 + bell_3
    return finalize_signal(sig, peak=0.90)


def gen_duel_lose() -> np.ndarray:
    """
    duel.lose: Trống trầm thua (heavy somber low war drum double-thud with dark decaying bronze).
    """
    rng = np.random.default_rng(2710)
    t = make_time(1.32)
    drum_1 = synth_war_drum_hit(t, rng, t0=0.02, f_base=62.0, pitch_drop=28.0, decay=0.48, rim_mix=0.18) * 0.92
    drum_2 = synth_war_drum_hit(t, rng, t0=0.42, f_base=52.0, pitch_drop=22.0, decay=0.68, rim_mix=0.14) * 0.85
    dark_gong = synth_bronze_gong(t, rng, t0=0.03, f0=130.81, decay=0.88, bloom=0.06) * 0.36
    low_string = synth_plucked_string(t, f0=98.0, t0=0.42, decay=0.65, n_harmonics=6) * 0.35
    sig = biquad_lowpass(drum_1 + drum_2 + dark_gong + low_string, 1400.0)
    return finalize_signal(sig, peak=0.88)


def gen_debate_push() -> np.ndarray:
    """
    debate.push: Tiếng phách gỗ sắc gọn kèm tiếng chuông nhỏ đi lên (thanh thuyết phục tiến).
    """
    rng = np.random.default_rng(2711)
    t = make_time(0.56)
    phach = synth_phach(t, rng, t0=0.01, f0=1080.0) * 0.82
    bell_low = synth_stone_khanh(t, f0=783.99, t0=0.05, decay=0.28) * 0.52
    bell_high = synth_stone_khanh(t, f0=1046.50, t0=0.13, decay=0.32) * 0.64
    pluck = synth_plucked_string(t, f0=523.25, t0=0.05, decay=0.28, n_harmonics=8) * 0.35
    sig = phach + bell_low + bell_high + pluck
    return finalize_signal(sig, peak=0.86)


def gen_debate_counter() -> np.ndarray:
    """
    debate.counter: Tiếng gõ mõ gỗ đôi kèm chuông nhỏ trầm xuống (đối phương phản bác).
    """
    rng = np.random.default_rng(2712)
    t = make_time(0.58)
    mo_1 = synth_mo(t, rng, t0=0.01, f0=690.0) * 0.85
    mo_2 = synth_mo(t, rng, t0=0.12, f0=520.0) * 0.78
    bell = synth_stone_khanh(t, f0=659.25, t0=0.04, decay=0.30) * 0.38
    bell_down = synth_stone_khanh(t, f0=523.25, t0=0.14, decay=0.32) * 0.42
    sig = mo_1 + mo_2 + bell + bell_down
    return finalize_signal(sig, peak=0.86)


def gen_debate_win() -> np.ndarray:
    """
    debate.win: Tiếng phách mở đầu cùng chuỗi chuông nhỏ và dây cổ cầm ngũ cung đi lên.
    """
    rng = np.random.default_rng(2713)
    t = make_time(1.16)
    phach = synth_phach(t, rng, t0=0.01, f0=1160.0) * 0.75
    notes = [
        (0.06, 523.25, 0.55),
        (0.19, 659.25, 0.62),
        (0.33, 783.99, 0.70),
        (0.48, 1046.50, 0.82),
    ]
    sig = phach.copy()
    for t0, freq, amp in notes:
        sig += synth_stone_khanh(t, f0=freq * 1.5, t0=t0, decay=0.48) * (amp * 0.52)
        sig += synth_plucked_string(t, f0=freq, t0=t0, decay=0.46, n_harmonics=10) * (amp * 0.48)
    return finalize_signal(sig, peak=0.88)


def gen_debate_lose() -> np.ndarray:
    """
    debate.lose: Tiếng mõ trầm cùng chuông nhỏ và dây gảy hạ giọng khi nhượng bộ / thất lý.
    """
    rng = np.random.default_rng(2714)
    t = make_time(1.12)
    mo_1 = synth_mo(t, rng, t0=0.015, f0=540.0) * 0.82
    mo_2 = synth_mo(t, rng, t0=0.34, f0=410.0) * 0.75
    bell_1 = synth_stone_khanh(t, f0=659.25, t0=0.04, decay=0.45) * 0.45
    bell_2 = synth_stone_khanh(t, f0=493.88, t0=0.35, decay=0.55) * 0.48
    qin_1 = synth_plucked_string(t, f0=220.0, t0=0.04, decay=0.50, n_harmonics=7) * 0.48
    qin_2 = synth_plucked_string(t, f0=164.81, t0=0.35, decay=0.62, n_harmonics=6) * 0.52
    sig = mo_1 + mo_2 + bell_1 + bell_2 + qin_1 + qin_2
    return finalize_signal(sig, peak=0.86)


def gen_event_reveal() -> np.ndarray:
    """
    event.reveal: Tiếng khánh đá và chiêng đồng nhẹ ngân vang cùng tiếng cổ cầm khi mở trang sử biến.
    """
    rng = np.random.default_rng(2715)
    t = make_time(1.34)
    khanh = synth_stone_khanh(t, f0=880.0, t0=0.02, decay=0.88) * 0.68
    khanh_octave = synth_stone_khanh(t, f0=1318.51, t0=0.06, decay=0.76) * 0.42
    soft_gong = synth_bronze_gong(t, rng, t0=0.03, f0=220.0, decay=0.92, bloom=0.14) * 0.55
    guqin = synth_plucked_string(t, f0=146.83, t0=0.04, decay=0.88, n_harmonics=9) * 0.52
    sig = khanh + khanh_octave + soft_gong + guqin
    return finalize_signal(sig, peak=0.88)


# ---------------------------------------------------------------------------
# Market ambience generator (8-12s seamless loop -> assets/audio/ambience/market.ogg)
# ---------------------------------------------------------------------------

def gen_market_ambience() -> np.ndarray:
    """
    Synthesizes a 10.0-second seamlessly looping ancient Chinese/Vietnamese market ambience:
    1. Crowd murmur: formant-filtered noise bursts with natural rising/falling cadence.
    2. Small merchant bronze bells (chuông nhỏ) chiming gently in the distance.
    3. Wooden cart wheels (bánh xe gỗ) rolling and creaking over stone pavement.
    Uses a 1.5s equal-power crossfade overlap so the 10.0s file loops with zero seam.
    """
    rng = np.random.default_rng(2720)
    loop_dur = 10.0
    xfade_dur = 1.6
    total_dur = loop_dur + xfade_dur
    t = make_time(total_dur)
    n = len(t)

    mix = np.zeros(n, dtype=np.float64)

    # 1. Warm street & crowd murmur bed with slow rising/falling waves
    pink = np.zeros(n, dtype=np.float64)
    white = rng.standard_normal(n)
    for fc, q, gain in [(220.0, 1.2, 0.35), (420.0, 1.8, 0.48), (760.0, 2.2, 0.36), (1280.0, 2.8, 0.18)]:
        band = biquad_bandpass(white, fc, q)
        # Slow crowd swell modulation
        swell = 0.68 + 0.32 * np.sin(TAU * (t / loop_dur) * 2.0 + float(rng.uniform(0, TAU)))
        pink += band * gain * swell
    mix += pink * 0.38

    # Syllabic chatter clusters (18 talker groups at varying vocal formants)
    for v in range(18):
        f1 = float(rng.uniform(340.0, 680.0))
        f2 = float(rng.uniform(950.0, 1780.0))
        syl_rate = float(rng.uniform(2.8, 4.8))
        phase0 = float(rng.uniform(0.0, TAU))
        phrase_rate = float(rng.choice([1.0, 2.0, 3.0])) / loop_dur
        phrase_phase = float(rng.uniform(0.0, TAU))

        v_noise = rng.standard_normal(n)
        formant1 = biquad_bandpass(v_noise, f1, 4.5)
        formant2 = biquad_bandpass(v_noise, f2, 5.5)
        syllable_env = np.maximum(0.0, np.sin(TAU * syl_rate * t + phase0)) ** 2.2
        phrase_env = np.maximum(0.0, 0.45 + 0.55 * np.sin(TAU * phrase_rate * t + phrase_phase)) ** 1.8
        mix += (formant1 * 0.65 + formant2 * 0.35) * syllable_env * phrase_env * 0.085

    # 2. Wooden cart wheels (bánh xe gỗ) rolling over stone street + wooden axle creak
    # Two cart passes across the 10s loop
    for pass_t0, pass_dur, base_f in [(0.8, 3.6, 155.0), (5.4, 3.8, 138.0)]:
        wheel_env = np.zeros(n, dtype=np.float64)
        mask = (t >= pass_t0) & (t < pass_t0 + pass_dur)
        u = (t[mask] - pass_t0) / pass_dur
        wheel_env[mask] = np.sin(math.pi * u) ** 1.5

        # Rolling wooden rim rumble on cobblestones
        rumble_raw = rng.standard_normal(n)
        rumble = (
            biquad_bandpass(rumble_raw, base_f, 2.6) * 0.7
            + biquad_bandpass(rumble_raw, base_f * 2.1, 3.2) * 0.4
        )
        cobble_mod = 0.65 + 0.35 * np.sin(TAU * 6.5 * t) * np.sin(TAU * 2.3 * t + 0.7)
        mix += rumble * wheel_env * cobble_mod * 0.28

        # Periodic wooden spoke/stone clacks & subtle wood axle creak
        n_bumps = int(pass_dur / 0.22)
        for b in range(n_bumps):
            bt = pass_t0 + b * 0.22 + float(rng.uniform(-0.03, 0.03))
            b_gain = math.sin(math.pi * ((bt - pass_t0) / pass_dur)) * float(rng.uniform(0.08, 0.16))
            mix += synth_mo(t, rng, t0=bt, f0=float(rng.uniform(210.0, 310.0))) * b_gain

        # Subtle wood-on-wood axle creak
        creak_t0 = pass_t0 + pass_dur * 0.42
        creak = modal_resonator(
            t,
            f0=base_f * 2.9,
            modes=[(1.0, 0.5, 0.18), (1.42, 0.3, 0.14)],
            t0=creak_t0,
            attack=0.04,
            pitch_bend_hz=-35.0,
            bend_decay=0.12,
        )
        mix += creak * 0.11

    # 3. Small merchant bronze bells (chuông nhỏ) chiming softly in the bazaar
    bell_events = [
        (1.35, 1567.98, 0.16),
        (1.52, 1975.53, 0.12),
        (4.15, 1318.51, 0.15),
        (6.80, 1760.00, 0.16),
        (7.02, 1567.98, 0.13),
        (9.10, 1318.51, 0.14),
    ]
    for bt, bf, bamp in bell_events:
        mix += synth_stone_khanh(t, f0=bf, t0=bt, decay=0.42) * bamp

    # Equal-power crossfade tail (loop_dur .. loop_dur + xfade_dur) into head (0 .. xfade_dur)
    n_loop = int(round(loop_dur * SR))
    n_xfade = n - n_loop
    loop_sig = mix[:n_loop].copy()
    tail_sig = mix[n_loop : n_loop + n_xfade]

    theta = np.linspace(0.0, 0.5 * math.pi, n_xfade)
    fade_in_w = np.sin(theta)
    fade_out_w = np.cos(theta)
    loop_sig[:n_xfade] = loop_sig[:n_xfade] * fade_in_w + tail_sig * fade_out_w

    # Gentle normalization without edge fades (so loop boundary stays seamless)
    loop_sig -= np.mean(loop_sig)
    loop_sig = biquad_lowpass(loop_sig, 4200.0)
    max_abs = float(np.max(np.abs(loop_sig)))
    if max_abs > 1e-8:
        loop_sig = loop_sig / max_abs
    loop_sig = np.tanh(loop_sig * 1.1) / math.tanh(1.1) * 0.72
    return loop_sig


def encode_ogg_with_ffmpeg(wav_samples: np.ndarray, ogg_path: Path, quality: int = 3) -> None:
    ogg_path.parent.mkdir(parents=True, exist_ok=True)
    ffmpeg_bin = shutil.which("ffmpeg")
    if not ffmpeg_bin:
        raise RuntimeError("ffmpeg not found on PATH")
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        tmp_wav = Path(tmp.name)
    try:
        write_wav(tmp_wav, wav_samples)
        cmd = [
            ffmpeg_bin,
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(tmp_wav),
            "-c:a",
            "libvorbis",
            "-q:a",
            str(quality),
            str(ogg_path),
        ]
        subprocess.run(cmd, check=True)
    finally:
        tmp_wav.unlink(missing_ok=True)


SFX_SPECS = [
    ("fx.sword_slash", "fx_sword_slash.wav", gen_fx_sword_slash, "Tiếng xé gió vung kiếm, lưỡi thép ngân và tia lửa (khớp sword-slash.js 0.75s)"),
    ("fx.thrust", "fx_thrust.wav", gen_fx_thrust, "Tiếng thương/kiếm đâm nhanh xé gió, mũi kim loại xuyên phá (khớp thrust.js 0.75s)"),
    ("fx.spear_sweep", "fx_spear_sweep.wav", gen_fx_spear_sweep, "Tiếng trường thương quét rộng tầm thấp, cát đá cuốn và đồng ngân (khớp spear-sweep.js)"),
    ("fx.arrow", "fx_arrow.wav", gen_fx_arrow, "Tiếng dây cung tơ bật nẩy, thân gỗ rung và tên lông vũ rít gió (khớp arrow.js 0.60s)"),
    ("fx.hit_flash", "fx_hit_flash.wav", gen_fx_hit_flash, "Tiếng trúng đòn trên giáp trụ, phách gỗ và lá giáp chấn động (khớp hit-flash.js 0.50s)"),
    ("fx.critical_hit", "fx_critical_hit.wav", gen_fx_critical_hit, "Trúng đòn chí mạng: trống đại cổ rền vang, chiêng đồng và mảnh vỡ (khớp critical-hit.js 0.90s)"),
    ("fx.crit_number", "fx_crit_number.wav", gen_fx_crit_number, "Hiện số chí mạng: khánh ngọc ngân sáng, phách đóng nhãn và tơ cầm (khớp crit-number.js)"),
    ("duel.start", "duel_start.wav", gen_duel_start, "Trống mở màn lôi đài: hai nhịp dồn dập mở đầu và một tiếng đại cổ trầm hùng"),
    ("duel.win", "duel_win.wav", gen_duel_win, "Chiêng thắng trận: đại la đồng ngân ấm hoà cùng biên chung ngũ cung"),
    ("duel.lose", "duel_lose.wav", gen_duel_lose, "Trống trầm thua cuộc: hai nhịp đại cổ trầm đục và dây trầm hạ giọng"),
    ("debate.push", "debate_push.wav", gen_debate_push, "Biện luận tiến thế: tiếng phách gỗ dứt khoát kèm chuông nhỏ đi lên"),
    ("debate.counter", "debate_counter.wav", gen_debate_counter, "Biện luận phản bác: tiếng mõ gỗ đôi kèm chuông nhỏ trầm xuống"),
    ("debate.win", "debate_win.wav", gen_debate_win, "Biện luận thắng cuộc: phách gỗ mở đầu và chuỗi chuông nhỏ - cổ cầm ngũ cung đi lên"),
    ("debate.lose", "debate_lose.wav", gen_debate_lose, "Biện luận thất lý / nhượng bộ: tiếng mõ trầm, chuông nhỏ và cổ cầm buông chùng"),
    ("event.reveal", "event_reveal.wav", gen_event_reveal, "Mở cảnh sử biến: tiếng khánh đá, chiêng đồng nhẹ và một tiếng cổ cầm trầm"),
]


def write_preview_html(preview_path: Path, rows: list[dict[str, str]]) -> None:
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    cards_html = []
    for idx, r in enumerate(rows):
        loop_attr = " loop" if r["is_loop"] == "1" else ""
        badge = "Âm nền lặp (OGG)" if r["is_loop"] == "1" else "Hiệu ứng (WAV)"
        cards_html.append(
            f"""
      <div class="sound-card" data-idx="{idx}">
        <div class="sound-head">
          <span class="sound-key">{html.escape(r["key"])}</span>
          <span class="sound-badge">{badge}</span>
        </div>
        <div class="sound-meta">
          <span>File: <code>{html.escape(r["rel_path"])}</code></span>
          <span>Độ dài: <strong>{html.escape(r["duration"])}</strong></span>
          <span>Dung lượng: <strong>{html.escape(r["size_kb"])}</strong></span>
        </div>
        <p class="sound-desc">{html.escape(r["desc"])}</p>
        <div class="sound-actions">
          <button type="button" class="play-btn" onclick="playItem({idx})">▶ Phát thử</button>
          <button type="button" class="stop-btn" onclick="stopItem({idx})">■ Dừng</button>
          <audio id="audio-{idx}" preload="auto"{loop_attr} controls src="{r["data_uri"]}"></audio>
        </div>
      </div>"""
        )

    doc = f"""<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Nghe thử âm thanh tổng hợp — Đợt 14B (Tam Quốc)</title>
  <style>
    :root {{
      --bg: #18130f;
      --paper: #f3ead8;
      --paper-border: #b89b72;
      --ink: #241911;
      --muted: #5c4633;
      --seal: #8f241e;
    }}
    * {{ box-sizing: border-box; }}
    body {{
      margin: 0;
      padding: 24px;
      font-family: "Noto Serif", "Palatino Linotype", Georgia, serif;
      background: var(--bg);
      color: var(--ink);
    }}
    .wrap {{
      max-width: 1040px;
      margin: 0 auto;
      background: var(--paper);
      border: 2px solid var(--paper-border);
      padding: 24px 28px;
    }}
    header {{
      border-bottom: 2px solid var(--paper-border);
      padding-bottom: 14px;
      margin-bottom: 18px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
    }}
    h1 {{
      margin: 0;
      font-size: 22px;
      color: var(--seal);
    }}
    .sub {{
      margin: 4px 0 0;
      color: var(--muted);
      font-size: 14px;
    }}
    .toolbar {{
      display: flex;
      gap: 10px;
    }}
    button {{
      font-family: inherit;
      font-size: 14px;
      font-weight: 600;
      padding: 7px 14px;
      border: 1px solid var(--seal);
      background: var(--seal);
      color: #fff8eb;
      cursor: pointer;
    }}
    button.stop-btn {{
      background: transparent;
      color: var(--ink);
      border-color: var(--paper-border);
    }}
    button:hover {{
      opacity: 0.9;
    }}
    .grid {{
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(460px, 1fr));
      gap: 14px;
    }}
    .sound-card {{
      border: 1px solid var(--paper-border);
      background: #faf4e8;
      padding: 12px 14px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }}
    .sound-head {{
      display: flex;
      justify-content: space-between;
      align-items: center;
    }}
    .sound-key {{
      font-family: Consolas, monospace;
      font-size: 15px;
      font-weight: 700;
      color: var(--seal);
    }}
    .sound-badge {{
      font-size: 12px;
      color: var(--muted);
      border: 1px solid var(--paper-border);
      padding: 2px 6px;
    }}
    .sound-meta {{
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      font-size: 13px;
      color: var(--muted);
    }}
    .sound-desc {{
      margin: 0;
      font-size: 14px;
      line-height: 1.4;
    }}
    .sound-actions {{
      display: flex;
      align-items: center;
      gap: 8px;
      margin-top: 4px;
    }}
    audio {{
      height: 32px;
      flex: 1;
    }}
  </style>
</head>
<body>
  <div class="wrap">
    <header>
      <div>
        <h1> Trang nghe thử âm thanh tổng hợp — Phần B (Đợt 14)</h1>
        <p class="sub">Âm sắc nhạc cụ cổ (phách, mõ, chiêng, khánh, đại cổ, dây gảy) &amp; tiếng chợ rì rầm · 22050 Hz Mono 16-bit</p>
      </div>
      <div class="toolbar">
        <button type="button" onclick="playAllSfx()">▶ Phát lần lượt 15 SFX</button>
        <button type="button" class="stop-btn" onclick="stopAll()">■ Dừng tất cả</button>
      </div>
    </header>
    <div class="grid">
      {"".join(cards_html)}
    </div>
  </div>
  <script>
    let seqTimer = null;
    function stopAll() {{
      if (seqTimer) {{ clearTimeout(seqTimer); seqTimer = null; }}
      document.querySelectorAll('audio').forEach((a) => {{ a.pause(); a.currentTime = 0; }});
    }}
    function playItem(idx) {{
      stopAll();
      const el = document.getElementById('audio-' + idx);
      if (el) {{ el.currentTime = 0; el.play(); }}
    }}
    function stopItem(idx) {{
      const el = document.getElementById('audio-' + idx);
      if (el) {{ el.pause(); el.currentTime = 0; }}
    }}
    function playAllSfx() {{
      stopAll();
      const sfxCount = {len(SFX_SPECS)};
      let i = 0;
      function next() {{
        if (i >= sfxCount) return;
        const el = document.getElementById('audio-' + i);
        i += 1;
        if (el) {{
          el.currentTime = 0;
          el.play();
          seqTimer = setTimeout(next, Math.max(600, (el.duration || 1) * 1000 + 250));
        }}
      }}
      next();
    }}
  </script>
</body>
</html>
"""
    preview_path.write_text(doc, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Synthesize Three Kingdoms ancient instrument SFX and market ambience.")
    parser.add_argument("--preview-html", type=Path, default=None, help="Optional output path for self-contained HTML preview page.")
    args = parser.parse_args()

    root_dir = Path(__file__).resolve().parents[2]
    audio_dir = root_dir / "assets" / "audio"
    sfx_dir = audio_dir / "sfx"
    ambience_dir = audio_dir / "ambience"
    preview_rows: list[dict[str, str]] = []

    print(f"Synthesizing {len(SFX_SPECS)} SFX files into {sfx_dir} ...")
    for key, filename, fn, desc in SFX_SPECS:
        samples = fn()
        out_path = sfx_dir / filename
        dur = write_wav(out_path, samples)
        assert dur < 1.5, f"{key} duration {dur:.3f}s exceeds 1.5s limit!"
        size_kb = out_path.stat().st_size / 1024.0
        print(f"  [SFX] {key:<18} -> sfx/{filename:<22} ({dur:.2f}s, {size_kb:.1f} KB)")
        b64 = base64.b64encode(out_path.read_bytes()).decode("ascii")
        preview_rows.append({
            "key": key,
            "rel_path": f"assets/audio/sfx/{filename}",
            "duration": f"{dur:.2f}s",
            "size_kb": f"{size_kb:.1f} KB",
            "desc": desc,
            "is_loop": "0",
            "data_uri": f"data:audio/wav;base64,{b64}",
        })

    market_ogg = ambience_dir / "market.ogg"
    market_samples = gen_market_ambience()
    market_dur = len(market_samples) / SR
    assert 8.0 <= market_dur <= 12.0, f"market.ogg duration {market_dur:.2f}s outside 8-12s range!"
    encode_ogg_with_ffmpeg(market_samples, market_ogg, quality=3)
    size_kb = market_ogg.stat().st_size / 1024.0
    print(f"  [AMB] {'ambience.market':<18} -> ambience/market.ogg        ({market_dur:.2f}s, {size_kb:.1f} KB)")
    b64_ogg = base64.b64encode(market_ogg.read_bytes()).decode("ascii")
    preview_rows.append({
        "key": "ambience.market",
        "rel_path": "assets/audio/ambience/market.ogg",
        "duration": f"{market_dur:.2f}s (lặp)",
        "size_kb": f"{size_kb:.1f} KB",
        "desc": "Tiếng chợ đông người rì rầm bằng nhiễu lọc formant có nhịp lên xuống, xen tiếng chuông nhỏ và bánh xe gỗ lăn trên đường đá.",
        "is_loop": "1",
        "data_uri": f"data:audio/ogg;base64,{b64_ogg}",
    })

    if args.preview_html is not None:
        write_preview_html(args.preview_html, preview_rows)
        print(f"  [HTML] Wrote preview page to {args.preview_html}")


if __name__ == "__main__":
    main()
