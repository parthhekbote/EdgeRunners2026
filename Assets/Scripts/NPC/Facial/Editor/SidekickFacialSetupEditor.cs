using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Synty.Sidekick.Facial;
using PolygonCyberCity.NPC.Spawner;

namespace Synty.Sidekick.Facial.Editor
{
    public static class SidekickFacialSetupEditor
    {
        [MenuItem("Tools/Sidekick Characters/Setup Talking Sidekick NPCs in Scene", priority = 1)]
        [MenuItem("CyberCity/NPC/Setup Talking Sidekick NPCs in Scene", priority = 1)]
        public static void SetupTalkingSidekickNPCs()
        {
            int configuredCount = 0;

            // 1. Configure "Combined Character" if present in scene
            var combinedChar = GameObject.Find("Combined Character");
            if (combinedChar != null)
            {
                var animator = combinedChar.GetComponent<SyntySidekickFacialAnimator>();
                if (animator == null) animator = combinedChar.AddComponent<SyntySidekickFacialAnimator>();
                animator.characterDisplayName = "ALITA";
                animator.characterRoleSubtitle = "CYBORG HERO // DAMASCUS WIELDER";
                animator.richDialogueLines = new SyntySidekickFacialAnimator.DialogueEntry[]
                {
                    new SyntySidekickFacialAnimator.DialogueEntry("Systems nominal. Damascus blade calibrated and ready for combat.", SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk),
                    new SyntySidekickFacialAnimator.DialogueEntry("Doctor Ido rebuilt my core chassis with high-frequency servo dampeners.", SyntySidekickFacialAnimator.FacialMood.Friendly),
                    new SyntySidekickFacialAnimator.DialogueEntry("I can sense rogue Blackwall frequencies pulsing beneath the street asphalt.", SyntySidekickFacialAnimator.FacialMood.Serious),
                    new SyntySidekickFacialAnimator.DialogueEntry("Keep your guard up. The rival syndicate has enforcers lurking around the plaza.", SyntySidekickFacialAnimator.FacialMood.Suspicious)
                };
                animator.currentMood = SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk;
                animator.CacheBlendShapeIndices();
                EditorUtility.SetDirty(combinedChar);
                configuredCount++;
                Debug.Log("<color=cyan>[SidekickFacialSetup] Configured SyntySidekickFacialAnimator on 'Combined Character'!</color>");
            }

            // 2. Scan all SkinnedMeshRenderers with blendshapes in scene
            var allSmrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            foreach (var smr in allSmrs)
            {
                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount >= 30)
                {
                    var root = smr.transform.root.gameObject;
                    if (root.GetComponent<SyntySidekickFacialAnimator>() == null)
                    {
                        var fa = root.AddComponent<SyntySidekickFacialAnimator>();
                        fa.targetSkinnedMesh = smr;
                        fa.characterDisplayName = root.name.Replace("(Clone)", "").Replace("_Pooled", "").Replace("Sidekick_", "").ToUpper();
                        fa.CacheBlendShapeIndices();
                        EditorUtility.SetDirty(root);
                        configuredCount++;
                        Debug.Log($"<color=cyan>[SidekickFacialSetup] Configured SyntySidekickFacialAnimator on '{root.name}' ({smr.sharedMesh.blendShapeCount} blendshapes)!</color>");
                    }
                }
            }

            // 3. Place interactive Sidekick Street NPCs near the player spawn / walkway
            EnsureSidekickStreetCitizensPlaced();

            Debug.Log($"<color=green>[SidekickFacialSetup] Complete! Configured characters with AAA Lip Sync, Blinking, Look-At & Holographic Dialogue!</color>");
        }

        public static void EnsureSidekickStreetCitizensPlaced()
        {
            string[] sidekickPrefabPaths = new string[]
            {
                "Assets/Characters/Doctor.prefab",
                "Assets/Synty/SidekickCharacters/Characters/ScifiCivilians/ScifiCivilians_01/ScifiCivilians_01.prefab",
                "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/HumanSpecies_01.prefab"
            };

            // Relative offsets from player in local forward/right space
            // Offset 0: 3.5m forward-right on the walkway
            // Offset 1: 5.5m forward-left near the bridge railing
            // Offset 2: 7.2m forward on the plaza platform
            Vector3[] localOffsets = new Vector3[]
            {
                new Vector3(2.6f, 0f, 3.8f),
                new Vector3(-2.8f, 0f, 5.2f),
                new Vector3(0.5f, 0f, 7.5f)
            };

            string[] names = new string[]
            {
                "Sidekick_Doctor_Ido",
                "Sidekick_Netrunner_Mira",
                "Sidekick_Merc_Kaelen"
            };

            string[] displayNames = new string[]
            {
                "DR. IDO",
                "MIRA",
                "KAELEN"
            };

            string[] roles = new string[]
            {
                "CYBER SURGEON // CLINIC",
                "NETRUNNER // FIXER",
                "STREET MERC // VETERAN"
            };

            SyntySidekickFacialAnimator.DialogueEntry[][] dialogueSets = new SyntySidekickFacialAnimator.DialogueEntry[][]
            {
                new SyntySidekickFacialAnimator.DialogueEntry[]
                {
                    new SyntySidekickFacialAnimator.DialogueEntry("Stay sharp out there, choom. Boostergangers in Sector 4 are packing military disruptors.", SyntySidekickFacialAnimator.FacialMood.Serious),
                    new SyntySidekickFacialAnimator.DialogueEntry("I inspected your Damascus katana earlier. Blade alignment is pristine. Treat it well.", SyntySidekickFacialAnimator.FacialMood.Friendly),
                    new SyntySidekickFacialAnimator.DialogueEntry("If your core cyber-servos surge past redline, drop by the clinic. Cleanest chrome in town.", SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk),
                    new SyntySidekickFacialAnimator.DialogueEntry("The Blackwall telemetry is fluctuating wildly today. Keep your ICE filters calibrated.", SyntySidekickFacialAnimator.FacialMood.Serious)
                },
                new SyntySidekickFacialAnimator.DialogueEntry[]
                {
                    new SyntySidekickFacialAnimator.DialogueEntry("Hey, solo! Got fresh telemetry on channel 07: Arasaka sweep teams just deployed drones nearby.", SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk),
                    new SyntySidekickFacialAnimator.DialogueEntry("Saw that katana combo you pulled off in the alleyway earlier. Incredible speed, choom.", SyntySidekickFacialAnimator.FacialMood.Friendly),
                    new SyntySidekickFacialAnimator.DialogueEntry("Need military-grade daemon exploits? Catch me behind the ramen stall after midnight.", SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk),
                    new SyntySidekickFacialAnimator.DialogueEntry("The corporate neon never sleeps, but neither do the hit squads. Keep that Damascus blade drawn.", SyntySidekickFacialAnimator.FacialMood.Suspicious)
                },
                new SyntySidekickFacialAnimator.DialogueEntry[]
                {
                    new SyntySidekickFacialAnimator.DialogueEntry("Word of advice: never trust any fixer who won't meet you in an unjammed district.", SyntySidekickFacialAnimator.FacialMood.Serious),
                    new SyntySidekickFacialAnimator.DialogueEntry("That cyber-katana you're carrying... Damascus forged? Rare steel in this city.", SyntySidekickFacialAnimator.FacialMood.Friendly),
                    new SyntySidekickFacialAnimator.DialogueEntry("NCPD tactical drones are running heavy scans. Keep your combat cyberware sheathed in public.", SyntySidekickFacialAnimator.FacialMood.Concerned),
                    new SyntySidekickFacialAnimator.DialogueEntry("Night in Night City... the air smells like ozone, rain, and synthetic adrenaline.", SyntySidekickFacialAnimator.FacialMood.CyberpunkSmirk)
                }
            };

            var player = GameObject.FindWithTag("Player");
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            Vector3 playerFwd = player != null ? player.transform.forward : Vector3.forward;
            Vector3 playerRight = player != null ? player.transform.right : Vector3.right;

            for (int i = 0; i < sidekickPrefabPaths.Length; i++)
            {
                string objName = names[i];
                var existing = GameObject.Find(objName);
                if (existing != null)
                {
                    // Update dialogue and configuration on existing
                    var existingFa = existing.GetComponent<SyntySidekickFacialAnimator>();
                    if (existingFa != null)
                    {
                        existingFa.characterDisplayName = displayNames[i];
                        existingFa.characterRoleSubtitle = roles[i];
                        existingFa.richDialogueLines = dialogueSets[i];
                        existingFa.CacheBlendShapeIndices();
                        EditorUtility.SetDirty(existing);
                    }
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sidekickPrefabPaths[i]);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SidekickFacialSetup] Prefab not found at path: {sidekickPrefabPaths[i]}");
                    continue;
                }

                GameObject citizen = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                citizen.name = objName;

                // Calculate spawn position in player's forward/right space
                Vector3 targetSpawn = playerPos + (playerRight * localOffsets[i].x) + (playerFwd * localOffsets[i].z);

                // Raycast down to find ground level
                if (Physics.Raycast(targetSpawn + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
                {
                    targetSpawn.y = hit.point.y;
                }
                else
                {
                    targetSpawn.y = -17.99f; // Default walkway level
                }

                citizen.transform.position = targetSpawn;

                // Turn to face towards player
                if (player != null)
                {
                    Vector3 lookDir = (player.transform.position - citizen.transform.position);
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        citizen.transform.rotation = Quaternion.LookRotation(lookDir.normalized);
                    }
                }

                // Add or configure facial animator
                var fa = citizen.GetComponent<SyntySidekickFacialAnimator>();
                if (fa == null) fa = citizen.AddComponent<SyntySidekickFacialAnimator>();
                fa.characterDisplayName = displayNames[i];
                fa.characterRoleSubtitle = roles[i];
                fa.richDialogueLines = dialogueSets[i];
                fa.enableProximityInteraction = true;
                fa.interactionRadius = 3.6f;
                fa.enableBlinking = true;
                fa.enableHeadMicroGestures = true;
                fa.enableLookAtPlayer = true;
                fa.lookAtMaxDistance = 5.2f;
                fa.currentMood = dialogueSets[i][0].mood;
                fa.CacheBlendShapeIndices();

                // Assign idle animation controller
                var anim = citizen.GetComponent<Animator>();
                if (anim != null)
                {
                    var idleAc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Synty/SidekickCharacters/_Demos/Animations/AC_FacialAnim.controller");
                    if (idleAc == null)
                    {
                        idleAc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
                    }
                    if (idleAc != null)
                    {
                        anim.runtimeAnimatorController = idleAc;
                    }
                }

                EditorUtility.SetDirty(citizen);
                Debug.Log($"<color=cyan>[SidekickFacialSetup] Placed interactive talking NPC '{objName}' at {citizen.transform.position}!</color>");
            }
        }
    }
}
