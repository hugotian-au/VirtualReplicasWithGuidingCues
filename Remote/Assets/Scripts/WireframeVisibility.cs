using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to every SomaCube*TOri_WireFrame prefab (Remote project).
///
/// On Awake — finds the "WireframeModel" descendant and immediately hides its
/// renderers, so the wireframe is never briefly visible after instantiation.
///
/// NOTE: WireframeModel is NOT a direct child of root (it lives under MTObj4),
/// so Transform.Find(name) — which only searches direct children — does not
/// work here. We instead walk all descendants via GetComponentsInChildren.
///
/// Self-registers in a static list (All) so TrackingVisualsReceiver can
/// drive visibility without any polling or name-based searches.
/// </summary>
public class WireframeVisibility : MonoBehaviour
{
    /// <summary>All live instances, in no particular order.</summary>
    public static readonly List<WireframeVisibility> All = new List<WireframeVisibility>();

    private Renderer[] _renderers;

    void Awake()
    {
        // Transform.Find(name) searches DIRECT children only — not recursive.
        // WireframeModel is a grandchild (root → MTObj4 → WireframeModel),
        // so iterate the full subtree instead.
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "WireframeModel")
            {
                _renderers = t.GetComponentsInChildren<Renderer>(true);
                break;
            }
        }

        // Hide immediately — before the first rendered frame
        SetVisible(false);
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Show or hide the WireframeModel renderers.</summary>
    public void SetVisible(bool visible)
    {
        if (_renderers == null) return;
        foreach (var r in _renderers)
            if (r != null) r.enabled = visible;
    }
}
