#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyntyCombined.BlackMarket;
using Cyberpunk.World;

namespace SyntyCombined.Editor
{
    /// <summary>
    /// Automated Black Market NPC & Shopkeeper Populator.
    /// Spawns a complete, authentic cyberpunk market ecosystem in Demo_BlackMarket_01.unity:
    /// - Authentic, interactive shopkeepers stationed inside EVERY shop, stall, kiosk, and bar
    /// - Expressive, autonomous pedestrians walking smoothly along multi-point patrol routes
    /// - Custom role badges, cyberware/tech/street food dialogue, and animated idle postures
    /// </summary>
    [InitializeOnLoad]
    public static class BlackMarketNPCPopulator
    {
        [MenuItem("Game/Populate Black Market NPCs & Shopkeepers")]
        public static void PopulateBlackMarketNPCs()
        {
            string scenePath = "Assets/Synty/PolygonSciFiWorlds/Scenes/Demo_BlackMarket_01.unity";
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name != "Demo_BlackMarket_01")
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // 1. Locate or create root container
            GameObject root = GameObject.Find("[BlackMarket_NPC_Ecosystem]");
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            root = new GameObject("[BlackMarket_NPC_Ecosystem]");
            Undo.RegisterCreatedObjectUndo(root, "Populate Black Market NPCs");

            // Ensure Dialogue HUD exists in scene
            var hud = Object.FindFirstObjectByType<BlackMarketNPCDialogueHUD>();
            if (hud == null)
            {
                GameObject hudGo = new GameObject("[BlackMarket_Dialogue_HUD]");
                hudGo.AddComponent<BlackMarketNPCDialogueHUD>();
                Undo.RegisterCreatedObjectUndo(hudGo, "Create BlackMarket Dialogue HUD");
            }

            // ==============================================================
            // 2. CORE STORY NPC: SPACESUIT DECK TECH SILAS (Stationed next to Landing Pad 01)
            // (ONLY this NPC and The Collector give the option to talk with Alita)
            // ==============================================================
            SpawnNPC(
                root.transform,
                "Deck Tech Silas",
                NPCRoleType.ShipMechanic,
                "SPACESUIT TECH // PAD 01",
                new Color(0.95f, 0.60f, 0.15f), // Industrial Amber
                new Vector3(21.80f, 1.86f, 31.80f), // Directly beside Landing Pad 01, right next to Alita
                Quaternion.Euler(0f, 30f, 0f), // Facing Alita as she steps off the starship
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Male_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab",
                defaultIdleId: 7, // Hands On Hips / resting
                isStoryObjective: false,
                unlockObjectiveText: "MISSION UPDATED: Head down the gantry stairs to the Collector's armored pod with the alien skull",
                dialogue: new string[]
                {
                    "Alita: \"Hey. Where do I find the Collector around here?\"",
                    "\"Looking for Voss? Head down the gantry stairs past the market stalls. Look for the armored pod with that big alien skull over the door. Can't miss it—two heavy alien bruisers are standing guard outside.\"",
                    "Alita: \"Understood. Thanks.\"",
                    "\"Watch your back in there. Nobody walks out of Voss's den without losing something.\""
                },
                patrolWaypoints: null,
                canTalk: true, // Talkable!
                interactionRange: 5.0f
            );

            // ==============================================================
            // 3. CORE STORY NPC: THE COLLECTOR (Stationed in Trophy Sanctuary Pod)
            // ==============================================================
            SpawnNPC(
                root.transform,
                "The Collector (Kaelen Voss)",
                NPCRoleType.StoryFixer,
                "THE COLLECTOR // ALIEN RELIC OVERLORD",
                new Color(0.95f, 0.75f, 0.15f), // Radiant Amber Gold
                new Vector3(4.8f, 0.28f, -7.8f),
                Quaternion.Euler(0f, -125f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienArmor_01.prefab",
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_Alien_01.prefab",
                defaultIdleId: 3, // Arms Folded Casual / Master of the Den
                isStoryObjective: true,
                unlockObjectiveText: "MISSION ACTIVE: Head to Western Flight Deck, Board Starship & Retrieve 4 Core Matrices [0/4]",
                dialogue: new string[]
                {
                    "Alita: \"You're Voss. I hear you have the jump coordinates to Mars.\"",
                    "\"Word travels fast. Yeah, I've got the nav-data locked in my terminal. But nothing on Drift is free, hunter.\"",
                    "Alita: \"What's your price?\"",
                    "\"Omni-Corp raided my fuel depot and scattered four power cores into the debris field outside. Take the ship on the western pad, use the tractor beam [T], and slot them into the main reactor. Do that, and the coordinates are yours.\"",
                    "Alita: \"Consider it done.\""
                },
                patrolWaypoints: null,
                canTalk: true, // Talkable!
                interactionRange: 5.5f
            );

            // Atmospheric lighting inside The Collector's Trophy Pod
            var podLightObj = GameObject.Find("Collector_Den_Ambient_Light");
            if (podLightObj == null)
            {
                podLightObj = new GameObject("Collector_Den_Ambient_Light");
                podLightObj.transform.SetParent(root.transform);
                podLightObj.transform.position = new Vector3(5.0f, 2.2f, -8.5f);
                var pl = podLightObj.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1.0f, 0.85f, 0.55f); // Warm amber
                pl.range = 7.5f;
                pl.intensity = 2.4f;

                var screenLightObj = new GameObject("Collector_Den_Terminal_Glow");
                screenLightObj.transform.SetParent(root.transform);
                screenLightObj.transform.position = new Vector3(4.6f, 1.2f, -9.1f);
                var sl = screenLightObj.AddComponent<Light>();
                sl.type = LightType.Point;
                sl.color = new Color(0.1f, 0.85f, 1.0f); // Hologram cyan
                sl.range = 3.5f;
                sl.intensity = 1.2f;
            }

            // ==============================================================
            // 3b. ALIEN BODYGUARDS (Stationed outside The Collector's Sanctuary)
            // Flanking the main pod entrance at (-0.13, 0.50, -10.25)
            // ==============================================================
            var guard1 = SpawnNPC(
                root.transform,
                "Alien Bodyguard Vorgash",
                NPCRoleType.StationEnforcer,
                "WARLORD ELITE // BODYGUARD",
                new Color(0.95f, 0.35f, 0.15f), // Flame Orange
                new Vector3(-1.85f, -0.11f, -8.35f),
                Quaternion.Euler(0f, -120f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienArmor_01.prefab",
                defaultIdleId: 28, // Aggressive military stance
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Halt, cybernetic intruder. You enter Warlord Voss's sanctuary only on official business.",
                    "Keep your blade sheathed inside. One wrong move and my pulse rifle disintegrates your core."
                },
                patrolWaypoints: null,
                canTalk: false // Silent bodyguard
            );
            AttachWeaponToHand(guard1, "Assets/PolygonSciFiSpace/Prefabs/Weapons/SM_Wep_Rifle_01.prefab");

            var guard2 = SpawnNPC(
                root.transform,
                "Alien Bodyguard Krogan",
                NPCRoleType.StationEnforcer,
                "WARLORD ELITE // BODYGUARD",
                new Color(0.95f, 0.35f, 0.15f),
                new Vector3(-1.85f, -0.11f, -12.15f),
                Quaternion.Euler(0f, -60f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienSpikes_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 8, // Hands on hips / vigilant sentry posture
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The Collector granted you passage, warrior. Do not test his patience.",
                    "We have crushed corporate strike squads for less than a crooked glance."
                },
                patrolWaypoints: null,
                canTalk: false // Silent bodyguard
            );
            AttachWeaponToHand(guard2, "Assets/PolygonSciFiSpace/Prefabs/Weapons/SM_Wep_Shotgun_01.prefab");

            // Configure Collector pod doors: open ONLY when Alita walks up very close (< 2.2m)
            ConfigureCollectorPodDoors();

            // Concourse Enforcer Captain: Thorne
            SpawnNPC(
                root.transform,
                "Enforcer Thorne",
                NPCRoleType.StationEnforcer,
                "SECURITY // STATION ENFORCER",
                new Color(0.5f, 0.6f, 0.7f),
                new Vector3(11.2f, 1.88f, 7.5f),
                Quaternion.Euler(0f, 180f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_013_Gideon_Rezvani.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 28, // Posture Aggressive
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Weapons stay sheathed on the central concourse. Sector 4 has rules, even for Martian veterans.",
                    "Keep the peace while you're resupplying. Station turrets don't ask questions."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(11.2f, 1.88f, 7.5f),
                    new Vector3(14.0f, 1.88f, 12.0f),
                    new Vector3(15.0f, 1.88f, 18.0f),
                    new Vector3(12.0f, 1.88f, 14.0f)
                }
            );

            // Bounty Hunter: Orion Ward
            SpawnNPC(
                root.transform,
                "Orion Ward",
                NPCRoleType.StationEnforcer,
                "BOUNTY // GUILD HUNTER",
                new Color(0.85f, 0.2f, 0.2f),
                new Vector3(-1.8f, 1.88f, 32.5f),
                Quaternion.Euler(0f, 135f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_006_Orion_Ward.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 12, // Arms Folded Grumpy
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "There's a 200,000 credit bounty on rogue Martian chassis. But relax... I don't hunt Berserkers. Too messy.",
                    "If you're flying out to the broken dome, watch for Omni-Corp interceptors. They shoot first and salvage later."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-1.8f, 1.88f, 32.5f),
                    new Vector3(-6.0f, 1.88f, 28.0f),
                    new Vector3(-10.0f, 1.88f, 24.0f),
                    new Vector3(-4.0f, 1.88f, 30.0f)
                }
            );

            // Western Gantry Broker: Reeve Blackwood
            SpawnNPC(
                root.transform,
                "Reeve Blackwood",
                NPCRoleType.ContrabandDealer,
                "CARGO // WESTERN BROKER",
                new Color(0.7f, 0.4f, 0.95f),
                new Vector3(-12.5f, 0.05f, 16.8f),
                Quaternion.Euler(0f, 40f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_007_Reeve_Blackwood.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_02.prefab",
                defaultIdleId: 14, // Lean Back Arms Folded
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Looking for the Western Flight Deck? Head down the ramp and across the gantry. The SS-04 Nightshade Striker is prepped on Pad 02.",
                    "That ship is retrofitted with electromagnetic tractor clamps to pull asteroid core debris straight out of open space."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-12.5f, 0.05f, 16.8f),
                    new Vector3(-18.0f, 0.05f, 12.0f),
                    new Vector3(-22.0f, 0.05f, 6.0f),
                    new Vector3(-15.0f, 0.05f, 10.0f)
                }
            );

            // ==============================================================
            // 3. AUTHENTIC SHOPKEEPERS IN EVERY SHOP, STALL & KIOSK
            // ==============================================================

            // SHOP 1: Central Ramen & Dumpling Stall (SM_Bld_Vendor_01 at 0.53, -0.21, 14.88)
            SpawnNPC(
                root.transform,
                "Chef Gorn",
                NPCRoleType.FoodVendor,
                "FOOD // NOODLE MASTER",
                new Color(0.95f, 0.2f, 0.65f), // Radiant Hot Pink
                new Vector3(1.15f, -0.21f, 14.88f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienChef_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_001_Sora_Voltage.prefab",
                defaultIdleId: 27, // Eat / Cook Hold
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Hot sulfur-grub noodles and spiced plasma broth! The only meal on Sector 4 that'll reboot your neural processor!",
                    "Eat up, cyborg girl. The corporate fleets won't wait for your stomach to settle! Ha ha!"
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 2: Main Concourse Arms Broker (SM_Bld_Vendor_02 (1) at 1.21, -1.25, 23.90)
            SpawnNPC(
                root.transform,
                "Vex 'Trigger' Malakor",
                NPCRoleType.WeaponsMerchant,
                "WEAPONS // ARMS BROKER",
                new Color(0.95f, 0.25f, 0.25f), // Crimson red
                new Vector3(1.75f, -1.25f, 23.90f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_004_Jett_Chromium.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 6, // Thoughtful / Inspecting weapon
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Looking for firepower, hunter? Out here on Sector 4, your blade is only as sharp as your reflexes.",
                    "I see you carry genuine Damascus steel. Magnificent craftsmanship. If you're heading toward the Martian warzone, make sure you keep your plasma edge charged."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 3: Plaza Armor & Dermal Shielding Smith (SM_Bld_Vendor_02 (2) at 9.26, -1.58, 29.19)
            SpawnNPC(
                root.transform,
                "Torg 'Anvil' Karr",
                NPCRoleType.WeaponsMerchant,
                "ARMOR // SHIELDING SMITH",
                new Color(0.98f, 0.65f, 0.15f), // Golden Amber
                new Vector3(9.26f, -1.58f, 28.55f),
                Quaternion.Euler(0f, 0f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_003_Silas_Titan.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 7, // Hands On Hips / Craftsman
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Kinetic plating, ballistic ceramic weaves, and ionized reactive mesh. When an Omni-Corp gunship opens up with 30mm tungsten rounds, you'll thank me.",
                    "That Damascus blade of yours... I could reinforce the hilt crossguard with titanium-carbide if you find spare scrap."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 4: Southwest Gantry Hardware Depot (SM_Bld_Vendor_02 at -12.25, -1.25, 3.10)
            SpawnNPC(
                root.transform,
                "Silas Vance",
                NPCRoleType.ShipMechanic,
                "TECH // HARDWARE SALVAGE",
                new Color(0.90f, 0.55f, 0.10f), // Industrial Amber
                new Vector3(-12.85f, -1.25f, 3.10f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_020_Kestrel_Spire.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_02.prefab",
                defaultIdleId: 5, // Datapad Diagnostics
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "High-output solenoid relays, magnetic induction coils, atmospheric seals... if it was bolted to a ship, my salvagers pried it loose.",
                    "Watch your step on the lower gantry. The magnetic locks are leaking current."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 5: Lower Promenade Propulsion Broker (SM_Bld_Vendor_03 at 3.02, -1.29, -0.10)
            SpawnNPC(
                root.transform,
                "Jaxom 'Sparks' Cole",
                NPCRoleType.ShipMechanic,
                "PARTS // PROPULSION BROKER",
                new Color(0.20f, 0.70f, 1.0f), // Electric Cyan
                new Vector3(2.40f, -1.29f, -0.10f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_011_Jax_Pulse.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                defaultIdleId: 4, // Look Around
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Need hyperdrive injectors or sub-light cooling baffles? I pull the best military surplus from the scrap barges.",
                    "Keep an eye on your heat sinks when you pull high-G turns in the asteroid ring. Vacuum doesn't dissipate thermal build-up easily."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 6: Lower Alley Scrap Salvage Shop (SM_Bld_Vendor_03 (1) at -10.51, -1.29, -8.89)
            SpawnNPC(
                root.transform,
                "Old Man Rusty",
                NPCRoleType.MarketPatron,
                "SALVAGE // DERELICT SCRAP",
                new Color(0.85f, 0.45f, 0.25f), // Rusted Copper
                new Vector3(-11.15f, -1.29f, -8.89f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_002_Bex_Cross.prefab",
                defaultIdleId: 14, // Slumped / Leaning Back
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "One spacer's wreckage is another scavenger's retirement. Dug up pieces of a pre-war Martian cruiser yesterday.",
                    "Careful poking around the southern abyss. Strange energy echoes down there."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 7: Central Covered Bazaar Stall (SM_Bld_Vendor_04 at 2.46, -1.40, 19.80)
            SpawnNPC(
                root.transform,
                "Madame Zara",
                NPCRoleType.StoryFixer,
                "BAZAAR // MARKET OVERSEER",
                new Color(0.75f, 0.25f, 0.95f), // Royal Purple
                new Vector3(2.20f, -1.40f, 19.80f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_016_Raven_Chen.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Female_01.prefab",
                defaultIdleId: 3, // Arms Folded Casual / Authoritative
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Welcome to Sector 4's grand bazaar. Everything here has a price, and everyone has a past they're trying to outrun.",
                    "You're looking for The Collector? He keeps his office inside the skull-marked relic pod down the southwest corridor. Don't touch his trophies unless you want to lose an arm."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 8: North Plaza Cybernetics Kiosk (SM_Bld_Vendor_05 at 3.64, -1.11, 28.47)
            SpawnNPC(
                root.transform,
                "Dr. Lexa Frost",
                NPCRoleType.Ripperdoc,
                "MED // BIO-SYNTHESIS",
                new Color(0.1f, 0.95f, 0.7f), // Mint Cyan
                new Vector3(4.10f, -1.11f, 27.90f),
                Quaternion.Euler(0f, -36f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_030_Lexa_Frost.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Female_01.prefab",
                defaultIdleId: 6, // Thoughtful / Chemical vial check
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The atmospheric radiation in the asteroid ring is elevated today. Keep your synthetic dermal shielding engaged.",
                    "If you sustain critical cybernetic trauma, the nanites in your heart chassis should trigger automatic emergency triage."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 9: West Alleyway Contraband Stall (SM_Bld_Vendor_06 (2) at -6.74, -1.25, -0.74)
            SpawnNPC(
                root.transform,
                "Nyx 'Shadow'",
                NPCRoleType.ContrabandDealer,
                "OUTLAW // CONTRABAND",
                new Color(0.6f, 0.35f, 0.95f), // Dark Violet
                new Vector3(-7.45f, -1.25f, -0.74f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_038_Nyx_Ignite.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                defaultIdleId: 14, // Lean Back Arms Folded
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Keep your voice down. Corporate surveillance drones have audio-taps in every ventilation grate.",
                    "If you need military-grade reflex boosters or untraceable neural stims before your drop on Mars, I'm your contact."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 10: West Alleyway Neural Stims Dealer (SM_Bld_Vendor_06 (3) at -6.74, -1.25, 7.51)
            SpawnNPC(
                root.transform,
                "Dex 'Zero' Mercer",
                NPCRoleType.ContrabandDealer,
                "PHARMA // NEURAL STIMS",
                new Color(0.35f, 0.95f, 0.35f), // Acid Green
                new Vector3(-6.05f, -1.25f, 7.51f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_008_Nadia_Vortex.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                defaultIdleId: 1, // Phone / Datapad Scroll
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Reflex enhancers, adrenaline surges, overclocked neural synapses. When the shooting starts, you'll experience time at half speed.",
                    "Corporate bounties don't scare me. The station enforcers know who keeps their med-bays stocked."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 11: Ripperdoc Cyber-Clinic & Droid Tech (SM_Bld_Vendor_07 (2) at -10.22, -0.73, 23.49)
            SpawnNPC(
                root.transform,
                "Dr. Aris Thorne",
                NPCRoleType.Ripperdoc,
                "MED // CHIEF RIPPERDOC",
                new Color(0.2f, 0.85f, 0.5f), // Medical Emerald
                new Vector3(-10.60f, -0.73f, 23.49f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_012_Vektor_Kowalski.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Female_01.prefab",
                defaultIdleId: 1, // Datapad Scroll / Medical Scan
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Fascinating bio-mechanical chassis... Martian Berserker design, isn't it? Pre-war URM nanotech.",
                    "Your neural circuits are running on emergency reserves without your central core. Retrieve it from Cydonia, and your body will unleash its true kinetic overdrive."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 12: Western Heavy Droid & Mech Repair Depot (SM_Bld_Vendor_07 (1) at -37.96, -1.29, -5.54)
            SpawnNPC(
                root.transform,
                "Magnus Holt",
                NPCRoleType.ShipMechanic,
                "TECH // MECH ENGINEER",
                new Color(0.98f, 0.45f, 0.05f), // Safety Orange
                new Vector3(-38.30f, -1.29f, -5.54f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_010_Vaughn_Cruz.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 7, // Hands on Hips / Heavy Mechanic
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "I tune the hydraulic servomotors for the heavy cargo loaders. If you're heading to Mars, prepare for Titan-class security mechs.",
                    "Their armor is reinforced across the torso, but the knee actuators and neck joints are vulnerable to high-frequency plasma blades."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 13: Plaza Cyber Bar (SM_Bld_Vendor_08 at 6.34, -1.28, 6.06)
            SpawnNPC(
                root.transform,
                "Balthazar 'Neon' Cruz",
                NPCRoleType.FoodVendor,
                "BAR // NEON SPIRITS",
                new Color(0.95f, 0.15f, 0.85f), // Neon Magenta
                new Vector3(5.80f, -1.28f, 6.06f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_014_Zane_Zero.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                defaultIdleId: 26, // Drink Hold / Counter wipe
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "What'll it be, warrior? Liquid nitrogen gin, distilled Martian agave, or pure battery-grade synthetic rum?",
                    "The landing pad out there has been on high alert ever since your ship touched down. Word is Omni-Corp has eyes on every gantry."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 14: Western Moored Merchant Barge (SM_Veh_Vendor_Tug_01 (7) at -14.90, 2.10, -2.72)
            SpawnNPC(
                root.transform,
                "Captain Drake",
                NPCRoleType.ShipMechanic,
                "OUTFITTER // SHIP SUPPLIES",
                new Color(0.25f, 0.55f, 0.95f), // Cobalt Blue
                new Vector3(-14.30f, 2.10f, -2.72f),
                Quaternion.Euler(0f, 180f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_006_Orion_Ward.prefab",
                defaultIdleId: 3, // Arms Folded Spacer
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Welcome aboard the Red Horizon outfitting barge. We supply long-range hyperspace fuel, plasma thruster nozzles, and heat shielding.",
                    "Your starship's flight systems look tuned for high-velocity combat. Be ready when you jump to Martian orbit."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // SHOP 15: Mobile Scrap Trader Cart (SM_Veh_Vendor_01 at -9.54, -0.73, 30.20)
            SpawnNPC(
                root.transform,
                "Scrappy Pip",
                NPCRoleType.MarketPatron,
                "TRADER // SCRAP HAULER",
                new Color(0.95f, 0.80f, 0.20f), // Yellow-Amber
                new Vector3(-10.60f, -0.73f, 30.80f),
                Quaternion.Euler(0f, 30f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_002_Bex_Cross.prefab",
                defaultIdleId: 4, // Look Around
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Fresh salvage from the lower catwalks! Droid optics, fiber-optic bundles, and uncracked memory crystals!",
                    "Everything in this cart is 100% legal... as long as the Enforcers are on coffee break."
                },
                patrolWaypoints: null,
                canTalk: true,
                interactionRange: 3.2f
            );

            // ==============================================================
            // 4. AUTONOMOUS PATROLLING WANDERERS (Walking & Browsing Stalls)
            // ==============================================================

            // Wanderer 1: Concourse Patrol Enforcer (Patrolling Central Market Concourse)
            SpawnNPC(
                root.transform,
                "Patrol Guard Gideon",
                NPCRoleType.StationEnforcer,
                "SECURITY // CONCOURSE PATROL",
                new Color(0.45f, 0.65f, 0.85f),
                new Vector3(14.0f, 1.88f, 18.0f),
                Quaternion.Euler(0f, 180f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_013_Gideon_Rezvani.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 7,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Concourse patrol is clear. Move along, citizen.",
                    "Keep your plasma blade in its magnetic scabbard while passing through the market."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(14.0f, 1.88f, 18.0f),
                    new Vector3(11.0f, 1.88f, 14.0f),
                    new Vector3(11.0f, 1.88f, 8.0f),
                    new Vector3(14.0f, 1.88f, 12.0f)
                }
            );

            // Wanderer 2: Marketplace Shopper & Courier (Browsing between food stalls and arms counter)
            SpawnNPC(
                root.transform,
                "Courier Sora",
                NPCRoleType.MarketPatron,
                "COURIER // MARKET SHOPPER",
                new Color(0.95f, 0.85f, 0.2f),
                new Vector3(14.0f, 1.88f, 16.0f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_001_Sora_Voltage.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                defaultIdleId: 1,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Got deliveries for the food stalls and weapons racks. Busy day on the concourse!",
                    "Chef Gorn makes the best spicy noodles on the station. Give them a try!"
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(14.0f, 1.88f, 16.0f),
                    new Vector3(10.0f, 1.88f, 14.0f),
                    new Vector3(2.0f, -0.21f, 14.0f),
                    new Vector3(2.0f, -1.25f, 20.0f),
                    new Vector3(10.0f, 1.88f, 22.0f)
                }
            );

            // Wanderer 3: Western Gantry Drifter (Walking between North Plaza and Western Gantry)
            SpawnNPC(
                root.transform,
                "Drifter Jax",
                NPCRoleType.MarketPatron,
                "OUTLAW // DRIFTER",
                new Color(0.6f, 0.8f, 0.95f),
                new Vector3(8.0f, 1.88f, 28.0f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_011_Jax_Pulse.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                defaultIdleId: 4,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Just watching the stars drift past the fractured dome. Peaceful out here on the edge of the market.",
                    "If you take the western ramp down, you can reach the secondary flight deck."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(8.0f, 1.88f, 28.0f),
                    new Vector3(1.0f, 1.88f, 30.0f),
                    new Vector3(-6.0f, 0.8f, 28.0f),
                    new Vector3(-12.0f, 0.05f, 22.0f),
                    new Vector3(-4.0f, 1.2f, 28.0f)
                }
            );

            // Wanderer 4: Cyber Bar Stroller (Walking between Cyber Bar and Lower Promenade)
            SpawnNPC(
                root.transform,
                "Raven Chen",
                NPCRoleType.MarketPatron,
                "SYNTHWAVE // NIGHTOWL",
                new Color(1f, 0.2f, 0.8f),
                new Vector3(12.0f, 1.88f, 10.0f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_016_Raven_Chen.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                defaultIdleId: 26,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Synthe-gin with liquid neon glow... only way to keep warm in a hollow asteroid shell.",
                    "The music from the bar reverberates nicely down the promenade."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(12.0f, 1.88f, 10.0f),
                    new Vector3(8.0f, -1.28f, 6.0f),
                    new Vector3(2.0f, -1.28f, 4.0f),
                    new Vector3(4.0f, -1.29f, -1.0f),
                    new Vector3(10.0f, 1.88f, 7.0f)
                }
            );

            // Wanderer 5: West Alleyway Netrunner (Traversing the narrow contraband alleys)
            SpawnNPC(
                root.transform,
                "Nadia Vortex",
                NPCRoleType.ContrabandDealer,
                "NETRUNNER // DATA RUNNER",
                new Color(0f, 0.95f, 1f),
                new Vector3(-4.0f, -1.25f, 2.0f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_008_Nadia_Vortex.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                defaultIdleId: 1,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Decrypting the station's main orbital radar right now. Four unstable energy matrix signatures detected drifting in space.",
                    "Stay light on your feet in these back alleys. Corporate wire-taps are everywhere."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-4.0f, -1.25f, 2.0f),
                    new Vector3(-6.0f, -1.25f, 6.0f),
                    new Vector3(-8.0f, -1.25f, 12.0f),
                    new Vector3(-8.0f, -0.73f, 20.0f),
                    new Vector3(-5.0f, -1.25f, 10.0f)
                }
            );

            // Wanderer 6: North Plaza Shopper (Browsing armor and cyberware kiosks)
            SpawnNPC(
                root.transform,
                "Bex Cross",
                NPCRoleType.MarketPatron,
                "SCAVENGER // TECH SCOUT",
                new Color(0.8f, 0.85f, 0.2f),
                new Vector3(4.0f, 1.88f, 32.0f),
                Quaternion.Euler(0f, -110f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_015_Nova_Static.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Male_01.prefab",
                defaultIdleId: 27,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Checking out the new shield generators at Torg's stall. Expensive, but worth every credit.",
                    "Station's buzzing today. Ever since you dropped out of hyperspace, everyone's watching the landing pads."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(4.0f, 1.88f, 32.0f),
                    new Vector3(8.0f, -1.58f, 30.0f),
                    new Vector3(12.0f, 1.88f, 26.0f),
                    new Vector3(6.0f, 1.88f, 24.0f)
                }
            );

            // Wanderer 7: Western Flight Deck Guard (Patrolling the long western gantry)
            SpawnNPC(
                root.transform,
                "Enforcer Jax",
                NPCRoleType.StationEnforcer,
                "SECURITY // WESTERN GANTRY",
                new Color(0.5f, 0.6f, 0.7f),
                new Vector3(-15.0f, 0.05f, 18.0f),
                Quaternion.Euler(0f, 180f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_014_Zane_Zero.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_02.prefab",
                defaultIdleId: 7,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Western gantry is restricted to authorized pilots and cargo handlers.",
                    "If you have clearance for Pad 02, proceed without loitering."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-15.0f, 0.05f, 18.0f),
                    new Vector3(-22.0f, 0.05f, 14.0f),
                    new Vector3(-28.0f, 0.05f, 8.0f),
                    new Vector3(-22.0f, 0.05f, 14.0f)
                }
            );

            // Wanderer 8: Lower Scrap Salvager (Patrolling the lower scrap stalls and trench)
            SpawnNPC(
                root.transform,
                "Zero Stryker",
                NPCRoleType.MarketPatron,
                "RONIN // WANDERING BLADE",
                new Color(0.9f, 0.9f, 0.95f),
                new Vector3(0.0f, -1.29f, -2.0f),
                Quaternion.Euler(0f, 75f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_022_Zero_Stryker.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 3,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Your blade cuts true, cyborg. Damascus folding absorbs kinetic recoil that would shatter common carbide steel.",
                    "Trust your edge in the storm ahead. Steel does not rust in the vacuum."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(0.0f, -1.29f, -2.0f),
                    new Vector3(-5.0f, -1.29f, -5.0f),
                    new Vector3(-9.0f, -1.29f, -8.0f),
                    new Vector3(-4.0f, -1.29f, -4.0f)
                }
            );

            // ==============================================================
            // 5. FOOD CONCOURSE PATRONS & ALIEN DINERS
            // ==============================================================

            // Food Patron 1: Slurping Noodles at Chef Gorn's stall
            SpawnNPC(
                root.transform,
                "Tala Vance",
                NPCRoleType.MarketPatron,
                "PATRON // FOOD CRITIC",
                new Color(0.95f, 0.35f, 0.65f),
                new Vector3(-0.65f, -0.21f, 15.20f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_027_Kira_0x7F.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab",
                defaultIdleId: 27, // Eat / Drink
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "This sulfur noodle broth burns going down, but on Sector 4 it's the only meal that tastes like real food.",
                    "Chef Gorn has been serving these noodles since before the Corporation bombed the outer ring."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-0.65f, -0.21f, 15.20f),
                    new Vector3(2.50f, -0.21f, 14.00f),
                    new Vector3(4.00f, -0.21f, 10.00f),
                    new Vector3(1.00f, -0.21f, 12.00f)
                }
            );

            // Food Patron 2: Krag 'The Maw' (Giant hungry alien)
            SpawnNPC(
                root.transform,
                "Krag 'The Maw'",
                NPCRoleType.MarketPatron,
                "ALIEN // VOID VOYAGER",
                new Color(0.85f, 0.45f, 0.15f),
                new Vector3(-0.55f, -0.21f, 14.10f),
                Quaternion.Euler(0f, 75f, 0f),
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_BR_BigAlien_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 25, // Contemplative / Ponder
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Mmm... chef adds extra plasma pepper today. Good for my third stomach.",
                    "You look lean, cyborg. Need more protein for battle. Eat noodles."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-0.55f, -0.21f, 14.10f),
                    new Vector3(-4.00f, -0.21f, 10.00f),
                    new Vector3(-2.00f, -0.21f, 6.00f),
                    new Vector3(1.50f, -0.21f, 8.00f)
                }
            );

            // Food Patron 3: Maya Lin (Waiting in line for food)
            SpawnNPC(
                root.transform,
                "Maya Lin",
                NPCRoleType.MarketPatron,
                "CIVILIAN // COURIER",
                new Color(0.4f, 0.85f, 0.95f),
                new Vector3(-1.45f, -0.21f, 15.60f),
                Quaternion.Euler(0f, 85f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_024_Quinn_Holloway.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Female_01.prefab",
                defaultIdleId: 8, // Cross Arms Talking
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Chef Gorn has the longest queue in the Drift, but it's worth every credit. Don't skip the pickled grubs.",
                    "If you're flying out to the broken dome, grab emergency rations. Vacuum gets cold fast."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-1.45f, -0.21f, 15.60f),
                    new Vector3(0.50f, -0.21f, 18.00f),
                    new Vector3(3.00f, -0.21f, 15.00f),
                    new Vector3(0.00f, -0.21f, 12.00f)
                }
            );

            // Food Patron 4: Rondo Cole (Spectating over concourse railing)
            SpawnNPC(
                root.transform,
                "Rondo Cole",
                NPCRoleType.MarketPatron,
                "DRIFTER // SPECTATOR",
                new Color(0.6f, 0.7f, 0.9f),
                new Vector3(2.10f, -0.22f, 13.50f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_023_Trace_Byte.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_02.prefab",
                defaultIdleId: 14, // Railing Lean
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The view from this railing is the only peaceful thing on this rock. Watch the ships drift in and out of the nebula.",
                    "Keep your eyes on the central shaft. When the reactor fires, the light is blinding."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(2.10f, -0.22f, 13.50f),
                    new Vector3(5.00f, -0.22f, 9.00f),
                    new Vector3(2.00f, -0.22f, 5.00f),
                    new Vector3(-1.00f, -0.22f, 9.00f)
                }
            );

            // Food Patron 5: Scrap Bot Scratch-7 (Searching for dropped rations)
            SpawnNPC(
                root.transform,
                "Scratch-7",
                NPCRoleType.MarketPatron,
                "SCAV // SCRAPPER",
                new Color(0.9f, 0.8f, 0.2f),
                new Vector3(-1.60f, -0.21f, 13.50f),
                Quaternion.Euler(0f, 35f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_019_Raph_Cog.prefab",
                defaultIdleId: 4, // Look Around
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Beep-whirr! Looking for discarded copper wiring or dropped nutrient cubes. A bot's gotta keep its capacitors charged.",
                    "Watch your step around the steam vents, traveler. The pressure regulators leak thermal coolant."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-1.60f, -0.21f, 13.50f),
                    new Vector3(-5.00f, -0.21f, 16.00f),
                    new Vector3(-3.00f, -0.21f, 20.00f),
                    new Vector3(1.00f, -0.21f, 16.00f)
                }
            );

            // ==============================================================
            // 6. CYBER-BAR CANTINA LOUNGE PATRONS & GAMBLERS
            // ==============================================================

            // Bar Patron 6: Jett Chromium (Mercenary sipping synth-ale)
            SpawnNPC(
                root.transform,
                "Jett Chromium",
                NPCRoleType.MarketPatron,
                "MERC // HITMAN",
                new Color(0.95f, 0.25f, 0.25f),
                new Vector3(10.20f, 1.88f, 16.20f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_004_Jett_Chromium.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Male_01.prefab",
                defaultIdleId: 27, // Drink Hold
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Keep your blade close, girl. Half the patrons in this cantina have Omni-Corp bounties on their neuro-chips.",
                    "If you're looking for work, talk to the Fixer in the trophy den. He pays in real credits, not scrip."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(10.20f, 1.88f, 16.20f),
                    new Vector3(13.50f, 1.88f, 14.00f),
                    new Vector3(16.00f, 1.88f, 8.00f),
                    new Vector3(12.00f, 1.88f, 10.00f)
                }
            );

            // Bar Patron 7: Vesper Neon (Synthwave Musician)
            SpawnNPC(
                root.transform,
                "Vesper Neon",
                NPCRoleType.MarketPatron,
                "SYNTH // DJ",
                new Color(0.95f, 0.2f, 0.85f),
                new Vector3(12.60f, 1.88f, 14.50f),
                Quaternion.Euler(0f, -120f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_001_Sora_Voltage.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab",
                defaultIdleId: 26, // Rhythmic sway
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Feel the sub-bass vibrating through the deck plates? That's the rhythm of the Drift, baby!",
                    "When the station goes on red alert, the beat drops even harder."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(12.60f, 1.88f, 14.50f),
                    new Vector3(15.50f, 1.88f, 10.00f),
                    new Vector3(14.00f, 1.88f, 4.00f),
                    new Vector3(11.00f, 1.88f, 8.00f)
                }
            );

            // Bar Patron 8: Corvo Black (Syndicate Gambler A)
            SpawnNPC(
                root.transform,
                "Corvo Black",
                NPCRoleType.MarketPatron,
                "GAMBLER // SYNDICATE",
                new Color(0.3f, 0.8f, 0.4f),
                new Vector3(13.50f, 1.88f, 12.20f),
                Quaternion.Euler(0f, -135f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_028_Dante_Mercer.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 2, // Animated Talk
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "I put fifty thousand credits on the next cargo freighter getting intercepted. Easiest bet in Sector 4.",
                    "Zedd thinks Omni-Corp escorts can protect them. He's dreaming."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(13.50f, 1.88f, 12.20f),
                    new Vector3(16.50f, 1.88f, 15.00f),
                    new Vector3(14.00f, 1.88f, 20.00f),
                    new Vector3(11.00f, 1.88f, 16.00f)
                }
            );

            // Bar Patron 9: Zedd 'Snake-Eyes' (Bookie Gambler B)
            SpawnNPC(
                root.transform,
                "Zedd 'Snake-Eyes'",
                NPCRoleType.MarketPatron,
                "GAMBLER // BOOKIE",
                new Color(0.85f, 0.75f, 0.15f),
                new Vector3(14.30f, 1.88f, 11.50f),
                Quaternion.Euler(0f, 45f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_014_Zane_Zero.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                defaultIdleId: 8, // Listening / Nodding
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "You're crazy, Corvo. Omni-Corp's heavy gunships don't mess around in the asteroid belt.",
                    "Care to place a wager on how long your Damascus blade stays sharp, warrior?"
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(14.30f, 1.88f, 11.50f),
                    new Vector3(11.00f, 1.88f, 6.00f),
                    new Vector3(8.00f, 1.88f, 10.00f),
                    new Vector3(12.00f, 1.88f, 14.00f)
                }
            );

            // Bar Patron 10: Xylar the Spined (Alien Drifter)
            SpawnNPC(
                root.transform,
                "Xylar the Spined",
                NPCRoleType.MarketPatron,
                "ALIEN // DRIFTER",
                new Color(0.7f, 0.35f, 0.95f),
                new Vector3(9.60f, 1.88f, 13.80f),
                Quaternion.Euler(0f, 110f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienSpikes_01.prefab",
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_Alien_01.prefab",
                defaultIdleId: 14, // Relaxed lean
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Too much synthetic nitrogen ale... my spines are buzzing like an overheating plasma conduit...",
                    "The alien relic collector down the corridor was an old warlord in the Cydonia campaign. Show him respect."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(9.60f, 1.88f, 13.80f),
                    new Vector3(6.00f, 1.88f, 18.00f),
                    new Vector3(4.00f, 1.88f, 24.00f),
                    new Vector3(8.00f, 1.88f, 20.00f)
                }
            );

            // Bar Patron 11: Lyra Spark (Net Smuggler)
            SpawnNPC(
                root.transform,
                "Lyra Spark",
                NPCRoleType.MarketPatron,
                "NET // SMUGGLER",
                new Color(0.2f, 0.95f, 0.85f),
                new Vector3(11.80f, 1.88f, 16.80f),
                Quaternion.Euler(0f, -60f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_015_Nova_Static.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Female_01.prefab",
                defaultIdleId: 3, // Arms folded casual
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Nyx pours the only clean synth-water in this quadrant. Everything else has industrial heavy metals in it.",
                    "If you need encrypted nav-data, ask the Netrunner in the alley."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(11.80f, 1.88f, 16.80f),
                    new Vector3(15.00f, 1.88f, 22.00f),
                    new Vector3(12.00f, 1.88f, 26.00f),
                    new Vector3(9.00f, 1.88f, 20.00f)
                }
            );

            // ==============================================================
            // 7. WEAPONS BAZAAR & CYBERNETICS SHOPPERS
            // ==============================================================

            // Shopper 12: Karn 'Trigger-Happy' (Mercenary inspecting rifles)
            SpawnNPC(
                root.transform,
                "Karn 'Trigger-Happy'",
                NPCRoleType.WeaponsMerchant,
                "MERC // SHARPSHOOTER",
                new Color(0.95f, 0.35f, 0.2f),
                new Vector3(0.85f, -1.25f, 23.40f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_004_Jett_Chromium.prefab",
                defaultIdleId: 6, // Inspect weapon
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Vex has solid plasma hardware, but the magnetic coils need fine tuning. Always calibrate your sights yourself.",
                    "That Damascus sword on your back... now that's a real weapon. Never jams, never runs out of ammo."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(0.85f, -1.25f, 23.40f),
                    new Vector3(4.00f, -1.25f, 20.00f),
                    new Vector3(7.00f, -1.25f, 16.00f),
                    new Vector3(2.00f, -1.25f, 18.00f)
                }
            );

            // Shopper 13: Sasha Chromium (Testing armor plates)
            SpawnNPC(
                root.transform,
                "Sasha Chromium",
                NPCRoleType.MarketPatron,
                "VANGUARD // MERC",
                new Color(0.9f, 0.7f, 0.2f),
                new Vector3(8.40f, -1.58f, 28.00f),
                Quaternion.Euler(0f, 75f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_003_Silas_Titan.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienArmor_01.prefab",
                defaultIdleId: 5, // Datapad scan
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Torg's reactive kinetic plating can absorb a direct 30mm tungsten round. Expensive, but beats decompression in the void.",
                    "Make sure your armor seals are rated for vacuum before flying out the broken dome."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(8.40f, -1.58f, 28.00f),
                    new Vector3(5.00f, -1.58f, 24.00f),
                    new Vector3(2.00f, -1.25f, 26.00f),
                    new Vector3(6.00f, -1.58f, 29.00f)
                }
            );

            // Shopper 14: Valerie 'Voltage' (Ripperdoc Patient flexing cyber-arm)
            SpawnNPC(
                root.transform,
                "Valerie 'Voltage'",
                NPCRoleType.Ripperdoc,
                "CYBORG // PATIENT",
                new Color(0.1f, 0.85f, 0.95f),
                new Vector3(3.20f, -1.11f, 27.90f),
                Quaternion.Euler(0f, 80f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_008_Nadia_Vortex.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab",
                defaultIdleId: 18, // Arm flex / check cyberarm
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Dr. Frost just boosted my servo torque by 35%. My left cross could punch through a starship canopy now.",
                    "Your cybernetics look Martian military spec. URM technology is legendary on the Drift."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(3.20f, -1.11f, 27.90f),
                    new Vector3(-1.00f, -0.73f, 28.00f),
                    new Vector3(-5.00f, -0.73f, 25.00f),
                    new Vector3(0.00f, -0.73f, 26.00f)
                }
            );

            // Shopper 15: Bane 'Ironclad' (Alien Brute at weapon counter)
            SpawnNPC(
                root.transform,
                "Bane 'Ironclad'",
                NPCRoleType.MarketPatron,
                "BOUNTY // ENFORCER",
                new Color(0.85f, 0.25f, 0.25f),
                new Vector3(1.00f, -1.25f, 24.80f),
                Quaternion.Euler(0f, 120f, 0f),
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_BR_BigAlien_02.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                defaultIdleId: 7, // Hands on hips
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Stocking up on heavy explosive munitions. Corporate patrol drones have been sighted circling the outer ring.",
                    "Step aside, little cyborg. Big weapons for big warriors."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(1.00f, -1.25f, 24.80f),
                    new Vector3(-2.00f, -1.25f, 20.00f),
                    new Vector3(-5.00f, -1.25f, 15.00f),
                    new Vector3(-1.00f, -1.25f, 18.00f)
                }
            );

            // Shopper 16: Cyra Vance (Tech Apprentice organizing parts)
            SpawnNPC(
                root.transform,
                "Cyra Vance",
                NPCRoleType.ShipMechanic,
                "TECH // APPRENTICE",
                new Color(0.3f, 0.75f, 0.95f),
                new Vector3(4.90f, -1.11f, 28.80f),
                Quaternion.Euler(0f, -145f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_020_Kestrel_Spire.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                defaultIdleId: 1, // Standing datapad
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Calibrating neural interface chips is delicate work. One stray static shock and your memories get fried.",
                    "If you ever need your starship's flight computer flashed, come see me."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(4.90f, -1.11f, 28.80f),
                    new Vector3(7.00f, -1.58f, 24.00f),
                    new Vector3(9.00f, -1.58f, 18.00f),
                    new Vector3(6.00f, -1.58f, 22.00f)
                }
            );

            // ==============================================================
            // 8. PROMENADE, BALCONY & OVERLOOK SPECTATORS
            // ==============================================================

            // Spectator 17: Astra Vane (Overlook Lookout)
            SpawnNPC(
                root.transform,
                "Astra Vane",
                NPCRoleType.MarketPatron,
                "LOOKOUT // PILOT",
                new Color(0.6f, 0.85f, 1.0f),
                new Vector3(5.50f, 1.88f, 34.80f),
                Quaternion.Euler(0f, 0f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_016_Raven_Chen.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab",
                defaultIdleId: 14, // Railing lean
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Look out at the horizon. The wreckage of the old orbital ring stretches for thousands of kilometers.",
                    "They say hundreds of pilots died when Omni-Corp fired their orbital lance. Sector 4 was the only section that survived."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(5.50f, 1.88f, 34.80f),
                    new Vector3(1.00f, 1.88f, 32.00f),
                    new Vector3(-4.00f, 1.88f, 30.00f),
                    new Vector3(2.00f, 1.88f, 34.00f)
                }
            );

            // Spectator 18: Kip Thorne (North Plaza Sentry)
            SpawnNPC(
                root.transform,
                "Kip Thorne",
                NPCRoleType.StationEnforcer,
                "SECURITY // SENTRY",
                new Color(0.5f, 0.6f, 0.75f),
                new Vector3(-2.00f, 1.88f, 29.50f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_013_Gideon_Rezvani.prefab",
                defaultIdleId: 28, // Sentry stance
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Station security watches every walkway. Keep your blade in its sheath and we won't have problems.",
                    "Bounty hunters are thick in the lower bazaar today. Watch your back."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-2.00f, 1.88f, 29.50f),
                    new Vector3(2.00f, 1.88f, 26.00f),
                    new Vector3(6.00f, 1.88f, 24.00f),
                    new Vector3(0.00f, 1.88f, 28.00f)
                }
            );

            // Spectator 19: Zarek Rock-Skin (Alien Miner on gantry overlook)
            SpawnNPC(
                root.transform,
                "Zarek Rock-Skin",
                NPCRoleType.MarketPatron,
                "ALIEN // MINER",
                new Color(0.8f, 0.65f, 0.4f),
                new Vector3(8.20f, -1.38f, -22.50f),
                Quaternion.Euler(0f, 45f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienRock_01.prefab",
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_Alien_01.prefab",
                defaultIdleId: 3, // Arms folded
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Asteroid mining is honest work. Until the Corporation decides your claim belongs to them.",
                    "If you fly through the ring breach, watch for tumbling nickel-iron boulders. They will crush a hull like tinfoil."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(8.20f, -1.38f, -22.50f),
                    new Vector3(4.00f, -1.38f, -18.00f),
                    new Vector3(0.00f, -1.38f, -20.00f),
                    new Vector3(5.00f, -1.38f, -24.00f)
                }
            );

            // Spectator 20: Fenric Ward (Promenade Broker near Madame Zara)
            SpawnNPC(
                root.transform,
                "Fenric Ward",
                NPCRoleType.ContrabandDealer,
                "BROKER // TRADER",
                new Color(0.7f, 0.45f, 0.95f),
                new Vector3(1.20f, -1.40f, 19.20f),
                Quaternion.Euler(0f, 65f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_007_Reeve_Blackwood.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Male_01.prefab",
                defaultIdleId: 8, // Talking
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Madame Zara runs the tightest bazaar in the sector. You want rare Martian contraband, you play by her rules.",
                    "Her informants know every cargo ship that docked in the last forty-eight hours."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(1.20f, -1.40f, 19.20f),
                    new Vector3(-3.00f, -1.40f, 16.00f),
                    new Vector3(-6.00f, -1.40f, 12.00f),
                    new Vector3(-1.00f, -1.40f, 15.00f)
                }
            );

            // Spectator 21: Talon Drake (Moored Tug Docker)
            SpawnNPC(
                root.transform,
                "Talon Drake",
                NPCRoleType.ShipMechanic,
                "CARGO // DOCKER",
                new Color(0.95f, 0.6f, 0.1f),
                new Vector3(-12.80f, 2.10f, -3.80f),
                Quaternion.Euler(0f, 45f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_005_Rook_Glitch.prefab",
                defaultIdleId: 7, // Hands on hips
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Mooring clamps are holding. Just waiting for clearance from the control tower to unload the heavy fuel canisters.",
                    "That Nightshade Striker on Pad 02 is a beautiful piece of engineering. Fast, lethal, and fitted with an electromagnetic tractor beam."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-12.80f, 2.10f, -3.80f),
                    new Vector3(-16.00f, 2.10f, 0.00f),
                    new Vector3(-18.00f, 2.10f, -5.00f),
                    new Vector3(-14.00f, 2.10f, -8.00f)
                }
            );

            // Spectator 22: Sarix 'Whisper' (Informant outside The Collector's Pod)
            SpawnNPC(
                root.transform,
                "Sarix 'Whisper'",
                NPCRoleType.ContrabandDealer,
                "INFORMANT // SHADOW",
                new Color(0.2f, 0.85f, 0.95f),
                new Vector3(-3.20f, -0.11f, -13.80f),
                Quaternion.Euler(0f, 45f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_026_Cipher_Specter.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienSpikes_01.prefab",
                defaultIdleId: 14, // Lean in shadows
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Psst... you're heading into the skull pod? That alien warlord inside has seen a hundred battles.",
                    "He collected relics from Mars before you were ever built. Treat him with respect, warrior."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(2.80f, 0.28f, -10.25f),
                    new Vector3(6.00f, 0.28f, -14.00f),
                    new Vector3(8.00f, 0.28f, -10.00f),
                    new Vector3(4.00f, 0.28f, -7.00f)
                }
            );

            // ==============================================================
            // 9. BACK ALLEY CONTRABAND SHADOW DWELLERS
            // ==============================================================

            // Shadow 23: Vexen Nine (Data runner in alley)
            SpawnNPC(
                root.transform,
                "Vexen Nine",
                NPCRoleType.ContrabandDealer,
                "SHADOW // DATA RUNNER",
                new Color(0.1f, 0.95f, 0.8f),
                new Vector3(-6.50f, -1.25f, 4.50f),
                Quaternion.Euler(0f, 35f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_008_Nadia_Vortex.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_02.prefab",
                defaultIdleId: 5, // Datapad check
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Keep moving, blade. These narrow alleys aren't safe for anyone without syndicate clearance.",
                    "I trade in encrypted hyperspace jump manifests. You want to disappear from Omni-Corp sensors, come find me."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-6.50f, -1.25f, 4.50f),
                    new Vector3(-10.00f, -1.25f, 2.00f),
                    new Vector3(-12.00f, -1.25f, 6.00f),
                    new Vector3(-8.00f, -1.25f, 8.00f)
                }
            );

            // Shadow 24: Rook-9 (Scavenger sitting on crates)
            SpawnNPC(
                root.transform,
                "Rook-9",
                NPCRoleType.MarketPatron,
                "SCAVENGER // SALVAGE",
                new Color(0.7f, 0.75f, 0.8f),
                new Vector3(-10.20f, -1.25f, 7.80f),
                Quaternion.Euler(0f, 130f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_019_Raph_Cog.prefab",
                defaultIdleId: 12, // Arms folded grumpy
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Found three pristine hyperdrive solenoids in the scrap heap today. Silas will give me a hundred credits each.",
                    "The station's trash is an outlaw's treasure."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-10.20f, -1.25f, 7.80f),
                    new Vector3(-14.00f, -1.25f, 4.00f),
                    new Vector3(-11.00f, -1.25f, 0.00f),
                    new Vector3(-7.00f, -1.25f, 3.00f)
                }
            );

            // Shadow 25: Krell the Grunt (Syndicate Thug)
            SpawnNPC(
                root.transform,
                "Krell the Grunt",
                NPCRoleType.StationEnforcer,
                "SYNDICATE // THUG",
                new Color(0.85f, 0.3f, 0.2f),
                new Vector3(-8.20f, -0.73f, 24.20f),
                Quaternion.Euler(0f, -45f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_AlienCombat_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_006_Orion_Ward.prefab",
                defaultIdleId: 28, // Aggressive posture
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "You're in Cipher's net territory now, cyborg. Watch your step.",
                    "One wrong move and our pulse disruptors will fry your servos."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-8.20f, -0.73f, 24.20f),
                    new Vector3(-12.00f, -0.73f, 20.00f),
                    new Vector3(-9.00f, -0.73f, 16.00f),
                    new Vector3(-5.00f, -0.73f, 20.00f)
                }
            );

            // Shadow 26: Dax Malone (Hardware Logistics)
            SpawnNPC(
                root.transform,
                "Dax Malone",
                NPCRoleType.ShipMechanic,
                "DEPOT // LOGISTICS",
                new Color(0.9f, 0.6f, 0.15f),
                new Vector3(-14.20f, -1.25f, 3.80f),
                Quaternion.Euler(0f, -60f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_003_Silas_Titan.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_02.prefab",
                defaultIdleId: 6, // Inspecting
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Silas has the best atmospheric scrubbers in the quadrant. Don't leave Sector 4 without a spare oxygen seal.",
                    "Vacuum doesn't care who your friends are."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-14.20f, -1.25f, 3.80f),
                    new Vector3(-18.00f, 0.05f, 0.00f),
                    new Vector3(-14.00f, 0.05f, 4.00f),
                    new Vector3(-11.00f, -1.25f, 2.00f)
                }
            );

            // Shadow 27: Zola Frost (Netrunner slicing conduits)
            SpawnNPC(
                root.transform,
                "Zola Frost",
                NPCRoleType.ContrabandDealer,
                "NETRUNNER // SLICE",
                new Color(0.1f, 0.9f, 1.0f),
                new Vector3(-5.20f, -1.25f, 12.80f),
                Quaternion.Euler(0f, 110f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_011_Jax_Pulse.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Alien_Female_01.prefab",
                defaultIdleId: 5, // Datapad hacking
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Tapping into the station's main sensor array. Omni-Corp transponders are lighting up the orbital sector.",
                    "If they breach the dome, our point-defense turrets are ready."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-5.20f, -1.25f, 12.80f),
                    new Vector3(-2.00f, -1.25f, 8.00f),
                    new Vector3(2.00f, -1.25f, 10.00f),
                    new Vector3(-1.00f, -1.25f, 14.00f)
                }
            );

            // ==============================================================
            // 10. DYNAMIC PLAZA ROAMERS & SPRINTING COURIERS (WANDERERS)
            // ==============================================================

            // Wanderer 9: Courier Dex (Sprinting across main concourse)
            SpawnNPC(
                root.transform,
                "Courier Dex",
                NPCRoleType.MarketPatron,
                "COURIER // SPEEDRUNNER",
                new Color(0.95f, 0.85f, 0.15f),
                new Vector3(14.0f, 1.88f, 20.0f),
                Quaternion.Euler(0f, -90f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_015_Nova_Static.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Male_01.prefab",
                defaultIdleId: 1,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Out of the way! Urgent datachip delivery for Baron Klyne's gambling suite!",
                    "On Sector 4, seconds mean millions of credits!"
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(14.0f, 1.88f, 20.0f),
                    new Vector3(10.0f, 1.88f, 24.0f),
                    new Vector3(4.0f, 1.88f, 28.0f),
                    new Vector3(8.0f, 1.88f, 22.0f)
                }
            );

            // Wanderer 10: Alien Tourist Gax (Admiring neon signs)
            SpawnNPC(
                root.transform,
                "Gax the Curious",
                NPCRoleType.MarketPatron,
                "ALIEN // TOURIST",
                new Color(0.2f, 0.95f, 0.65f),
                new Vector3(6.0f, -1.28f, 8.0f),
                Quaternion.Euler(0f, 0f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Alien_Male_01.prefab",
                "Assets/PolygonSciFiSpace/Prefabs/Characters/SM_Chr_Alien_01.prefab",
                defaultIdleId: 4, // Look around
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Such fascinating human architecture! The smell of burning hydrocarbons is most exotic!",
                    "My clan on Kepler-452 would adore these glowing ramen signs."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(6.0f, -1.28f, 8.0f),
                    new Vector3(2.0f, -1.28f, 12.0f),
                    new Vector3(0.0f, -0.21f, 14.0f),
                    new Vector3(4.0f, -1.28f, 4.0f)
                }
            );

            // Wanderer 11: Syndicate Heavy Riker (Assertive sentry patrol)
            SpawnNPC(
                root.transform,
                "Enforcer Riker",
                NPCRoleType.StationEnforcer,
                "SECURITY // PATROL",
                new Color(0.4f, 0.55f, 0.85f),
                new Vector3(12.0f, 1.88f, 8.0f),
                Quaternion.Euler(0f, 45f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_013_Gideon_Rezvani.prefab",
                defaultIdleId: 28, // Sentry
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Routine patrol. Keep the peace and nobody gets hit with an electromagnetic stun rod.",
                    "Commander Thorne wants the central promenade completely locked down."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(12.0f, 1.88f, 8.0f),
                    new Vector3(8.0f, 1.88f, 14.0f),
                    new Vector3(6.0f, 1.88f, 20.0f),
                    new Vector3(10.0f, 1.88f, 12.0f)
                }
            );

            // Wanderer 12: Scavenger Trix (Hauling parts along western gantry)
            SpawnNPC(
                root.transform,
                "Scavenger Trix",
                NPCRoleType.MarketPatron,
                "SCAV // PACK RAT",
                new Color(0.85f, 0.65f, 0.2f),
                new Vector3(-8.0f, 0.05f, 14.0f),
                Quaternion.Euler(0f, 90f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Scavenger_03.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_025_Riku_Synapse.prefab",
                defaultIdleId: 1,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Found a working gyro-stabilizer in cargo pod seventeen! Today is a great day on the Drift!",
                    "You want scrap metal? Check the lower gantry bins. Just watch for arc leaks."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(-8.0f, 0.05f, 14.0f),
                    new Vector3(-12.0f, 0.05f, 20.0f),
                    new Vector3(-15.0f, 0.05f, 14.0f),
                    new Vector3(-10.0f, 0.05f, 8.0f)
                }
            );

            // Wanderer 13: Android Inspector Unit-4 (Automated pylon inspection)
            SpawnNPC(
                root.transform,
                "Inspector Unit-4",
                NPCRoleType.ShipMechanic,
                "ANDROID // INSPECTOR",
                new Color(0.1f, 0.85f, 0.95f),
                new Vector3(2.0f, -1.29f, 2.0f),
                Quaternion.Euler(0f, 180f, 0f),
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_004_Jett_Chromium.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_02.prefab",
                defaultIdleId: 5, // Diagnostic
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "Atmospheric pressure: 98.4 kPa. Micro-meteorite breach risk: nominal. Have a compliant day, citizen.",
                    "Thermal conduits in sector B are operating at 104% capacity."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(2.0f, -1.29f, 2.0f),
                    new Vector3(-2.0f, -1.29f, 0.0f),
                    new Vector3(0.0f, -1.29f, -4.0f),
                    new Vector3(4.0f, -1.29f, -1.0f)
                }
            );

            // Wanderer 14: Void-Walker Kael (Strolling along scenic north gantry)
            SpawnNPC(
                root.transform,
                "Void-Walker Kael",
                NPCRoleType.MarketPatron,
                "DRIFTER // WANDERER",
                new Color(0.7f, 0.8f, 0.95f),
                new Vector3(0.0f, 1.88f, 32.0f),
                Quaternion.Euler(0f, -45f, 0f),
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Strider_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs/CyberNPC_016_Raven_Chen.prefab",
                defaultIdleId: 14,
                isStoryObjective: false,
                unlockObjectiveText: "",
                dialogue: new string[]
                {
                    "The cosmic nebula is breathtaking from this bridge. Makes you forget how dangerous this station really is.",
                    "Take time to breathe, warrior. The galaxy won't wait, but the stars aren't going anywhere."
                },
                patrolWaypoints: new Vector3[]
                {
                    new Vector3(0.0f, 1.88f, 32.0f),
                    new Vector3(-4.0f, 1.88f, 30.0f),
                    new Vector3(-2.0f, 1.88f, 34.0f),
                    new Vector3(3.0f, 1.88f, 31.0f)
                }
            );

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#10B981>[Black Market]</color> Successfully populated 60 fully animated NPCs across all districts!");
        }

        private static GameObject SpawnNPC(
            Transform parent,
            string name,
            NPCRoleType role,
            string badgeTitle,
            Color badgeColor,
            Vector3 position,
            Quaternion rotation,
            string primaryPrefabPath,
            string fallbackPrefabPath,
            int defaultIdleId,
            bool isStoryObjective,
            string unlockObjectiveText,
            string[] dialogue,
            Vector3[] patrolWaypoints = null,
            bool canTalk = false,
            float interactionRange = 4.5f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(primaryPrefabPath);
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPrefabPath);
            }

            GameObject instance = null;
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = "NPC_" + name.Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("'", "");
            }
            else
            {
                instance = new GameObject("NPC_" + name.Replace(" ", "_"));
                instance.transform.SetParent(parent);
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.transform.SetParent(instance.transform, false);
                cap.transform.localPosition = new Vector3(0f, 1f, 0f);
            }

            // Raycast ground to ensure 100% precision placement without floating or clipping
            RaycastHit groundHit;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out groundHit, 6.0f))
            {
                position.y = groundHit.point.y;
            }

            instance.transform.position = position;
            instance.transform.rotation = rotation;

            // Ensure Animator with AC_CyberNPC controller
            var anim = instance.GetComponentInChildren<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            if (anim.runtimeAnimatorController == null)
            {
                var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
                if (ac != null) anim.runtimeAnimatorController = ac;
            }

            if (anim.avatar == null)
            {
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/CharactersAvatar.asset");
                if (avatar != null) anim.avatar = avatar;
            }

            // Configure locomotion / colliders based on whether NPC is a wandering pedestrian or stationary shopkeeper
            if (patrolWaypoints != null && patrolWaypoints.Length > 1)
            {
                // Remove static CapsuleCollider if present so CharacterController handles movement cleanly
                var oldCol = instance.GetComponent<CapsuleCollider>();
                if (oldCol != null) Object.DestroyImmediate(oldCol);

                // Add CharacterController for smooth step navigation and gravity
                var cc = instance.GetComponent<CharacterController>();
                if (cc == null) cc = instance.AddComponent<CharacterController>();
                cc.center = new Vector3(0f, 0.95f, 0f);
                cc.radius = 0.35f;
                cc.height = 1.85f;
                cc.stepOffset = 0.35f;
                cc.slopeLimit = 45f;
                cc.minMoveDistance = 0.001f;

                var wanderer = instance.GetComponent<BlackMarketNPCWanderer>();
                if (wanderer == null) wanderer = instance.AddComponent<BlackMarketNPCWanderer>();
                wanderer.animator = anim;
                wanderer.waypoints = new List<Vector3>(patrolWaypoints);
                wanderer.walkSpeed = 1.45f;
                wanderer.turnSpeed = 5.5f;
                wanderer.waypointRadius = 1.0f;
                wanderer.loopPatrol = true;
                wanderer.waitTimeMin = 0.4f;
                wanderer.waitTimeMax = 1.2f;
            }
            else
            {
                // Stationary shopkeeper / guard: ensure CapsuleCollider
                var col = instance.GetComponent<CapsuleCollider>();
                if (col == null)
                {
                    col = instance.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 0.95f, 0f);
                    col.radius = 0.4f;
                    col.height = 1.9f;
                }
            }

            // Attach & Configure BlackMarketNPC
            var npcComp = instance.GetComponent<BlackMarketNPC>();
            if (npcComp == null)
            {
                npcComp = instance.AddComponent<BlackMarketNPC>();
            }

            npcComp.characterName = name;
            npcComp.roleType = role;
            npcComp.roleBadgeTitle = badgeTitle;
            npcComp.badgeColor = badgeColor;
            npcComp.canTalk = canTalk;
            npcComp.isStoryObjectiveNPC = isStoryObjective;
            npcComp.unlockObjectiveText = unlockObjectiveText;
            npcComp.interactionRange = interactionRange;
            npcComp.animator = anim;
            npcComp.defaultIdleID = defaultIdleId;
            npcComp.ApplyIdlePosture(defaultIdleId);

            npcComp.dialogueEntries = new List<DialogueEntry>();
            foreach (var line in dialogue)
            {
                npcComp.dialogueEntries.Add(new DialogueEntry { dialogueText = line });
            }

            return instance;
        }

        private static void ConfigureCollectorPodDoors()
        {
            Vector3 frontDoorPos = new Vector3(-0.13f, 0.50f, -10.25f);
            Vector3 backDoorPos = new Vector3(10.48f, 0.27f, -10.31f);

            var allDoors = Object.FindObjectsByType<CyberpunkAutomaticDoor>(FindObjectsSortMode.None);
            foreach (var d in allDoors)
            {
                if (d == null) continue;
                float distFront = Vector3.Distance(d.transform.position, frontDoorPos);
                float distBack = Vector3.Distance(d.transform.position, backDoorPos);

                if (distFront < 1.5f || distBack < 1.5f)
                {
                    d.openDistance = 7.5f;   // Opens smoothly when Alita approaches the stairs or alien bodyguards (1.8m - 7.5m)
                    d.closeDistance = 10.5f; // Closes once she moves away
                    d.openSpeed = 6.0f;      // Fast, responsive pneumatic sliding
                    d.disableRootFrameColliderAlways = true;
                    var rc = d.GetComponent<Collider>();
                    if (rc != null) rc.enabled = false;

                    EditorUtility.SetDirty(d);
                    Debug.Log($"[BlackMarketNPCPopulator] Configured Collector Pod Door '{d.gameObject.name}' with openDist=7.5m, closeDist=10.5m, openSpeed=6.0");
                }
            }
        }

        private static void AttachWeaponToHand(GameObject npcInstance, string weaponPrefabPath)
        {
            if (npcInstance == null) return;
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath);
            if (weaponPrefab == null) return;

            Animator anim = npcInstance.GetComponentInChildren<Animator>();
            Transform handR = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (handR == null)
            {
                foreach (var t in npcInstance.GetComponentsInChildren<Transform>())
                {
                    if (t.name.Contains("Hand_R") || t.name.Contains("RightHand"))
                    {
                        handR = t;
                        break;
                    }
                }
            }

            if (handR != null)
            {
                GameObject wep = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, handR);
                wep.transform.localPosition = new Vector3(0.04f, 0.08f, -0.02f);
                wep.transform.localRotation = Quaternion.Euler(-10f, 90f, 90f);
                wep.transform.localScale = Vector3.one * 1.0f;
            }
        }
    }
}
#endif
