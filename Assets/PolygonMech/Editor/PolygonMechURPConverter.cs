using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Synty.PolygonMech.Editor
{
    public static class PolygonMechURPConverter
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += CheckAndConvert;
        }

        private static void CheckAndConvert()
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/PolygonMech/Materials" });
            bool needsUpgrade = false;

            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null)
                {
                    string sName = mat.shader.name;
                    if (sName == "Standard" || sName.Contains("Legacy Shaders/Particles") || sName.StartsWith("Particles/"))
                    {
                        needsUpgrade = true;
                        break;
                    }
                }
            }

            if (needsUpgrade)
            {
                Debug.Log("[PolygonMechURPConverter] Detected Built-in shaders in PolygonMech. Upgrading to URP...");
                ConvertAllMaterials();
            }
        }

        [MenuItem("Synty/Convert PolygonMech to URP")]
        public static void ConvertAllMaterials()
        {
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            var urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (urpLitShader == null)
            {
                Debug.LogError("[PolygonMechURPConverter] Universal Render Pipeline/Lit shader not found!");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/PolygonMech/Materials" });
            int convertedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in matGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;

                    string sName = mat.shader != null ? mat.shader.name : "";
                    bool isFX = path.Contains("/FX/") || sName.Contains("Particle");

                    if (isFX)
                    {
                        if (urpParticleShader != null)
                        {
                            ConvertParticleMaterial(mat, urpParticleShader);
                            convertedCount++;
                        }
                    }
                    else
                    {
                        ConvertLitMaterial(mat, urpLitShader);
                        convertedCount++;
                    }

                    EditorUtility.SetDirty(mat);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PolygonMechURPConverter] Successfully converted {convertedCount} PolygonMech materials to URP!");
        }

        private static void ConvertLitMaterial(Material mat, Shader litShader)
        {
            // Cache legacy Standard properties
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1.0f;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.0f;

            // Switch to URP Lit
            mat.shader = litShader;

            // Apply URP Lit properties
            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
            }
            mat.SetColor("_BaseColor", color);

            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
            }

            bool hasEmission = emissionMap != null || (emissionColor.maxColorComponent > 0.01f);
            if (hasEmission)
            {
                if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Surface", 0f); // Opaque
            mat.renderQueue = (int)RenderQueue.Geometry;
        }

        private static void ConvertParticleMaterial(Material mat, Shader particleShader)
        {
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color col = Color.white;

            if (mat.HasProperty("_TintColor") && mat.GetColor("_TintColor").a > 0.001f)
            {
                col = mat.GetColor("_TintColor");
            }
            else if (mat.HasProperty("_Color"))
            {
                col = mat.GetColor("_Color");
            }

            int dstBlend = mat.HasProperty("_DstBlend") ? (int)mat.GetFloat("_DstBlend") : 10;
            bool isAdditive = dstBlend == 1; // BlendMode.One

            mat.shader = particleShader;

            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
            }
            mat.SetColor("_BaseColor", col);

            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", isAdditive ? 1f : 0f); // 1 = Additive, 0 = Alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", isAdditive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
