using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ArrowRenderer : MonoBehaviour
{
    private float height = 0.05f;
    private float segmentLength = 0.055f;
    private float fadeDistance = 0.035f;
    public float speed = 1f;

    /// <summary>Tint color applied to every segment and arrowhead. Defaults to white.</summary>
    [HideInInspector] public Color tintColor = Color.white;

    [SerializeField] GameObject arrowPrefab;
    [SerializeField] GameObject segmentPrefab;

    [Space] [SerializeField] Vector3 start;
    [SerializeField] Vector3 end;
    [SerializeField] Vector3 upwards = Vector3.up;

    Transform arrow;

    readonly List<Transform>      segments  = new List<Transform>();
    readonly List<MeshRenderer>   renderers = new List<MeshRenderer>();

    // Runtime-created materials so we never depend on prefab material colors
    Material _segMat;   // transparent, for segments
    Material _headMat;  // opaque, for arrowhead

    public void SetPositions(Vector3 start0, Vector3 end0)
    {
        start = start0;
        end   = end0;
        UpdateSegments();
    }

    void Update()
    {
        UpdateSegments();
    }

    // ── Material helpers ──────────────────────────────────────────────────

    static Shader FindShader()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh) return sh;
        sh = Shader.Find("Unlit/Color");
        if (sh) return sh;
        return Shader.Find("Sprites/Default");
    }

    void EnsureMaterials()
    {
        if (_segMat && _headMat) return;

        Shader sh = FindShader();

        // Segment material — transparent/fade
        _segMat = new Material(sh);
        if (_segMat.HasProperty("_Surface"))    _segMat.SetFloat("_Surface",  1f); // Transparent
        if (_segMat.HasProperty("_AlphaClip"))  _segMat.SetFloat("_AlphaClip", 0f);
        if (_segMat.HasProperty("_ZWrite"))     _segMat.SetFloat("_ZWrite",   0f);
        if (_segMat.HasProperty("_SrcBlend"))   _segMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_segMat.HasProperty("_DstBlend"))   _segMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        _segMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _segMat.renderQueue = 3000;
        ApplyTint(_segMat, tintColor);

        // Arrowhead material — opaque
        _headMat = new Material(sh);
        ApplyTint(_headMat, new Color(tintColor.r, tintColor.g, tintColor.b, 1f));
    }

    static void ApplyTint(Material mat, Color c)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
    }

    // ── Core rendering ────────────────────────────────────────────────────

    void UpdateSegments()
    {
        EnsureMaterials();

        float distance    = Vector3.Distance(start, end);
        float radius      = height / 2f + distance * distance / (8f * height);
        float diff        = radius - height;
        float angle       = 2f * Mathf.Acos(diff / radius);
        float length      = angle * radius;
        float segmentAngle = segmentLength / radius * Mathf.Rad2Deg;

        Vector3 center = new Vector3(0, -diff, distance / 2f);
        Vector3 left   = Vector3.zero;
        Vector3 right  = new Vector3(0, 0, distance);

        int segmentsCount = (int)(length / segmentLength) + 1;

        CheckSegments(segmentsCount - 2);

        float   offset           = Time.time * speed * segmentAngle;
        Vector3 firstSegmentPos  =
            Quaternion.Euler(Mathf.Repeat(offset, segmentAngle), 0f, 0f) * (left - center) + center;
        float   fadeStartDist    =
            (Quaternion.Euler(segmentAngle / 2f, 0f, 0f) * (left - center) + center).z;

        for (int i = 0; i < segmentsCount - 2; i++)
        {
            Vector3 pos = Quaternion.Euler(segmentAngle * i, 0f, 0f) * (firstSegmentPos - center) + center;
            segments[i].localPosition = pos;
            segments[i].localRotation = Quaternion.FromToRotation(Vector3.up, pos - center);

            MeshRenderer rend = renderers[i];
            if (!rend) continue;

            float a = GetAlpha(pos.z - left.z, right.z - fadeDistance - pos.z, fadeStartDist);
            ApplyTint(rend.material, new Color(tintColor.r, tintColor.g, tintColor.b, a));
        }

        if (!arrow)
        {
            arrow = Instantiate(arrowPrefab, transform).transform;
            // Replace arrowhead material with our runtime material
            foreach (var mr in arrow.GetComponentsInChildren<MeshRenderer>(true))
                mr.material = _headMat;
        }
        // Keep arrowhead tint in sync every frame
        ApplyTint(_headMat, new Color(tintColor.r, tintColor.g, tintColor.b, 1f));

        arrow.localPosition = right;
        arrow.localRotation = Quaternion.FromToRotation(Vector3.up, right - center);

        transform.position = start;
        transform.rotation = Quaternion.LookRotation(end - start, upwards);
    }

    void CheckSegments(int segmentsCount)
    {
        while (segments.Count < segmentsCount)
        {
            Transform    segment = Instantiate(segmentPrefab, transform).transform;
            MeshRenderer mr      = segment.GetComponent<MeshRenderer>();
            segments.Add(segment);
            renderers.Add(mr);
            // Assign runtime material so prefab baked color is irrelevant
            if (mr != null) mr.material = _segMat;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            GameObject seg = segments[i].gameObject;
            if (seg.activeSelf != i < segmentsCount)
                seg.SetActive(i < segmentsCount);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    static float GetAlpha(float distance0, float distance1, float distanceMax)
    {
        return Mathf.Clamp01(
            Mathf.Clamp01(distance0 / distanceMax) +
            Mathf.Clamp01(distance1 / distanceMax) - 1f);
    }
}
