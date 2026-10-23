---
name: unity-aaa-combat-vfx
description: >-
  Expert guide and architectural patterns for creating AAA-quality combat visual effects
  in Unity (URP). Covers high-fidelity procedural sword slash arcs, multi-layered ribbon trails,
  impact needle sparks, expanding plasma shockwaves, dynamic point-light flashes, and post-processing
  bloom synergy for visceral melee combat.
---

# Unity AAA Combat VFX Architecture Guide

This skill provides the comprehensive specification and implementation techniques for engineering high-end, AAA-quality melee combat visual effects in Unity using the Universal Render Pipeline (URP).

---

## 1. Anatomy of an AAA Melee Slash Effect

In AAA character action games (e.g. *Devil May Cry 5*, *NieR: Automata*, *Ghostrunner*, *Cyberpunk 2077*), an attack effect is never a single particle system or a flat line. It is an orchestrated multi-layer visual event composed of five distinct physical layers:

| Layer | Component | Visual Purpose | Lifetime |
|---|---|---|---|
| **1. Weapon Ribbon Trail** | `TrailRenderer` on blade edge | Continuous kinetic ribbon trailing the blade through 3D space with tapered width curve and HDR glow. | 0.18s – 0.28s |
| **2. Procedural Slash Arc** | Dynamic 3D Arc Mesh (`MeshFilter` + `MeshRenderer`) | Sweeping crescent energy wave along the swing plane (120°–180°), with sharp inner rim and soft blooming outer falloff. | 0.12s – 0.16s |
| **3. Impact Needle Sparks** | `ParticleSystem` (Stretched Billboard) | High-speed directional sparks spraying outward at 12–25 m/s along the cut vector. | 0.15s – 0.25s |
| **4. Plasma Shockwave Ring** | Planar circular mesh/quad | Expanding kinetic impact ring expanding from 0.2m to 1.5m at the point of collision. | 0.14s – 0.20s |
| **5. Dynamic Point Light Flash** | Unity `Light` (Type: Point) | Blinding HDR illumination flash (1–2 frames) casting real-time colored light on duelists and ground. | 0.04s – 0.08s |

---

## 2. URP Post-Processing Synergy (The "Glow" Secret)

Emissive particle shaders without Post-Processing look flat and desaturated. True AAA brilliance requires:
1. **HDR Color Intensity**: Material base and emission colors must use HDR values with color multipliers between `2.5` and `8.0` (e.g., `new Color(0f, 2.5f, 3.5f)`).
2. **Global Volume Profile**:
   - **Bloom**: Threshold `0.9f`, Intensity `1.8f - 2.5f`, Scatter `0.7f`.
   - **Tonemapping**: `ACES` or `Neutral` (preserves contrast while allowing bright cores to burn white).
   - **Chromatic Aberration**: Intensity `0.15f - 0.25f` for subtle futuristic optical fringe.
3. **Camera Data**: `UniversalAdditionalCameraData.renderPostProcessing = true`.

---

## 3. Procedural Slash Arc Mesh Construction

Rather than flat 2D sprite billboards, AAA slashes use procedurally generated 3D polar coordinate fan meshes:
- **Inner Radius ($R_{in}$)**: Set near the character's hand/hilt (~0.6m).
- **Outer Radius ($R_{out}$)**: Extends past the sword tip (~1.8m – 2.2m).
- **Angle Sweep ($\theta$)**: Typically $120^\circ$ to $160^\circ$ across 24–36 segments.
- **UV Mapping**: U spans from 0 (trailing tail) to 1 (leading blade cutting edge); V spans from 0 (inner radius) to 1 (outer radius).
- **Vertex Color Alpha**: Quadratic falloff $A = (u)^2 \times (1 - v)$ giving a razor-sharp leading edge and smooth dissolving tail.

---

## 4. Multi-Stage Impact Sparks Specification

When the blade connects with an opponent:
1. **Flash Core**: Additive unlit quad flash oriented toward the camera.
2. **Directional Needle Sparks**:
   - `RenderMode`: `Stretch` with `LengthScale = 2.5` and `VelocityScale = 0.015`.
   - Shape: Cone angle $25^\circ$, directed away from the cut direction $\vec{d}_{cut}$.
   - Speed: $14 - 24 \text{ m/s}$.
3. **Secondary Drifting Plasma Embers**:
   - Spherical burst with drag `1.5`, gravity `1.2`, fading from cyan/magenta to dark amber.

---

## 5. Per-Hit Combo Hierarchy

A satisfying combo escalates in visual density and color:
- **Hits 1–2 (Standard Slashes)**: Crisp electric cyan ribbons, 12-particle needle bursts, minor camera shake.
- **Hits 3–5 (Spin & Uppercuts)**: Widened arc sweep ($160^\circ$), dual-tone cyan + magenta accents, 24-spark burst.
- **Hit 7 (Finisher)**:
  - Massive magenta/gold chromatic rupture crescent ($2.4\text{m}$ radius).
  - Expanding planar shockwave ring ($2.0\text{m}$).
  - 48 needle sparks + point light flash ($8\text{ lux}$).
  - Cinematic hitstop frame freeze ($0.08\text{s}$) + Euphoria physics ragdoll launch.
