#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Editor
{
    public static class NPCAnimationProfileBuilder
    {
        private const string ProfileDir = "Assets/PolygonCyberCity/Data/AnimProfiles";

        [MenuItem("CyberCity/NPC Animation/Create Default Animation Profiles", false, 11)]
        [MenuItem("Tools/Cyber City/NPC Animation/Create Default Animation Profiles", false, 11)]
        public static void CreateDefaultProfiles()
        {
            if (!Directory.Exists(ProfileDir))
            {
                Directory.CreateDirectory(ProfileDir);
                AssetDatabase.Refresh();
            }

            CreateProfile("CivilianAnimProfile", NPCType.Civilian, false, false, false, false);
            CreateProfile("RichCitizenAnimProfile", NPCType.RichCitizen, false, false, false, false);
            CreateProfile("HelperBotAnimProfile", NPCType.HelperBot, false, false, false, false);
            CreateProfile("MercenaryAnimProfile", NPCType.Mercenary, true, true, true, true);
            CreateProfile("SecurityAnimProfile", NPCType.StreetThug, true, true, false, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NPCAnimationProfileBuilder] Default animation profiles created in '{ProfileDir}'");
        }

        private static void CreateProfile(string name, NPCType archetype, bool canCombat, bool block, bool parry, bool dodge)
        {
            string path = $"{ProfileDir}/{name}.asset";
            NPCAnimationProfile profile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NPCAnimationProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.targetArchetype = archetype;
            profile.hasCombatLocomotion = canCombat;
            profile.canBlock = block;
            profile.canParry = parry;
            profile.canDodge = dodge;
            profile.blockChance = block ? 0.35f : 0f;
            profile.parryChance = parry ? 0.20f : 0f;
            profile.dodgeChance = dodge ? 0.25f : 0f;
            profile.walkSpeed = archetype == NPCType.HelperBot ? 1.2f : 1.35f;
            profile.runSpeed = archetype == NPCType.HelperBot ? 3.2f : 4.5f;
            profile.sprintSpeed = 5.8f;
            profile.idleFidgetIntervalMin = 4.0f;
            profile.idleFidgetIntervalMax = 8.5f;
            profile.idleVariantCount = 30;
            profile.useKnockdownSequenceOnHeavyHit = true;
            profile.enableHitPause = true;

            string[] guids = AssetDatabase.FindAssets("t:SyntyAnimClipData");
            if (guids.Length > 0)
            {
                string clipDataPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var clipData = AssetDatabase.LoadAssetAtPath<SyntyCombined.SyntyAnimClipData>(clipDataPath);
                profile.combatClipData = clipData;

                if (clipData != null)
                {
                    if (clipData.deathForward != null) profile.deathClipsFront = new[] { clipData.deathForward };
                    if (clipData.deathBack != null) profile.deathClipsBack = new[] { clipData.deathBack };
                    if (clipData.deathLeft != null) profile.deathClipsLeft = new[] { clipData.deathLeft };
                    if (clipData.deathRight != null) profile.deathClipsRight = new[] { clipData.deathRight };
                }
            }

            if (!canCombat)
            {
                profile.allowedActivities = new[]
                {
                    NPCActivityType.Phone,
                    NPCActivityType.Eat,
                    NPCActivityType.Drink,
                    NPCActivityType.Talk,
                    NPCActivityType.Sit,
                    NPCActivityType.Lean,
                    NPCActivityType.Inspect
                };
            }

            EditorUtility.SetDirty(profile);
        }
    }
}
#endif
