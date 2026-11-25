using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools → Assign TOri_Both Physics Material
/// Assigns SomaTOri_Contact physics material to every Collider on all
/// SomaCube*TOri_Both prefabs in Resources.
/// </summary>
public class AssignTOriPhysicsMaterial : MonoBehaviour
{
    [MenuItem("Tools/Assign TOri_Both Physics Material")]
    static void Execute()
    {
        // Try both extensions — Unity uses .physicMaterial (no 's')
        PhysicsMaterial pm =
            AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/materials/SomaTOri_Contact.physicMaterial") ??
            AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/materials/SomaTOri_Contact.physicsMaterial");

        if (pm == null)
        {
            // Create it if still not found
            pm = new PhysicsMaterial("SomaTOri_Contact")
            {
                dynamicFriction = 0.4f,
                staticFriction  = 0.4f,
                bounciness      = 0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine   = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(pm, "Assets/materials/SomaTOri_Contact.physicMaterial");
            AssetDatabase.SaveAssets();
            Debug.Log("[AssignTOriPhysicsMaterial] Created new physics material.");
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
        int prefabCount = 0, colliderCount = 0;

        foreach (string guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(prefabPath).Contains("TOri_Both")) continue;

            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root = scope.prefabContentsRoot;

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                col.sharedMaterial = pm;
                colliderCount++;
            }

            prefabCount++;
            Debug.Log($"[AssignTOriPhysicsMaterial] Updated: {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AssignTOriPhysicsMaterial] Done — {prefabCount} prefabs, {colliderCount} colliders.");
    }
}
