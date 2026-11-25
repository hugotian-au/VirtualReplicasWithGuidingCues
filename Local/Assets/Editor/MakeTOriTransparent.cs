using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools → Make TOri_Both Semi-Transparent
///
/// For each SomaCube*TOri_Both prefab in Resources:
///   1. Finds MeshRenderers whose material is the FBX-embedded (URP Lit) one
///   2. Copies the material to Assets/materials/TOri/ with transparency enabled
///   3. Assigns the copy back into the prefab (persistent change, no runtime cost)
/// </summary>
public class MakeTOriTransparent : EditorWindow
{
    private float _alpha = 0.4f;

    [MenuItem("Tools/Make TOri_Both Semi-Transparent")]
    public static void ShowWindow() =>
        GetWindow<MakeTOriTransparent>("TOri Transparency");

    void OnGUI()
    {
        GUILayout.Label("Semi-transparent alpha for scanned mesh", EditorStyles.boldLabel);
        _alpha = EditorGUILayout.Slider("Alpha (0=invisible, 1=opaque)", _alpha, 0f, 1f);
        GUILayout.Space(8);
        if (GUILayout.Button("Apply to all SomaCube*TOri_Both prefabs"))
            Apply(_alpha);
    }

    static void Apply(float alpha)
    {
        const string matFolder = "Assets/materials/TOri";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder("Assets/materials", "TOri");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
        int processed = 0;

        foreach (string guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(prefabPath).Contains("TOri_Both")) continue;

            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root = scope.prefabContentsRoot;

            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = mr.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var orig = mats[i];
                    if (orig == null) continue;

                    // Only touch the URP Lit mesh material — skip the custom wireframe shader
                    if (!orig.HasProperty("_BaseColor")) continue;

                    string baseName = Path.GetFileNameWithoutExtension(prefabPath);
                    string matPath  = $"{matFolder}/{baseName}_transparent.mat";

                    // Re-use existing copy if already created; update its alpha
                    Material transMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (transMat == null)
                    {
                        transMat = new Material(orig);
                        AssetDatabase.CreateAsset(transMat, matPath);
                    }

                    // Enable URP Transparent mode
                    transMat.SetFloat("_Surface",    1f);   // 1 = Transparent
                    transMat.SetFloat("_ZWrite",     0f);
                    transMat.SetFloat("_SrcBlend",   (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    transMat.SetFloat("_DstBlend",   (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    transMat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                    transMat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    transMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    transMat.renderQueue = 3000;
                    transMat.SetOverrideTag("RenderType", "Transparent");

                    // Apply alpha
                    Color bc = transMat.HasProperty("_BaseColor")
                        ? transMat.GetColor("_BaseColor")
                        : Color.white;
                    transMat.SetColor("_BaseColor", new Color(bc.r, bc.g, bc.b, alpha));
                    transMat.SetColor("_Color",     new Color(bc.r, bc.g, bc.b, alpha));

                    EditorUtility.SetDirty(transMat);

                    mats[i] = transMat;
                    changed  = true;
                }

                if (changed)
                    mr.sharedMaterials = mats;
            }

            processed++;
            Debug.Log($"[MakeTOriTransparent] Processed: {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MakeTOriTransparent] Done — {processed} prefabs updated, alpha={alpha:F2}");
    }
}
