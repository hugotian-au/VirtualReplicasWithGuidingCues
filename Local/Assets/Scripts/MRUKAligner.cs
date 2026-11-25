using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Aligns ScannedRoom with the physical room.
///
/// Why spatial anchors are necessary:
///   After a long Cable-Link disconnect the Quest's SLAM tracking origin can
///   shift substantially.  MRUK positions are expressed in SLAM space, so the
///   effect mesh drifts relative to passthrough — any MRUK-relative approach
///   inherits that drift.  OVRSpatialAnchor is specifically designed to restore
///   a physically-correct world position after SLAM re-initialisation; the
///   anchor system compensates for SLAM drift so the reported position stays at
///   the correct physical location in the passthrough view.
///
/// Flow:
///   1. If a saved anchor UUID exists → try to restore it (90 s timeout so the
///      user has time to put on the headset and look around the room).
///      On success → apply pose to ScannedRoom, done.
///      On failure → fall back to MRUK (path 2), but keep the OLD anchor UUID
///                   so the next session can try it again.
///   2. MRUK alignment (first run, or anchor permanently lost) →
///      position ScannedRoom, then immediately create & save a new anchor
///      so future sessions skip MRUK entirely.
///
/// Tip: if the anchor fails consistently after long disconnects, make sure the
/// headset cameras can see the room before launching (don't start in a bag/dark).
/// </summary>
public class MRUKRoomAligner : MonoBehaviour
{
    [Header("Alignment Settings")]
    [SerializeField] private bool alignOnLoad = true;
    [SerializeField] private bool disableEffectMeshAfterAlign = true;

    [Header("Fine Tuning (world-space, applied on top of both paths)")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    // ── Public state ──────────────────────────────────────────────────────
    public bool IsAligned => _isAligned;

    // ── Private state ─────────────────────────────────────────────────────
    private bool _isAligned   = false;
    private bool _sceneLoaded = false;

    private const string UUID_KEY       = "ScannedRoomAnchorUUID";
    private const float  ANCHOR_TIMEOUT = 20f;  // seconds — give SLAM time to re-localise
    private const float  REFINE_TIMEOUT = 3f;   // seconds to wait for pose to stabilise
    private const float  STABLE_POS     = 0.001f;
    private const float  STABLE_DEG     = 0.05f;
    private const float  STABLE_HOLD    = 0.4f;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Start()
    {
        StartCoroutine(AlignmentFlow());
    }

    private void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneLoadedEvent.RemoveListener(HandleSceneLoaded);
    }

    // ── Main flow ─────────────────────────────────────────────────────────

    private IEnumerator AlignmentFlow()
    {
        // ── Path 1: Restore from spatial anchor ───────────────────────────
        if (PlayerPrefs.HasKey(UUID_KEY))
        {
            yield return TryRestoreAnchor();
            if (_isAligned)
            {
                Debug.Log("[MRUKRoomAligner] Aligned via spatial anchor.");

                // Subscribe so MRUK effect meshes are hidden when they appear
                if (disableEffectMeshAfterAlign && MRUK.Instance != null)
                {
                    MRUK.Instance.SceneLoadedEvent.AddListener(HandleSceneLoaded);
                    if (MRUK.Instance.IsInitialized && MRUK.Instance.GetCurrentRoom() != null)
                        HandleSceneLoaded();
                }
                yield break;
            }
            // Keep the old UUID — don't delete it. It may still work next session
            // once the user looks around more. Only run MRUK as a one-shot fallback.
            Debug.LogWarning("[MRUKRoomAligner] Anchor restore failed — using MRUK this session. " +
                             "Old anchor UUID preserved for next launch.");
        }

        // ── Path 2: MRUK-based alignment ──────────────────────────────────
        if (!alignOnLoad || MRUK.Instance == null) yield break;

        MRUK.Instance.SceneLoadedEvent.AddListener(HandleSceneLoaded);
        if (MRUK.Instance.IsInitialized && MRUK.Instance.GetCurrentRoom() != null)
            HandleSceneLoaded();

        while (!_isAligned) yield return null;

        // Save a new anchor so future sessions can skip MRUK
        yield return SaveAnchor();
    }

    public void HandleSceneLoaded()
    {
        if (_isAligned && disableEffectMeshAfterAlign)
        {
            DisableAllEffectMeshes();
            MRUK.Instance.SceneLoadedEvent.RemoveListener(HandleSceneLoaded);
            return;
        }
        _sceneLoaded = true;
    }

    private void LateUpdate()
    {
        if (_isAligned || !_sceneLoaded) return;
        if (MRUK.Instance == null || !MRUK.Instance.IsInitialized) return;
        if (MRUK.Instance.GetCurrentRoom() == null) return;

        if (!ExecuteAlignmentRoutine()) return;

        _isAligned = true;
        if (disableEffectMeshAfterAlign) DisableAllEffectMeshes();
    }

    // ── Spatial Anchor: Restore ───────────────────────────────────────────

    private IEnumerator TryRestoreAnchor()
    {
        string uuidStr = PlayerPrefs.GetString(UUID_KEY, "");
        if (!Guid.TryParse(uuidStr, out Guid savedUuid))
        {
            Debug.LogWarning("[MRUKRoomAligner] Saved UUID malformed — clearing.");
            PlayerPrefs.DeleteKey(UUID_KEY);
            yield break;
        }

        Debug.Log($"[MRUKRoomAligner] Waiting for anchor {savedUuid} to localise " +
                  $"(timeout {ANCHOR_TIMEOUT} s) — look around the room …");

        var unboundList = new List<OVRSpatialAnchor.UnboundAnchor>();
        bool loadDone = false;

        OVRSpatialAnchor.LoadUnboundAnchors(
            new OVRSpatialAnchor.LoadOptions
            {
                StorageLocation = OVRSpace.StorageLocation.Local,
                Uuids           = new[] { savedUuid }
            },
            result =>
            {
                if (result != null) unboundList.AddRange(result);
                loadDone = true;
            });

        yield return WaitForFlag(() => loadDone, ANCHOR_TIMEOUT);

        if (unboundList.Count == 0)
        {
            Debug.LogWarning("[MRUKRoomAligner] Anchor not found in local storage.");
            yield break;
        }

        var host   = new GameObject("_AnchorRestoreHost");
        var anchor = host.AddComponent<OVRSpatialAnchor>();
        unboundList[0].BindTo(anchor);

        // Long wait — after a long disconnect the SLAM map may take 30-60 s to
        // re-localise.  The user should look around the room during this time.
        float logInterval = 5f, logTimer = 0f;
        float elapsed = 0f;
        while (!anchor.Localized && elapsed < ANCHOR_TIMEOUT)
        {
            elapsed   += Time.deltaTime;
            logTimer  += Time.deltaTime;
            if (logTimer >= logInterval)
            {
                Debug.Log($"[MRUKRoomAligner] Still waiting for anchor localisation " +
                          $"({elapsed:F0}/{ANCHOR_TIMEOUT:F0} s) …");
                logTimer = 0f;
            }
            yield return null;
        }

        if (!anchor.Localized)
        {
            Debug.LogWarning("[MRUKRoomAligner] Anchor localisation timed out.");
            Destroy(host);
            yield break;
        }

        // Let the runtime refine the pose before we read it
        yield return WaitForPoseStable(host.transform, REFINE_TIMEOUT,
                                       STABLE_POS, STABLE_DEG, STABLE_HOLD);

        transform.position = host.transform.position + positionOffset;
        transform.rotation = host.transform.rotation * Quaternion.Euler(rotationOffset);
        _isAligned = true;

        Debug.Log($"[MRUKRoomAligner] Anchor localised. " +
                  $"pos={transform.position}, rot={transform.rotation.eulerAngles:F1}");

        if (disableEffectMeshAfterAlign) DisableAllEffectMeshes();
        Destroy(host);
    }

    // ── Spatial Anchor: Save ──────────────────────────────────────────────

    private IEnumerator SaveAnchor()
    {
        var host = new GameObject("_AnchorSaveHost");
        host.transform.position = transform.position - positionOffset;
        host.transform.rotation = transform.rotation
                                * Quaternion.Inverse(Quaternion.Euler(rotationOffset));

        var anchor = host.AddComponent<OVRSpatialAnchor>();

        yield return WaitForFlag(() => anchor.Created, 10f);

        if (!anchor.Created)
        {
            Debug.LogWarning("[MRUKRoomAligner] Anchor creation timed out.");
            Destroy(host);
            yield break;
        }

        bool saveDone = false, saveOK = false;
        anchor.Save((a, ok) => { saveOK = ok; saveDone = true; });
        yield return WaitForFlag(() => saveDone, 10f);

        if (saveOK)
        {
            PlayerPrefs.SetString(UUID_KEY, anchor.Uuid.ToString());
            PlayerPrefs.Save();
            Debug.Log($"[MRUKRoomAligner] Anchor saved: {anchor.Uuid}");
        }
        else
        {
            Debug.LogWarning("[MRUKRoomAligner] Anchor save failed.");
        }

        Destroy(host);
    }

    // ── MRUK Alignment ────────────────────────────────────────────────────

    private bool ExecuteAlignmentRoutine()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        Bounds     roomBounds  = new Bounds();
        bool       boundsInit  = false;
        MRUKAnchor floorAnchor = null;
        MRUKAnchor bestWall    = null;
        float      maxArea     = 0f;

        foreach (var anchor in room.Anchors)
        {
            if (anchor.name.Contains("GLOBAL_MESH")) continue;

            MeshRenderer mr = anchor.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                if (!boundsInit) { roomBounds = mr.bounds; boundsInit = true; }
                else             { roomBounds.Encapsulate(mr.bounds); }
            }

            if (floorAnchor == null &&
                anchor.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR))
                floorAnchor = anchor;

            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE))
            {
                float area = 0f;
                if (anchor.PlaneRect.HasValue)
                    area = anchor.PlaneRect.Value.width * anchor.PlaneRect.Value.height;
                else if (anchor.VolumeBounds.HasValue)
                    area = anchor.VolumeBounds.Value.size.x * anchor.VolumeBounds.Value.size.z;
                if (area > maxArea) { maxArea = area; bestWall = anchor; }
            }
        }

        if (!boundsInit || floorAnchor == null || bestWall == null) return false;

        Vector3 pos     = new Vector3(roomBounds.center.x,
                                      floorAnchor.transform.position.y,
                                      roomBounds.center.z);
        Vector3 forward = Vector3.ProjectOnPlane(
            bestWall.transform.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.01f) return false;

        transform.position = pos     + positionOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up)
                           * Quaternion.Euler(rotationOffset);
        return true;
    }

    // ── Utilities ─────────────────────────────────────────────────────────

    private static IEnumerator WaitForFlag(Func<bool> pred, float timeout)
    {
        float t = 0f;
        while (!pred() && t < timeout) { t += Time.deltaTime; yield return null; }
    }

    private static IEnumerator WaitForPoseStable(
        Transform t, float timeout, float epsPos, float epsDeg, float hold)
    {
        float elapsed = 0f, stable = 0f;
        Vector3 lp = t.position; Quaternion lr = t.rotation;
        while (elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
            if (Vector3.Distance(t.position, lp) < epsPos &&
                Quaternion.Angle(t.rotation, lr)  < epsDeg)
            {
                stable += Time.deltaTime;
                if (stable >= hold) { Debug.Log($"[MRUKRoomAligner] Pose stable at {elapsed:F2} s."); yield break; }
            }
            else { stable = 0f; lp = t.position; lr = t.rotation; }
        }
        Debug.LogWarning("[MRUKRoomAligner] Pose-stable timeout — using best available pose.");
    }

    private void DisableAllEffectMeshes()
    {
        foreach (var mesh in FindObjectsOfType<EffectMesh>())
            mesh.gameObject.SetActive(false);

        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        if (room == null) return;
        foreach (var anchor in room.Anchors)
            foreach (var r in anchor.GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

        Debug.Log("[MRUKRoomAligner] Effect meshes hidden.");
    }

    // ── Editor helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Clears the saved anchor UUID. Next launch re-runs MRUK alignment and
    /// creates a fresh anchor. Use after room re-scan.
    /// </summary>
    [ContextMenu("Clear Saved Anchor")]
    private void ClearSavedAnchor()
    {
        PlayerPrefs.DeleteKey(UUID_KEY);
        PlayerPrefs.Save();
        Debug.Log("[MRUKRoomAligner] Anchor cleared — MRUK will re-align on next launch.");
    }
}
