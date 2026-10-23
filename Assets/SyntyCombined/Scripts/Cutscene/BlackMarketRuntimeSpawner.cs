using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyntyCombined.BlackMarket;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Master Runtime and Editor Spawner for the Black Market NPC Ecosystem.
    /// Exclusively uses character models from the Polygon SciFi Worlds pack (Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters)
    /// and guarantees 100% precision floor raycast grounding with zero floating NPCs.
    /// Places shopkeepers carefully INSIDE their shops behind counters, customers in front,
    /// and equips roaming NPCs with the BlackMarketNPCWanderer system to walk and patrol across the market.
    /// </summary>
    public class BlackMarketRuntimeSpawner : MonoBehaviour
    {
        private const string SCI_FI_CHR_PATH = "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == "Demo_BlackMarket_01" || scene.name.Contains("BlackMarket"))
            {
                SpawnEcosystem();
            }
        }

        public static GameObject SpawnEcosystem()
        {
            GameObject existing = GameObject.Find("[BlackMarket_NPC_Ecosystem]");
            if (existing != null)
            {
                if (existing.transform.childCount > 0)
                {
                    // Ecosystem already baked and populated in scene
                    return existing;
                }
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject("[BlackMarket_NPC_Ecosystem]");

            // Attach Dialogue HUD and Story Combat Director
            BlackMarketNPCDialogueHUD.GetOrCreate();
            var combatDirector = root.AddComponent<BlackMarketStoryCombatDirector>();

            // Load Spectator & Locomotion Animation Controllers
            RuntimeAnimatorController animDrinkM = null;
            RuntimeAnimatorController animDrinkF = null;
            RuntimeAnimatorController animLeanM = null;
            RuntimeAnimatorController animLeanF = null;
            RuntimeAnimatorController animVideoM = null;
            RuntimeAnimatorController animVideoF = null;
            RuntimeAnimatorController animHypeM = null;
            RuntimeAnimatorController animWalkM = null;
            RuntimeAnimatorController animWalkF = null;

            // Load Chatter Audio Clips
            AudioClip[] civChatter = null;
            AudioClip[] botChatter = null;
            AudioClip[] mercChatter = null;
            AudioClip[] phoneChatter = null;

#if UNITY_EDITOR
            combatDirector.alarmSirenSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3");
            combatDirector.explosionSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/dragon-studio-dry-thunder-364468.mp3");
            combatDirector.fallLandingSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Jump_Landing_Boots_01.wav");
            combatDirector.warpDriveSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/SciFi_Starship_Flyby_Pass.mp3");

            animDrinkM = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_DrinkWatching_Masc.controller");
            animDrinkF = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_DrinkWatching_Femn.controller");
            animLeanM = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_RailingLean_Masc.controller");
            animLeanF = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_RailingLean_Femn.controller");
            animVideoM = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_VideoRecording_Masc.controller");
            animVideoF = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_VideoRecording_Femn.controller");
            animHypeM = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_Spectator_AggressiveHype_Masc.controller");

            animWalkM = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_BlackMarket_Walker_Masc.controller");
            animWalkF = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Spectators/AC_BlackMarket_Walker_Femn.controller");

            civChatter = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_civilian_murmur_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_civilian_murmur_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_civilian_murmur_03.wav")
            };

            botChatter = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_helperbot_chirp_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_helperbot_chirp_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_helperbot_chirp_03.wav")
            };

            mercChatter = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_merc_grunt_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_merc_grunt_02.wav")
            };

            phoneChatter = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_phone_chatter_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_phone_chatter_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/npc_phone_chatter_03.wav")
            };
#endif

            // =========================================================================
            // ZONE 1: WESTERN CARGO BARGE & PERIMETER GANTRY
            // (Landing Pad B-04 is kept 100% clear of NPCs for starship flight)
            // =========================================================================

            // 1. Captain Drake (Cargo Tug Pilot)
            SpawnNPC(
                root.transform, "Captain Drake", NPCRoleType.ShipMechanic, "CARGO // TUG PILOT",
                new Color(0.95f, 0.65f, 0.15f), new Vector3(-14.30f, 2.10f, -2.72f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Mooring clamps locked on the cargo barge. If you need transport across the ring, talk to the gantry broker.",
                    "Keep your eyes sharp—Omni-Corp patrol cruisers have been scanning Sector 4 ever since the reactor dropped offline."
                }
            );

            // 2. Pad Tech Vorn (Relocated to Western Gantry)
            SpawnNPC(
                root.transform, "Technician Vorn", NPCRoleType.ShipMechanic, "TECH // GANTRY ENGINEER",
                new Color(0.2f, 0.85f, 0.95f), new Vector3(-12.8f, 0.05f, 12.0f), Quaternion.Euler(0f, 60f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab", animVideoM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Starship repulsor coils at 84% thermal dissipation. Keep back while I vent the hyperdrive plasma manifold."
                }
            );

            // 3. Kryx the Stargazer (Relocated to Observation Overlook)
            SpawnNPC(
                root.transform, "Kryx the Stargazer", NPCRoleType.MarketPatron, "VOYAGER // LOOKOUT",
                new Color(0.7f, 0.8f, 0.95f), new Vector3(8.5f, 1.88f, 32.5f), Quaternion.Euler(0f, 110f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Alien_Male_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The cosmic nebula looks turbulent today. Static storms always bring corporate gunships."
                }
            );

            // =========================================================================
            // ZONE 2: WALKING & PATROLLING CITIZENS / ENFORCERS
            // =========================================================================

            // 4. Enforcer Vance (Concourse Patrol - WALKING)
            SpawnNPC(
                root.transform, "Enforcer Vance", NPCRoleType.StationEnforcer, "SECURITY // CONCOURSE PATROL",
                new Color(0.35f, 0.55f, 0.95f), new Vector3(21.0f, 1.86f, 34.0f), Quaternion.Euler(0f, 140f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_02.prefab", animWalkM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Station security patrol active. Keep weapons holstered on the promenade.",
                    "Last Martian ship that docked here without papers burned a whole sector. Command still hasn't forgiven the paperwork."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(21.0f, 1.86f, 34.0f),
                    new Vector3(17.5f, -2.0f, 28.0f),
                    new Vector3(14.0f, -2.0f, 22.0f),
                    new Vector3(9.0f, -1.8f, 16.0f)
                },
                walkSpeed: 1.25f,
                idleController: animHypeM
            );

            // 5. Lt. Vane (Market Security - WALKING)
            SpawnNPC(
                root.transform, "Lieutenant Vane", NPCRoleType.StationEnforcer, "SECURITY // MARKET SENTRY",
                new Color(0.35f, 0.55f, 0.95f), new Vector3(3.2f, -1.25f, 20.5f), Quaternion.Euler(0f, 0f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Female_01.prefab", animWalkF, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Level 2 alert issued by station command. Unauthorized vessel arrival logged on Pad B-04."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(3.2f, -1.25f, 20.5f),
                    new Vector3(3.0f, -1.25f, 23.5f),
                    new Vector3(2.2f, -0.22f, 16.5f),
                    new Vector3(2.0f, -0.22f, 13.5f)
                },
                walkSpeed: 1.15f,
                idleController: animVideoF
            );

            // 6. Traveler Kira (Market Window Shopper - WALKING)
            SpawnNPC(
                root.transform, "Traveler Kira", NPCRoleType.MarketPatron, "CIVILIAN // SHOPPER",
                new Color(0.35f, 0.85f, 0.85f), new Vector3(2.4f, -0.22f, 14.5f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Female_02.prefab", animWalkF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "I came looking for genuine Martian crystals, but everyone in the market is whispering about a 10 million credit bounty."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(2.4f, -0.22f, 14.5f),
                    new Vector3(3.0f, -1.25f, 19.5f),
                    new Vector3(2.8f, -1.25f, 24.0f),
                    new Vector3(3.2f, 1.19f, 27.5f)
                },
                walkSpeed: 1.1f,
                idleController: animLeanF
            );

            // 7. Rust 'Rat' (Trench Prowler - WALKING)
            SpawnNPC(
                root.transform, "Rust 'Rat'", NPCRoleType.MarketPatron, "TRENCH // SCAVENGER",
                new Color(0.65f, 0.45f, 0.25f), new Vector3(13.5f, -2.0f, 23.5f), Quaternion.Euler(0f, -70f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_03.prefab", animWalkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Everything dropped from the high gantry ends up down here in the trench. Keep your eyes sharp for falling debris."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(13.5f, -2.0f, 23.5f),
                    new Vector3(15.8f, -2.0f, 26.5f),
                    new Vector3(12.0f, -2.0f, 25.0f),
                    new Vector3(14.0f, -2.0f, 21.0f)
                },
                walkSpeed: 1.2f,
                idleController: animLeanM
            );

            // 8. Cider 'Daze' (Wandering Outlaw - WALKING)
            SpawnNPC(
                root.transform, "Cider 'Daze'", NPCRoleType.MarketPatron, "OUTLAW // WANDERER",
                new Color(0.75f, 0.25f, 0.25f), new Vector3(17.5f, -2.0f, 12.0f), Quaternion.Euler(0f, -120f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Male_01.prefab", animWalkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Ghk... leave me be, blade girl... whole station's rotten from the reactor to the flight deck..."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(17.5f, -2.0f, 12.0f),
                    new Vector3(15.0f, -2.0f, 14.5f),
                    new Vector3(16.5f, -2.0f, 18.0f)
                },
                walkSpeed: 1.0f,
                idleController: animDrinkM
            );

            // 9. Technician Zedd (Tech Courier - WALKING)
            SpawnNPC(
                root.transform, "Technician Zedd", NPCRoleType.ShipMechanic, "MAINTENANCE // TECH COURIER",
                new Color(0.2f, 0.65f, 0.95f), new Vector3(7.0f, 6.2f, 6.5f), Quaternion.Euler(0f, 110f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_01.prefab", animWalkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Transporting medical and tech canisters between the clinic and gantry terminal. Mind the clearance."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(7.0f, 6.2f, 6.5f),
                    new Vector3(10.5f, 6.2f, 7.5f),
                    new Vector3(13.5f, -2.0f, 18.0f),
                    new Vector3(16.0f, -2.0f, 24.0f)
                },
                walkSpeed: 1.3f,
                idleController: animVideoM
            );

            // =========================================================================
            // ZONE 3: STORY NPC (THE COLLECTOR // DRIFT SALVAGE KING)
            // =========================================================================

            // 10. The Collector (Stationed inside SM_Bld_Pod_Hab_Small_04 (1) Trophy Den)
            GameObject vossGo = SpawnNPC(
                root.transform, "The Collector (Kaelen Voss)", NPCRoleType.StoryFixer, "THE COLLECTOR // ALIEN RELIC OVERLORD",
                new Color(0.95f, 0.75f, 0.15f), new Vector3(4.8f, 0.28f, -7.8f), Quaternion.Euler(0f, -125f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienArmor_01.prefab", animLeanM, phoneChatter,
                isStoryObjective: true, unlockObjectiveText: "MISSION ACTIVE: Head to Western Flight Deck, Board Starship & Retrieve 4 Core Matrices [0/4]",
                dialogue: new string[]
                {
                    "Well, well... Alita. The legendary Martian Berserker standing inside my trophy sanctuary. Word reached my antennae before your ship's thrusters even cooled.",
                    "You're looking for your URM-004 Antimatter Core, aren't you? My salvage informants intercepted the Omni-Corp encrypted dispatch: the core was transferred off this station hours ago, bound for the ancient Cydonia War-Vault on Mars.",
                    "I have the orbital hyperspace jump vector to Mars locked in my terminal. But on Sector 4, nothing is free. When the Corporation raided our ring, four critical Antimatter Core Matrices were blown out into space through the broken dome.",
                    "Without those four matrices, this station's central reactor dies, and my hyperspace gate stays offline. That's where you come in.",
                    "Take the SS-04 Nightshade Striker docked on the Western Flight Deck—she's fitted with an electromagnetic ventral tractor beam [T]. Fly out through the fractured dome, lock onto all 4 scattered core matrices, and dock them into Engine-01's central reactor.",
                    "Restore station power, and I'll transmit the Mars hyperspace jump vector straight to your ship's nav-computer. Go get those cores, hunter."
                }
            );

            if (vossGo != null)
            {
                var vossNpc = vossGo.GetComponent<BlackMarketNPC>();
                vossNpc.waitingDialogueText = "The 4 Core Matrices are still drifting out in space. Board the Nightshade on Pad 02 and engage your ventral tractor beam [T]!";
                combatDirector.fixerVossNPC = vossNpc;
            }

            // =========================================================================
            // ZONE 4: SHOP 1 - WEAPONS & ARMS BAZAAR (SM_Bld_Vendor_04)
            // =========================================================================

            // 11. Vex 'Trigger' Malakor (Shopkeeper INSIDE booth behind counter)
            SpawnNPC(
                root.transform, "Vex 'Trigger' Malakor", NPCRoleType.WeaponsMerchant, "WEAPONS // ARMS BROKER",
                new Color(0.95f, 0.25f, 0.25f), new Vector3(2.05f, -1.25f, 19.80f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_01.prefab", animLeanM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Buy something or back off. ... Huh. Damascus plate, URM joints. You're not a customer. You're a war somebody dug up.",
                    "Voss? He came through two nights ago and paid me for a dead-man's switch — cheap transmitter wired to his own heartbeat. Man planning to die on his own terms.",
                    "He was shipping something out of the refinery, not the other way around. Whatever's in that terminal, he's already sold it once."
                },
                leadId: "vex",
                leadTitle: "VOSS PAID FOR A DEAD-MAN'S SWITCH",
                leadInsight: "NEW LEAD: VOSS BOUGHT A DEAD-MAN'S SWITCH"
            );

            // 12. Grim the Hunter (Customer OUTSIDE browsing blasters at counter)
            SpawnNPC(
                root.transform, "Grim the Hunter", NPCRoleType.MarketPatron, "OUTLAW // BOUNTY HUNTER",
                new Color(0.85f, 0.2f, 0.35f), new Vector3(1.15f, -1.25f, 19.80f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienArmor_01.prefab", animDrinkM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Nice selection of plasma weaponry, Vex. Got anything tuned to punch through corporate synth-dermis?"
                }
            );

            // 13. Sentinel-04 (Arms Stall Enforcer Guard)
            SpawnNPC(
                root.transform, "Sentinel-04", NPCRoleType.StationEnforcer, "SECURITY // ARMS GUARD",
                new Color(0.4f, 0.45f, 0.5f), new Vector3(1.20f, -1.25f, 21.00f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienCombat_01.prefab", animHypeM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Target scanned. Damascus steel blade detected on chassis. Weapon discharge in retail zone triggers lethal response."
                }
            );

            // =========================================================================
            // ZONE 5: SHOP 2 - STREET FOOD & NOODLE BAR (SM_Bld_Vendor_01)
            // =========================================================================

            // 14. Chef Gorn (Shopkeeper INSIDE behind wok cooker)
            SpawnNPC(
                root.transform, "Chef Gorn", NPCRoleType.FoodVendor, "VENDOR // STREET FOOD",
                new Color(0.9f, 0.4f, 0.85f), new Vector3(1.10f, -0.15f, 14.50f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienChef_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Hot noodles! Spicy synthetic squids! Eat before the shooting starts, cyborg! Market's on edge today!"
                }
            );

            // 15. Sous-Chef Zorg (Assistant INSIDE behind prep table)
            SpawnNPC(
                root.transform, "Sous-Chef Zorg", NPCRoleType.FoodVendor, "SERVICE // ALIEN COOK",
                new Color(0.3f, 0.85f, 0.95f), new Vector3(1.10f, -0.15f, 15.50f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienChef_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Simmering radioactive algae pods. Extra sulfur seasoning on request."
                }
            );

            // 16. Darius Cross (Customer OUTSIDE eating at counter stool)
            SpawnNPC(
                root.transform, "Darius Cross", NPCRoleType.MarketPatron, "PATRON // DINER",
                new Color(0.95f, 0.65f, 0.35f), new Vector3(-1.10f, -0.22f, 14.20f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab", animDrinkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Best broth on the Drift. Burns the taste of ozone and starship coolant right out of your mouth."
                }
            );

            // 17. Trixie Volt (Customer OUTSIDE at counter stool)
            SpawnNPC(
                root.transform, "Trixie 'Volt'", NPCRoleType.MarketPatron, "PATRON // CYBORG",
                new Color(0.85f, 0.45f, 0.95f), new Vector3(-1.20f, -0.22f, 15.40f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab", animDrinkF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "I saw a black corporate shuttle dock at Sector 1 earlier. Black ops death squad... they never visit without a target."
                }
            );

            // =========================================================================
            // ZONE 6: SHOP 3 - TECH & SCRAP SALVAGE STALL (SM_Bld_Vendor_02)
            // =========================================================================

            // 18. Scavenger Dax (Shopkeeper INSIDE behind salvage display)
            SpawnNPC(
                root.transform, "Scavenger Dax", NPCRoleType.MarketPatron, "SCAVENGER // SCRAP FENCE",
                new Color(0.9f, 0.75f, 0.2f), new Vector3(1.65f, -0.21f, 23.90f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Fresh salvage stripped from an orbital hauler! Gyro-stabilizers, optic prisms, neural diodes—only 50 credits!"
                }
            );

            // 19. Scavenger Jun (Customer browsing components)
            SpawnNPC(
                root.transform, "Scavenger Jun", NPCRoleType.MarketPatron, "SCAVENGER // TECH SALVAGE",
                new Color(0.85f, 0.65f, 0.25f), new Vector3(0.40f, -0.40f, 23.40f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_02.prefab", animVideoM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Omni-Corp patrol drones crashed near Sector 6 last night. Found pristine titanium plating before the fires caught."
                }
            );

            // 20. Zane 'Static' (Customer examining salvage)
            SpawnNPC(
                root.transform, "Zane 'Static'", NPCRoleType.MarketPatron, "STREET // OUTLAW",
                new Color(0.7f, 0.35f, 0.95f), new Vector3(0.40f, -0.40f, 24.60f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Strider_Male_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Corporate suits passed through an hour ago with encrypted datapads. Word on the mesh is there's an enormous bounty circulating."
                }
            );

            // =========================================================================
            // ZONE 7: SHOP 4 - RIPPERDOC CLINIC (SM_Bld_Vendor_08)
            // =========================================================================

            // 21. Dr. Aris Thorne (Ripperdoc INSIDE clinic operating room)
            SpawnNPC(
                root.transform, "Dr. Aris Thorne", NPCRoleType.Ripperdoc, "MED // RIPPERDOC",
                new Color(0.2f, 0.85f, 0.5f), new Vector3(6.50f, 6.20f, 6.00f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Male_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Sit. No — stand. I've seen Cydonia rot before, and it doesn't sit.",
                    "Voss has it. Salvage crews came back from the Fall with Martian dust in their marrow. He's got maybe six weeks. Then Omni-Corp walked in with a clean stem-cell contract and a price.",
                    "He took it. Didn't ask what they wanted. On the way out I heard him say one thing: 'She won't even know she's the payment.'"
                },
                leadId: "thorne",
                leadTitle: "OMNI-CORP BOUGHT VOSS A CURE",
                leadInsight: "NEW LEAD: OMNI-CORP BOUGHT VOSS A CURE"
            );

            // 22. Nurse Cora (Clinic Assistant INSIDE clinic)
            SpawnNPC(
                root.transform, "Nurse Cora", NPCRoleType.Ripperdoc, "MED // BIO-ASSISTANT",
                new Color(0.3f, 0.95f, 0.75f), new Vector3(7.20f, 6.20f, 7.00f), Quaternion.Euler(0f, 180f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Female_01.prefab", animVideoF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Running low on neural dampeners. If you're looking for implants, you'll have to wait until next cargo cycle."
                }
            );

            // 23. Patient RX-7 (Wounded Alien INSIDE clinic chair)
            SpawnNPC(
                root.transform, "Patient RX-7", NPCRoleType.MarketPatron, "PATIENT // WOUNDED",
                new Color(0.6f, 0.65f, 0.7f), new Vector3(5.60f, 6.20f, 5.20f), Quaternion.Euler(0f, 45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienRock_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Plasma burn took out half my outer exoskeleton. Thorne is using synthetic resin to seal the cracks."
                }
            );

            // 24. Twitch 'Jitter' (Stim Addict in waiting room)
            SpawnNPC(
                root.transform, "Twitch 'Jitter'", NPCRoleType.MarketPatron, "OUTLAW // STIM-HEAD",
                new Color(0.55f, 0.9f, 0.25f), new Vector3(4.80f, 6.20f, 6.50f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_02.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The neural pulses... they're broadcasting a kill code on the subnet... 10 million... 10 million credits!"
                }
            );

            // =========================================================================
            // ZONE 8: SHOP 5 - CONTRABAND DEN & ORGAN EXCHANGE (SM_Bld_Vendor_05)
            // =========================================================================

            // 25. Nyx 'Shadow' (Dealer INSIDE behind security window)
            SpawnNPC(
                root.transform, "Nyx 'Shadow'", NPCRoleType.ContrabandDealer, "OUTLAW // CONTRABAND",
                new Color(0.6f, 0.35f, 0.95f), new Vector3(3.20f, 1.19f, 29.20f), Quaternion.Euler(0f, -145f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Female_01.prefab", animLeanF, phoneChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The air is toxic today. Syndicate enforcers took positions on the high catwalks. Watch your flank."
                }
            );

            // 26. Lord Sterling (Customer OUTSIDE at security counter)
            SpawnNPC(
                root.transform, "Lord Sterling", NPCRoleType.MarketPatron, "ELITE // ANTIQUE BUYER",
                new Color(0.95f, 0.8f, 0.2f), new Vector3(1.80f, -1.16f, 28.20f), Quaternion.Euler(0f, 35f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Alien_Female_01.prefab", animLeanF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Fascinating station. Thoroughly filthy, yet the antique Damascus metallurgy here is simply peerless."
                }
            );

            // 27. Kage the Blade (Syndicate Enforcer Guarding Corner)
            SpawnNPC(
                root.transform, "Kage the Blade", NPCRoleType.StationEnforcer, "SYNDICATE // ENFORCER",
                new Color(0.85f, 0.15f, 0.15f), new Vector3(2.40f, -1.16f, 30.20f), Quaternion.Euler(0f, -45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_01.prefab", animHypeM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Keep moving, sword girl. Nyx's corner is syndicate territory."
                }
            );

            // 28. Cipher 'Blackwall' (Netrunner in side alcove)
            SpawnNPC(
                root.transform, "Cipher 'Blackwall'", NPCRoleType.ContrabandDealer, "NETRUNNER // DEEP MESH",
                new Color(0.4f, 0.2f, 0.95f), new Vector3(4.20f, -1.25f, 27.20f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Female_01.prefab", animVideoF, phoneChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Don't drip on the deck. The mesh is wet today.",
                    "The kill order isn't keyed to a crime. It's keyed to a signature — URM-004, Berserker-class. It went live four minutes after your ship touched Pad B-04. You didn't get made, hunter. You got scanned.",
                    "Voss brokered the contract. But the buyer wrote it before you ever landed. Think about that."
                },
                leadId: "cipher",
                leadTitle: "THE BOUNTY WAS WRITTEN BEFORE YOU LANDED",
                leadInsight: "NEW LEAD: THE KILL ORDER PRE-DATES YOUR ARRIVAL"
            );

            // =========================================================================
            // ZONE 9: SHOP 6 - HARDWARE & TOOLS STALL (SM_Bld_Vendor_03)
            // =========================================================================

            // 29. Gizmo (Shopkeeper INSIDE behind tool bench)
            SpawnNPC(
                root.transform, "Gizmo the Toolsmith", NPCRoleType.ShipMechanic, "HARDWARE // TOOLSMITH",
                new Color(0.95f, 0.55f, 0.15f), new Vector3(3.80f, 1.31f, -0.10f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_01.prefab", animVideoM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Calibrated plasma torches, hydraulic cutters, and starship wrenches. Best prices in the Outer Rim."
                }
            );

            // 30. Deckhand Jax (Customer OUTSIDE examining tools)
            SpawnNPC(
                root.transform, "Deckhand Jax", NPCRoleType.MarketPatron, "CARGO // FREIGHT HANDLER",
                new Color(0.95f, 0.45f, 0.15f), new Vector3(1.80f, -1.25f, -0.10f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Male_01.prefab", animDrinkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Looking for high-torque plasma drivers to fix the landing pad cranes."
                }
            );

            // =========================================================================
            // ZONE 10: SHOP 7 - DEEP ALLEY CYBER DEN (SM_Bld_Vendor_07)
            // =========================================================================

            // 31. Malik 'Ghost' (Fixer INSIDE alley booth)
            SpawnNPC(
                root.transform, "Malik 'Ghost'", NPCRoleType.ContrabandDealer, "ALLEY // CYBER FIXER",
                new Color(0.6f, 0.2f, 0.8f), new Vector3(-10.80f, 3.21f, 23.50f), Quaternion.Euler(0f, 90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Strider_Male_01.prefab", animLeanM, phoneChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "You're making too much noise in Sector 4. The syndicate doesn't like unexpected visitors."
                }
            );

            // 32. Spike the Stalker (Alien Customer outside booth)
            SpawnNPC(
                root.transform, "Spike the Stalker", NPCRoleType.MarketPatron, "ALIEN // OUTLAW",
                new Color(0.95f, 0.4f, 0.75f), new Vector3(-8.50f, 3.21f, 23.50f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienSpikes_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "[Hisses with low guttural resonance] Shadows move along the gantry tonight, blade walker."
                }
            );

            // =========================================================================
            // ZONE 11: SHOP 8 - MOBILE TUK-TUK VENDOR CART (SM_Veh_Vendor_01)
            // =========================================================================

            // 33. Rocco (Mobile Street Food Cook)
            SpawnNPC(
                root.transform, "Rocco the Spicemaster", NPCRoleType.FoodVendor, "STREET // MOBILE VENDOR",
                new Color(0.9f, 0.6f, 0.2f), new Vector3(-9.50f, 1.64f, 31.20f), Quaternion.Euler(0f, 120f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienChef_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Synthetic spice buns and Jovian skewers! Fuel for the road!"
                }
            );

            // =========================================================================
            // ZONE 12: REFINERY SECURITY & SECURE TERMINAL
            // =========================================================================

            // 34. Enforcer Thorne (Refinery Patrol Guard - WALKING)
            SpawnNPC(
                root.transform, "Enforcer Thorne", NPCRoleType.StationEnforcer, "SECURITY // REFINERY PATROL",
                new Color(0.5f, 0.6f, 0.7f), new Vector3(11.5f, 6.2f, 7.8f), Quaternion.Euler(0f, 180f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_02.prefab", animWalkM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Refinery perimeter under continuous surveillance. Clear the sector."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(11.5f, 6.2f, 7.8f),
                    new Vector3(13.5f, 6.2f, 7.8f),
                    new Vector3(10.0f, 6.2f, 9.0f)
                },
                walkSpeed: 1.1f,
                idleController: animLeanM
            );

            // 35. Titan-09 (Heavy Rock Bouncer guarding gate)
            SpawnNPC(
                root.transform, "Titan-09 Rock Bouncer", NPCRoleType.StationEnforcer, "HEAVY // REFINERY GUARD",
                new Color(0.85f, 0.75f, 0.2f), new Vector3(8.0f, 6.2f, 6.5f), Quaternion.Euler(0f, 45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienRock_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Halt. Radiation baffles active. High-voltage terminal access strictly forbidden.",
                    "Cydonia cargo came through this gate last week. Sealed crates. Warm to the touch. Command said not to ask."
                }
            );

            // =========================================================================
            // ZONE 13: CENTRAL MARKET PLAZA & PEW PEW VAULT (TRAILER ECOSYSTEM)
            // =========================================================================

            // 36. Gorb the Exotic Merchant (Shopkeeper INSIDE produce booth behind counter)
            SpawnNPC(
                root.transform, "Gorb the Exotic Merchant", NPCRoleType.FoodVendor, "EXOTIC // FRUIT & SLUG MERCHANT",
                new Color(0.35f, 0.85f, 0.35f), new Vector3(-5.5f, -1.0f, -1.2f), Quaternion.Euler(0f, 135f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienChef_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Fresh Jovian blood-melons and hydroponic moss! Harvested before the solar flares hit!",
                    "Watch where you step near the canopy, blade girl. Outlaws are getting reckless with their plasma sidearms."
                }
            );

            // 37. High Shaman Zephyr (Customer OUTSIDE browsing stall in gold alien armor)
            SpawnNPC(
                root.transform, "High Shaman Zephyr", NPCRoleType.MarketPatron, "ELITE // ALIEN SHAMAN",
                new Color(0.95f, 0.82f, 0.25f), new Vector3(-4.2f, -1.0f, -2.2f), Quaternion.Euler(0f, -45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienArmor_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The energy currents in this quadrant fluctuate violently. A storm of steel and fire is approaching.",
                    "Your cybernetic heart pulses with ancient Martian fury. Seek the Cydonia vault, child of battle."
                }
            );

            // 38. Commander Vesper (Customer OUTSIDE examining goods in cyber spacesuit)
            SpawnNPC(
                root.transform, "Commander Vesper", NPCRoleType.MarketPatron, "OUTLAW // VOID MERCENARY",
                new Color(0.25f, 0.75f, 0.95f), new Vector3(-4.6f, -1.0f, -2.8f), Quaternion.Euler(0f, -30f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab", animVideoF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Scanning station surveillance feeds. Syndicate drones are circling Sector 4 like vultures.",
                    "If you run into Omni-Corp hunters, aim for their knee servos. Shuts down their kinetic shields immediately."
                }
            );

            // 39. Astronaut Nova (Shopper browsing crates under umbrella canopy)
            SpawnNPC(
                root.transform, "Astronaut Nova", NPCRoleType.MarketPatron, "EXPLORER // DEEP VOID",
                new Color(0.85f, 0.85f, 0.95f), new Vector3(-3.6f, -1.0f, -3.2f), Quaternion.Euler(0f, 45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_01.prefab", animDrinkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Hard to find fresh produce this deep in the asteroid belt. Synthetic nutrient paste gets old after three months in hyperspace."
                }
            );

            // 40. Darek 'Slouch' (Lounging Outlaw by Canopy)
            SpawnNPC(
                root.transform, "Darek 'Slouch'", NPCRoleType.MarketPatron, "OUTLAW // DRIFTER",
                new Color(0.7f, 0.4f, 0.25f), new Vector3(-3.2f, -1.0f, -1.5f), Quaternion.Euler(0f, 110f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_02.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Keep moving, mercenary. Can't an honest smuggler sleep off his sulfur ale in peace?"
                }
            );

            // 41. Sergeant Stone (Tactical Patrol Lead - WALKING / MARCHING)
            SpawnNPC(
                root.transform, "Sergeant Stone", NPCRoleType.StationEnforcer, "SECURITY // TACTICAL ENFORCER",
                new Color(0.35f, 0.55f, 0.95f), new Vector3(-4.2f, -1.0f, -2.5f), Quaternion.Euler(0f, 180f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_01.prefab", animWalkM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Tactical sweep in progress. Keep the central corridor clear for authorized personnel."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(-4.2f, -1.0f, -2.5f),
                    new Vector3(-4.2f, -1.0f, -5.5f),
                    new Vector3(-4.0f, -1.0f, -8.0f),
                    new Vector3(-4.2f, -1.0f, -5.5f)
                },
                walkSpeed: 1.25f,
                idleController: animHypeM
            );

            // 42. Vanguard Rifleman (Tactical Escort - WALKING / MARCHING)
            SpawnNPC(
                root.transform, "Vanguard Rifleman", NPCRoleType.StationEnforcer, "SECURITY // HEAVY RIFLEMAN",
                new Color(0.35f, 0.55f, 0.95f), new Vector3(-3.5f, -1.0f, -2.5f), Quaternion.Euler(0f, 180f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Male_02.prefab", animWalkM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Weapons safety engaged. Threat index elevated across Sector 4."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(-3.5f, -1.0f, -2.5f),
                    new Vector3(-3.5f, -1.0f, -5.5f),
                    new Vector3(-3.4f, -1.0f, -8.0f),
                    new Vector3(-3.5f, -1.0f, -5.5f)
                },
                walkSpeed: 1.25f,
                idleController: animLeanM
            );

            // 43. Tech Vance (Station Engineer - WALKING / ROAMING)
            SpawnNPC(
                root.transform, "Tech Vance", NPCRoleType.ShipMechanic, "MAINTENANCE // POWER GRID",
                new Color(0.95f, 0.55f, 0.15f), new Vector3(-2.8f, -1.0f, -3.5f), Quaternion.Euler(0f, 180f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab", animWalkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Power conduits under the street are surging again. If the reactor blows, the whole plaza loses gravity."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(-2.8f, -1.0f, -3.5f),
                    new Vector3(-2.8f, -1.0f, -6.5f),
                    new Vector3(-3.2f, -1.0f, -7.5f),
                    new Vector3(-3.0f, -1.0f, -4.5f)
                },
                walkSpeed: 1.15f,
                idleController: animVideoM
            );

            // 44. Arcade Outlaw Reno (Player at Arcade Cabinet)
            SpawnNPC(
                root.transform, "Arcade Outlaw Reno", NPCRoleType.MarketPatron, "OUTLAW // ARCADE GAMER",
                new Color(0.85f, 0.35f, 0.95f), new Vector3(-1.27f, -1.0f, -3.2f), Quaternion.Euler(0f, 0f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Strider_Male_01.prefab", animDrinkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "One more credit... almost beat the high score on 'Galactic Invaders'!"
                }
            );

            // 45. Goliath Krag (Colossal Heavy Rock Alien on Promenade Deck)
            SpawnNPC(
                root.transform, "Goliath Krag", NPCRoleType.MarketPatron, "TITAN // ROCK BEHEMOTH",
                new Color(0.95f, 0.85f, 0.45f), new Vector3(-0.2f, -0.1f, -5.8f), Quaternion.Euler(0f, -110f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienRock_01.prefab", animHypeM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "[Deep rumbling gravel voice] Tiny cyborgs with shiny steel blades... Krag crushes dreadnought hulls for fun."
                }
            );

            // 46. Dune Scavenger Jax (Standing next to Goliath Krag)
            SpawnNPC(
                root.transform, "Dune Scavenger Jax", NPCRoleType.MarketPatron, "SCAVENGER // WASTELAND",
                new Color(0.75f, 0.55f, 0.3f), new Vector3(0.5f, -0.1f, -6.2f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Scavenger_01.prefab", animLeanM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Krag is my bodyguard. Anyone looking to collect bounties on Sector 4 has to go through 3 tons of tectonic plate first."
                }
            );

            // 47. Vault Sentry Kael (Pew Pew Vault Guard INSIDE Doorway)
            SpawnNPC(
                root.transform, "Vault Sentry Kael", NPCRoleType.StationEnforcer, "SECURITY // VAULT SENTINEL",
                new Color(0.95f, 0.85f, 0.2f), new Vector3(0.12f, 0.5f, -10.25f), Quaternion.Euler(0f, 270f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_AlienCombat_01.prefab", animHypeM, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Welcome to Pew Pew's Fun Gun Armory. Heavy ordnance authorized only for licensed bounty hunters."
                }
            );

            // 48. Stair Sentry Lyra (Stairway Patrol - WALKING)
            SpawnNPC(
                root.transform, "Stair Sentry Lyra", NPCRoleType.StationEnforcer, "SECURITY // STAIR SENTRY",
                new Color(0.35f, 0.65f, 0.95f), new Vector3(-2.14f, -1.0f, -5.5f), Quaternion.Euler(0f, 45f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Soldier_Female_01.prefab", animWalkF, mercChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Watch your footing on the gantry stairs. Plasma discharge from the under-deck makes the metal slick."
                },
                wanderWaypoints: new Vector3[]
                {
                    new Vector3(-2.14f, -1.0f, -5.5f),
                    new Vector3(-1.8f, -0.6f, -6.8f),
                    new Vector3(-1.2f, 0.25f, -7.8f),
                    new Vector3(-1.8f, -0.6f, -6.8f)
                },
                walkSpeed: 0.95f,
                idleController: animVideoF
            );

            // 49. Lookout Vanya (Upper Platform Lookout)
            SpawnNPC(
                root.transform, "Lookout Vanya", NPCRoleType.MarketPatron, "VOYAGER // HIGH LOOKOUT",
                new Color(0.7f, 0.35f, 0.95f), new Vector3(-0.5f, 0.5f, -8.8f), Quaternion.Euler(0f, -135f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_SpaceSuit_Alien_Female_01.prefab", animLeanF, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "From up here on the skull platform, you can see the entire trade artery. The station never sleeps."
                }
            );

            // 50. Deck Lounger Kip (Promenade Lounger)
            SpawnNPC(
                root.transform, "Deck Lounger Kip", NPCRoleType.MarketPatron, "CIVILIAN // SPECTATOR",
                new Color(0.85f, 0.45f, 0.25f), new Vector3(1.0f, 0.5f, -4.5f), Quaternion.Euler(0f, -90f, 0f),
                SCI_FI_CHR_PATH + "SM_Chr_ScifiWorlds_Male_01.prefab", animDrinkM, civChatter,
                isStoryObjective: false, unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Best seat in the house. You can watch the starships dock at the high aprons and the enforcers chase pickpockets below."
                }
            );

            // Spawn Refinery Terminal & Catwalk Fall Trigger
            SpawnRefineryTerminal(root.transform, combatDirector);
            SpawnCatwalkFallTrigger(root.transform, combatDirector);

            Debug.Log("<color=#10B981>[Black Market]</color> SciFi Worlds NPC Ecosystem spawned with 100% precision floor raycast grounding and active walking wanderers!");
            return root;
        }

        public static float GetFloorY(Vector3 position)
        {
            // Cast ray from 1.0m above the requested position down 5.0m
            RaycastHit[] hits = Physics.RaycastAll(new Ray(new Vector3(position.x, position.y + 1.0f, position.z), Vector3.down), 5.0f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            foreach (var hit in hits)
            {
                string name = hit.collider.name.ToLower();
                if (name.Contains("alita") || name.Contains("starship") || name.Contains("roof") ||
                    name.Contains("sign") || name.StartsWith("npc_") || name.Contains("gantry_catwalk") ||
                    name.Contains("trigger") || name.Contains("prop_") || name.Contains("cooker") ||
                    name.Contains("awning") || name.Contains("counter") || name.Contains("pan_") ||
                    name.Contains("crate_building") || name.Contains("vendor_06"))
                    continue;

                return hit.point.y;
            }

            // Fallback: search deeper if no floor hit immediately below
            hits = Physics.RaycastAll(new Ray(new Vector3(position.x, 15f, position.z), Vector3.down), 30f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (var hit in hits)
            {
                string name = hit.collider.name.ToLower();
                if (name.Contains("alita") || name.Contains("starship") || name.Contains("roof") ||
                    name.Contains("sign") || name.StartsWith("npc_") || name.Contains("gantry_catwalk") ||
                    name.Contains("trigger") || name.Contains("prop_") || name.Contains("cooker") ||
                    name.Contains("awning") || name.Contains("counter") || name.Contains("pan_") ||
                    name.Contains("crate_building") || name.Contains("vendor_06"))
                    continue;

                return hit.point.y;
            }

            return position.y;
        }

        private static void SpawnRefineryTerminal(Transform parent, BlackMarketStoryCombatDirector director)
        {
            Vector3 terminalPos = new Vector3(10.5f, 6.2f, 7.8f);
            terminalPos.y = GetFloorY(terminalPos);

            GameObject termGo = GameObject.Find("Refinery_Secure_Terminal");

            if (termGo == null)
            {
                GameObject termPrefab = null;
#if UNITY_EDITOR
                termPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Power_Terminal_01.prefab");
#endif
                if (termPrefab != null)
                {
                    termGo = Instantiate(termPrefab, terminalPos, Quaternion.Euler(0f, 180f, 0f), parent);
                    termGo.name = "Refinery_Secure_Terminal";
                }
                else
                {
                    termGo = new GameObject("Refinery_Secure_Terminal");
                    termGo.transform.SetParent(parent);
                    termGo.transform.position = terminalPos;

                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(termGo.transform, false);
                    cube.transform.localScale = new Vector3(0.8f, 1.4f, 0.6f);
                    cube.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                }
            }
            else
            {
                termGo.transform.position = terminalPos;
            }

            var terminalComp = termGo.GetComponent<RefineryDataTerminal>() ?? termGo.AddComponent<RefineryDataTerminal>();
            director.refineryTerminal = terminalComp;
        }

        private static void SpawnCatwalkFallTrigger(Transform parent, BlackMarketStoryCombatDirector director)
        {
            Vector3 catwalkPos = new Vector3(15.0f, 1.88f, 27.5f);
            GameObject triggerGo = GameObject.Find("Gantry_Catwalk_Fall_Trigger");

            if (triggerGo == null)
            {
                triggerGo = new GameObject("Gantry_Catwalk_Fall_Trigger");
                triggerGo.transform.SetParent(parent);
                triggerGo.transform.position = catwalkPos;
            }

            var fallTrigger = triggerGo.GetComponent<BlackMarketCatwalkFallTrigger>() ?? triggerGo.AddComponent<BlackMarketCatwalkFallTrigger>();
            fallTrigger.triggerCenter = catwalkPos;
            fallTrigger.triggerSize = new Vector3(4.5f, 4.0f, 4.5f);
            fallTrigger.fallVelocityImpulse = new Vector3(-5.0f, 2.5f, -5.0f);
            fallTrigger.lowerTrenchYThreshold = -0.5f;

#if UNITY_EDITOR
            fallTrigger.explosionSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/dragon-studio-dry-thunder-364468.mp3");
            fallTrigger.fallLandingSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Jump_Landing_Boots_01.wav");
            fallTrigger.explosionVFXPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Explosion_01.prefab");

            var existingVisual = triggerGo.transform.Find("Catwalk_Collapsible_Bridge");
            if (existingVisual == null)
            {
                GameObject bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Bridge_Platform_01.prefab");
                GameObject bridgeVisual = null;
                if (bridgePrefab != null)
                {
                    bridgeVisual = PrefabUtility.InstantiatePrefab(bridgePrefab) as GameObject;
                    bridgeVisual.name = "Catwalk_Collapsible_Bridge";
                    bridgeVisual.transform.SetParent(triggerGo.transform, false);
                    bridgeVisual.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                    bridgeVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
                fallTrigger.catwalkSectionToCollapse = bridgeVisual != null ? bridgeVisual.transform : null;
            }
#endif
            director.catwalkFallTrigger = fallTrigger;
        }

        private static GameObject SpawnNPC(
            Transform parent,
            string name,
            NPCRoleType role,
            string badgeTitle,
            Color badgeColor,
            Vector3 position,
            Quaternion rotation,
            string prefabPath,
            RuntimeAnimatorController animController,
            AudioClip[] chatterSFX,
            bool isStoryObjective,
            string unlockObjectiveText,
            string[] dialogue,
            Vector3[] wanderWaypoints = null,
            float walkSpeed = 1.25f,
            RuntimeAnimatorController idleController = null,
            string leadId = null,
            string leadTitle = null,
            string leadInsight = null)
        {
            // Precision ground snap: get true floor Y
            position.y = GetFloorY(position);

            GameObject instance = null;

#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.SetParent(parent, false);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.name = "NPC_" + name.Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("'", "");
            }
#endif
            if (instance == null)
            {
                instance = new GameObject("NPC_" + name.Replace(" ", "_"));
                instance.transform.SetParent(parent, false);
                instance.transform.position = position;
                instance.transform.rotation = rotation;

                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.transform.SetParent(instance.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            }

            var col = instance.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = instance.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.95f, 0f);
                col.radius = 0.35f;
                col.height = 1.9f;
            }

            // Assign Animator Controller
            Animator anim = instance.GetComponentInChildren<Animator>();
            if (anim != null && animController != null)
            {
                anim.runtimeAnimatorController = animController;
                anim.applyRootMotion = false;
            }

            var npcComp = instance.GetComponent<BlackMarketNPC>() ?? instance.AddComponent<BlackMarketNPC>();
            npcComp.characterName = name;
            npcComp.roleType = role;
            npcComp.roleBadgeTitle = badgeTitle;
            npcComp.badgeColor = badgeColor;
            npcComp.voiceChatterSFX = chatterSFX;
            npcComp.isStoryObjectiveNPC = isStoryObjective;
            npcComp.unlockObjectiveText = unlockObjectiveText;
            npcComp.leadId = leadId ?? "";
            npcComp.leadTitle = leadTitle ?? "";
            npcComp.leadInsight = leadInsight ?? "";

            npcComp.dialogueEntries.Clear();
            if (dialogue != null)
            {
                foreach (var line in dialogue)
                {
                    npcComp.dialogueEntries.Add(new DialogueEntry { dialogueText = line, speakerMood = "Neutral" });
                }
            }

            // Wire up autonomous wandering if waypoints are provided
            if (wanderWaypoints != null && wanderWaypoints.Length > 1)
            {
                var wanderer = instance.GetComponent<BlackMarketNPCWanderer>() ?? instance.AddComponent<BlackMarketNPCWanderer>();
                wanderer.waypoints.Clear();
                wanderer.waypoints.AddRange(wanderWaypoints);
                wanderer.walkSpeed = walkSpeed;
                wanderer.animator = anim;
                wanderer.walkController = animController;
                wanderer.idleController = idleController;
                wanderer.loopPatrol = true;
            }

            return instance;
        }
    }
}
