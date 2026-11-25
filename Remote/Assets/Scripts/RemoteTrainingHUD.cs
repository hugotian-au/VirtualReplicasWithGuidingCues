using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Training HUD for the remote PC.
/// Uses a World-Space canvas parented to Camera.main so it renders both on the
/// PC monitor and inside a connected headset (Screen Space – Overlay is invisible in VR).
///
/// Created automatically by SelectionSyncManager — no manual Inspector setup needed.
/// </summary>
public class RemoteTrainingHUD : MonoBehaviour
{
    [Header("Placement (camera-local offset)")]
    public float distanceFromHead = 1.5f;
    public float verticalOffset   = -0.05f;

    // ── internal refs ────────────────────────────────────────────────────────
    private TextMeshProUGUI _phaseNameText;
    private TextMeshProUGUI _countdownText;
    private Coroutine       _countdownRoutine;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        BuildHUD();
        AttachToCamera();
        gameObject.SetActive(false);   // hidden until ShowPhase() is called
    }

    void AttachToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[RemoteTrainingHUD] Camera.main is null — HUD will not follow the view.");
            return;
        }

        transform.SetParent(cam.transform, worldPositionStays: false);
        transform.localPosition = new Vector3(0f, verticalOffset, distanceFromHead);
        transform.localRotation = Quaternion.identity;
        transform.localScale    = Vector3.one;
        Debug.Log($"[RemoteTrainingHUD] Attached to '{cam.name}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API (called by SelectionSyncManager)
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowPhase(string phaseName, float duration)
    {
        gameObject.SetActive(true);
        _phaseNameText.text = phaseName;

        if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(CountdownRoutine(duration));
    }

    public void Hide()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Countdown
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator CountdownRoutine(float duration)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            UpdateCountdown(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        UpdateCountdown(0f);
    }

    void UpdateCountdown(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        int m = total / 60;
        int s = total % 60;
        if (_countdownText != null)
            _countdownText.text = m > 0 ? $"{m}:{s:D2}" : $"{s}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Build World-Space Canvas + TMPro UI (procedural, no prefab needed)
    //
    //  Canvas: 800 × 300 "px"  ×  localScale 0.001  =  0.8 m × 0.3 m world
    // ─────────────────────────────────────────────────────────────────────────

    void BuildHUD()
    {
        // ── Canvas ──────────────────────────────────────────────────────────
        var canvasGO = new GameObject("HUD_Canvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        var crt = canvasGO.GetComponent<RectTransform>();
        crt.sizeDelta    = new Vector2(800f, 300f);
        crt.localScale   = Vector3.one * 0.001f;
        crt.localPosition = Vector3.zero;
        crt.localRotation = Quaternion.identity;

        // ── Dark background ──────────────────────────────────────────────────
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // ── Phase name — upper half ──────────────────────────────────────────
        _phaseNameText = MakeTMPText(
            parent:    canvasGO,
            goName:    "PhaseName",
            anchorMin: new Vector2(0f, 0.45f),
            anchorMax: new Vector2(1f, 1f),
            fontSize:  60f,
            color:     Color.white,
            bold:      false);

        // ── Countdown — lower half, yellow-gold ──────────────────────────────
        _countdownText = MakeTMPText(
            parent:    canvasGO,
            goName:    "Countdown",
            anchorMin: new Vector2(0f, 0f),
            anchorMax: new Vector2(1f, 0.45f),
            fontSize:  90f,
            color:     new Color(1f, 0.88f, 0.25f),
            bold:      true);
    }

    static TextMeshProUGUI MakeTMPText(
        GameObject parent,
        string     goName,
        Vector2    anchorMin,
        Vector2    anchorMax,
        float      fontSize,
        Color      color,
        bool       bold)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);

        var rt        = go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.sizeDelta  = Vector2.zero;
        rt.offsetMin  = new Vector2(10f, 4f);
        rt.offsetMax  = new Vector2(-10f, -4f);

        var tmp         = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize    = fontSize;
        tmp.fontStyle   = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment   = TextAlignmentOptions.Center;
        tmp.color       = color;
        tmp.text        = "";
        return tmp;
    }
}
