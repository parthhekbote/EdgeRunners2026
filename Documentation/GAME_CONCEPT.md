# Cyberpunk Dev: Game Concept and Gameplay Design

*Working title. Single-player cinematic cyberpunk action-RPG for PC, built in Unity 6. Every statement below was checked against the project's code.*

**One line:** Alita is a Martian war cyborg, rebuilt from scrap and missing the antimatter heart she was built around. She fights across a neon city, a rogue-AI cyberspace and a black market to reclaim it on Mars.

---

## 1. VISUAL

**Style:** stylised low-poly sci-fi (Synty Polygon art packs) lit like a neon film: strong colour, glow, rain, dust and holograms. The camera is a third-person orbit camera in play and a directed cinematic camera in story scenes.

| Place | Look |
|---|---|
| Iron City scrapyard | acid rain, scrap heaps, the sky city of Zalem overhead |
| Clinic | warm, cramped workshop; Alita's three bodies (broken core, porcelain doll body, nano armour) |
| Cyber City | neon streets, crowds, flying-car traffic on skyways, a monorail |
| The Pit | 56 m circular arena with glowing rings, a hologram dragon, packed grandstands |
| Blackwall | dark cyberspace: a 200 m barrier that pulses with the music, mirror-water floor, a giant LiDAR face |
| Black market (Sector 04 "DRIFT") | asteroid port: neon stalls, alien and human vendors, landing pads, a broken dome open to space |
| Mars | red dunes, Phobos in the sky, dust storm, scrap wreckage, a crashed warship, dreadnoughts overhead |

**Visual language of play**
- Hit feel: short freeze-frames, screen shake, glowing sword trails, ragdoll enemies.
- UI: a Cyberpunk-2077-style HUD (health, bullet-time energy, minimap, quest tracker) in yellow, cyan and red; a glowing guide trail points to the next objective.
- Dialogue cards with a hexagonal portrait frame, colour-coded per speaker.
- Boss attacks are telegraphed with glowing ground rings and wind-ups.

---

## 2. STORY / PLOT

**Premise:** Dr. Dyson Ido finds Alita broken in the Iron City scrapyard and rebuilds her. Her true antimatter core is missing. Finding it is her goal.

1. **Scrapyard and clinic:** Ido rebuilds her; she wakes up. A flashback shows her as a soldier in the lunar war, "The Fall" (2263), beside Commander Gelda.
2. **Cyber City, four missions:** street trial and bridge ambush; steal a Pit pass from a scalper; fight the Pit ladder; defeat the arena boss Vulcan.
3. **The bridge:** Ido calls for help after the arena and is killed in front of her. His last signal leads her to a server room.
4. **Beyond the Blackwall:** she fights a rogue AI's holographic army. Cynosure-09 tells her she does not hold her real core and offers a starship.
5. **Black market:** the broker Voss sends her to collect four drifting cores. He betrays her (a 10-million-credit bounty), the catwalk collapses, she fights through bounty hunters and a siege tank, and escapes to Mars.
6. **Mars:** she finds the Berserker heart in a crashed warship. A fleet, an army and the Titan war mech Colossus-06 attack; she fights the Titan.
7. **Epilogue:** a standoff with the robed Corporation Overseer, then credits.

**Themes:** loss, betrayal, identity, rebellion against the corporations that made her.
**Characters:** Alita (hero), Ido (guide), Vulcan (arena boss), Cynosure-09 (rogue AI), Voss (traitor), Colossus-06 (final boss).

---

## 3. AUDIO

- **Voiced dialogue:** every dialogue box is spoken aloud, with a different voice per main character (neural text-to-speech).
- **Music changes with the moment:** a chase track for the scalper chase, an industrial track for the Blackwall fight, battle drums and a cheering crowd in the Pit, a rock track as Alita arrives at the black market, a build-up track for the Mars invasion that crossfades into the boss track.
- **Ambience:** rain and city hum, a murmuring black-market crowd with hydraulic hisses, Mars wind and dust storm.
- **Sound effects:** sword swings and impacts, gun bursts, dashes, servo jumps and armoured landings, heavy mech stomps and thrusters, tank cannon and explosions, starship fly-bys and engines.
- **Audio as feedback:**
  - Bullet time slows the sound down.
  - A perfect parry freezes time for a beat.
  - Music ducks under dialogue and phone calls.
  - The Blackwall barrier moves with the music's spectrum.
  - Sound is positioned in 3D.

---

## 4. GAMEPLAY

### Core loop
Get a briefing, follow the guide trail to the objective, fight or fly or chase, win the encounter, watch the story beat, repeat. If Alita dies, press **R** to reboot.

### Controls
| Action | Input |
|---|---|
| Move / run | WASD / Shift |
| Jump | Space |
| Light attack (7-hit combo) | Left click or J |
| Heavy attack (chains up to 3) | Right click or K |
| Gun burst (3 shots) | Q |
| Dodge dash (about 6 m, 4 directions) | Left Alt |
| Block / perfect parry | F |
| Bullet time (toggle) | T |
| Lock-on | Tab or middle mouse |
| Draw / sheathe sword | Z |
| Interact / talk | E |
| Inventory | I |
| Reboot after death | R |

### Combat
- Health 250. Light hit 35 damage, heavy 75, gun 28 per shot at up to 30 m.
- **Parry:** press block just before a hit (a 0.22 s window) to negate it and freeze time for a moment. A late block still cuts damage to 20%.
- **Dodge** repositions you but gives no invulnerability, so timing matters.
- **Bullet time:** 0.25x speed, a 100-point energy bar that drains while active and recharges when off.
- **Enemies** have poise: light hits do not stagger them but heavy hits and enough damage break their poise and stagger them. They fight in groups that take turns attacking, with named combos (Executioner Shred, Stalker Lunge, Cyclone Crossfire, Parry Riposte, Blitzkrieg Overdrive).

### Missions (each plays differently)
| Mission | What you do | Win condition |
|---|---|---|
| 1 Street trial | follow the trail to a bridge, fight 3 Razor Punk bots | kill all 3 |
| 2 Steal the Pit pass | chase a scalper (at least 30 s), then duel him | take the pass with [E] |
| 3 The Pit | 1-v-1 gladiator ladder, tougher each round, money after each win | win round 3 to trigger Vulcan |
| 4 Vulcan showdown | 3-phase boss duel | execute him with [E] |
| 5 Server room | follow Ido's signal to the door | enter [E] |
| Blackwall | 3 waves of holographic robots, the last with a 450 HP hyper-armoured overseer | win, then talk to the AI and choose a response |
| Black market | talk to vendors for 2 leads, fly the Nightshade through the dome, tractor-beam 4 cores [T], land on the pad, deliver them | survive the betrayal and reach the escape pad, then warp [R] |
| Mars | absorb the core [E] at the pedestal, leave the warship, survive the invasion cutscenes, fight Colossus-06 | defeat the Titan |

### Boss design
- **Vulcan:** 3 phases at 100/66/33% health, each adding new threats, ending with a kneeling finisher.
- **Colossus-06 Titan (4200 HP):** hammer combos, a leap slam shown by a ground ring, ground-stomp shockwaves, thruster dashes, guarding and counters. After a run of attacks it vents steam and is open to punishment. Below 50% health it turns faster and attacks more.

### Systems
- **Inventory and money:** weapons, healing items, cyberware (more health, speed or heavy damage), loot to sell, a stolen arena pass. Money comes from Pit wins and selling loot. (There is no shop yet.)
- **Starship flight:** 6-axis flight, cruise 55 m/s and afterburner 120 m/s, blasters (left click), torpedo (right click), camera toggle (V), tractor beam (T), hyperspace warp (R).
- **Living world:** crowds, vendors who talk and give leads, flying traffic and a monorail.

---

## Notes to keep the story consistent
- Ido's death is shown twice differently: the Blackwall AI says the corporation killed him, while the bridge scene shows Vulcan doing it after Vulcan was already beaten in the arena.
- Earlier mission texts treat Ido as already gone before the bridge scene.
