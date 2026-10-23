# Cyberpunk Dev — Game Abstract

**Genre:** Cinematic cyberpunk action-RPG / space opera
**Engine:** Unity 6 (6000.6.0f1), Universal Render Pipeline 17.6, C#
**Platform:** PC (Windows x64), DirectX 11/12 and Vulkan
**Protagonist:** Alita, Berserker Unit 99

---

## 1. Abstract (short form)

Cyberpunk Dev is a single-player, story-driven action game built in Unity 6. The player is Alita, a 300-year-old Martian war cyborg found broken in the Iron City scrapyard and rebuilt by the surgeon Dr. Dyson Ido. The game follows her from that discovery, through a memory flashback to the lunar war known as The Fall, into a neon metropolis with a four-mission underworld campaign, across a rogue-AI cyberspace barrier and a lawless asteroid black market, and finally to Mars. There she faces the Colossus-06 Hammer Titan War Mech.

It combines fast katana and gun combat (a 7-hit combo, dodge, bullet time), single-take cinematic cutscenes, an open city with air traffic and crowds, 6-DOF starship flight, and multi-phase boss fights. Most levels are driven by custom runtime director scripts rather than Timeline. Dialogue, HUD and much of the sound design are generated or composed in code.

---

## 2. Story, act by act

1. **The Scrapyard Discovery.** Under acid rain in the Iron City scrap heaps, Dr. Ido finds a warm antimatter core and an intact human brain inside a ruined torso. One continuous camera move follows him clearing debris, scanning her vitals and carrying her home.
2. **Clinic Restoration.** Ido grafts her core and brain into an antique porcelain DollBody. Her eyes ignite and he welcomes her back.
3. **TRON Awakening (120 s).** Inside her boot sequence: a physical CRT terminal, a laser burst, a voxel rebuild of her body, then her eyelids opening on a Nine Inch Nails track.
4. **The Fall (52 s flashback).** Lunar battle, Year 2263. Alita is a U.R.M. shock trooper beside Commander Gelda, fighting under a huge Earth with starfighters dogfighting overhead.
5. **Cyber City and the four missions.**
   - *Mission 1, The Street Trial:* escort through hostile streets and a multi-wave bridge ambush, ending at the server-room airlock.
   - *Mission 2, Steal the Pit Pass:* Ido calls about a scalper holding a VIP pit pass. Alita chases them along a roughly 210 m NavMesh route (skybridge, terrace, clinic plaza, lower alley) with rubber-banding. After at least 30 seconds of chase she corners the scalper, wins a 1v1 duel and takes the pass with an `[E]` prompt. The chase runs on the "Downtown Theme (Extended)" track.
   - *Mission 3, The Pit:* a gladiator ladder in a 56 m colosseum with 550 spectators and euro-dollar purses.
   - *Mission 4, Vulcan Showdown:* the lights cut and war drums stop. The Forge-Lord Vulcan drops in for a 3-phase boss duel (Duelist, Meltdown Surge, Judgement of the Forge) that ends with an `[E] FINISH` execution.
6. **Beyond the Blackwall.** A pneumatic airlock leads to a 200 m audio-reactive Blackwall barrier, a mirror-water floor, a 16 m LiDAR face (Cynosure-09) and waves of holographic rogue-AI enemies.
7. **Sector 04 "DRIFT" Black Market.** Alita pilots her starship into an asteroid port and meets the informant known as The Collector (Kaelen Voss). The mission is to retrieve 4 asteroid core matrices by flying out through the broken dome with a tractor beam, then deliver them. Voss betrays her. A gantry blows, the catwalk collapses, and she fights through the trench and a Syndicate hover tank before escaping on Pad B-04. Hyperspace coordinates for Mars are unlocked.
8. **The Siege of Mars.** Arrival over Cydonia Basin beneath Phobos, a dust storm, star destroyers overhead, an antimatter core pedestal, and the Colossus-06 boss with its rocket war hammer.
9. **Epilogue.** A standoff with the robed Corporation Overseer in deep space, leading into the credits.

---

## 3. Gameplay systems

### Combat
- **Katana combo:** 7 directional light hits (forward swipe, backhand, uppercut, thrust, overhead cleave, 360° spin, and a finisher 2.2× in size).
- **Heavy strikes:** charged finishers that crack the ground.
- **Sidearm:** a gun-kata pistol (`F`).
- **Dodge:** 4-way dodge roll with invulnerability frames.
- **Bullet time:** time scales to 0.25×, with audio pitch warp, an energy meter and holographic ghost trails.
- **Reboot:** at zero health, `R` triggers a reboot sequence.
- **Hit feel:** hit-stop on every connect, additive HDR ribbon trails on the blade, and ragdolls that inherit the blade's impact direction.

### Characters and bosses
- **Alita's three bodies:** broken scrapyard core, porcelain DollBody, and nano Berserker armour. The DollBody carries an 88-blendshape facial rig used in the awakening cutscene.
- **Boss platform:** Goliath and Hammer mechs with 32 combat animations, two-handed grip IK on the hammer, rocket boosters on heavy swings, telegraphed ground-ring decals, and a segmented health and shield bar.

### Flight
- 6-DOF arcade starfighter: 55 m/s cruise, 120 m/s afterburner.
- Hover cushion, banking roll, plasma blasters and torpedoes.
- Tractor beam for the core matrices, and a hyperspace warp jump.
- Switch between chase and cockpit cameras.

### Living world
- 15 hover cars on 3 spline skyways, and a monorail.
- Behavior-tree crowds in the city.
- 60+ black-market NPCs that walk patrol routes, pause to talk, and steer around walls.
- An interactive backpack and economy.

### UI
- A Cyberpunk-2077-style HUD: health, stamina, dash pips, minimap, and a quest tracker that sits under the minimap.
- A themed dialogue HUD with hexagonal portraits, colour-coded per speaker, and typewriter text.

---

## 4. Technology and production

- **Rendering:** URP with Forward+, linear colour, ACES tonemapping, bloom, SSAO, planar mirror reflections, rain-on-lens effects, and a dozen custom shaders including a holographic dissolve.
- **Physics:** a hybrid kinematic and dynamic character controller, SuperCharacterController, and Euphoria-style ragdolls.
- **Audio:** 3D spatial audio, a 64-band FFT analyser driving the Blackwall visuals, software synthesizers written in C# for UI clicks and mech sounds, layered black-market ambience that ducks during dialogue, and a mission chase track swap with crossfade.
- **Voice:** neural text-to-speech lines generated with Edge-TTS for Ido, Alita, Cynosure, Vulcan and Voss.
- **Input and networking:** the new Input System with legacy fallback, and a Netcode for GameObjects multiplayer foundation.
- **Art:** Synty Polygon packs (Cyber City, Sci-Fi Worlds, Mech, Sci-Fi Space, Sidekick characters) plus procedural builders for arenas, terrain and the Blackwall mesh.
- **Tooling:** 40+ custom editor tools and scene builders. The Mars scene can be rebuilt from the Scavenger Wastes map.

**Scale:** about 25 scenes, covering narrative, arenas, spaceflight, showcases and technical benchmarks.

---

## 5. Notes on current work

- The Mars final battle is being moved onto the Synty Scavenger map, with the buildings removed and the scrap kept. The builder is written but has not yet been run in the editor.
- Black-market NPC wall avoidance and the mission objective HUD relocation are recent fixes that still need playtesting.
