using System.Collections;
using UnityEngine;
using Photon.Pun;
using Com.IVE.ECL.RemoteCollaboration;

/// <summary>
/// REMOTE-SIDE ONLY — receives guiding-cue state streamed by TrackingVisualsSync on
/// the local device, and reconstructs the visuals locally:
///   • AxisDottedArcArrows3D rings  (orbiting a lightweight proxy transform)
///   • LineRenderer direction arrow  (no external prefab required)
///
/// ── Scene Setup (REMOTE scene only) ──────────────────────────────────────
///   1. Create an empty GameObject named "TrackingVisualsMgr".
///   2. Add a PhotonView: ViewID = 999, OwnershipTransfer = Takeover,
///      Synchronization = Unreliable On Change.
///   3. Add this TrackingVisualsReceiver component.
///   4. Drag TrackingVisualsReceiver into the PhotonView's ObservedComponents list.
///
///   On the LOCAL scene, use TrackingVisualsSync on the same-named object instead.
/// </summary>
public class TrackingVisualsReceiver : MonoBehaviourPun, IPunObservable
{
    [Header("Arc ring appearance")]
    public float arcRadius        = 0.25f;
    public float arcFlowDegPerSec = 30f;

    // ── Constants ─────────────────────────────────────────────────────────
    private const int    CUBE_COUNT       = 7;   // must match TrackingVisualsSync
    private const string ROOM_ANCHOR_NAME = "ScannedRoom";

    // ── Coordinate anchor ─────────────────────────────────────────────────
    private Transform _roomAnchor;
    private bool      _anchorReady;

    // ── Received state ────────────────────────────────────────────────────
    private readonly bool[]       _vis        = new bool[CUBE_COUNT];
    private readonly Vector3[]    _cubePos    = new Vector3[CUBE_COUNT];
    private readonly Quaternion[] _cubeRot    = new Quaternion[CUBE_COUNT];
    private readonly Vector3[]    _aStart     = new Vector3[CUBE_COUNT];
    private readonly Vector3[]    _aEnd       = new Vector3[CUBE_COUNT];
    private readonly float[]      _arcX       = new float[CUBE_COUNT];
    private readonly float[]      _arcY       = new float[CUBE_COUNT];
    private readonly float[]      _arcZ       = new float[CUBE_COUNT];
    private readonly bool[]       _reverse         = new bool[CUBE_COUNT];
    private readonly bool[]       _nearTranslation = new bool[CUBE_COUNT];
    private readonly bool[]       _nearRotX        = new bool[CUBE_COUNT];
    private readonly bool[]       _nearRotY        = new bool[CUBE_COUNT];
    private readonly bool[]       _nearRotZ        = new bool[CUBE_COUNT];

    // ── Proxy visuals ─────────────────────────────────────────────────────
    private readonly bool[]                  _lastVis     = new bool[CUBE_COUNT];
    private readonly GameObject[]            _arcHosts    = new GameObject[CUBE_COUNT];
    private readonly AxisDottedArcArrows3D[] _arcComps    = new AxisDottedArcArrows3D[CUBE_COUNT];
    private readonly ArrowRenderer[]         _arrows      = new ArrowRenderer[CUBE_COUNT];

    // ── Condition gate — guiding cues only shown when condition allows ────────
    // Remote's ExperimentEventBus is kept in sync by SelectionSyncManager (Photon RPC).
    // _vis[i] = IsTracked (wireframe) is always applied; arc rings / arrows are
    // additionally gated by _cuesAllowed so they disappear when cues are disabled.
    private bool _cuesAllowed;

    // ── WireFrame model visibility (hide/show in sync with local tracking) ──
    // WireframeVisibility components self-register in WireframeVisibility.All on OnEnable
    // and hide their own renderers in Awake — no polling needed here.
    // Once all CUBE_COUNT instances have registered we sort by name (matches _vis[] order)
    // and cache the array for O(1) per-frame access.
    private WireframeVisibility[] _wfSorted;

    // Smooth "aligned" circles per cube: 0=X red, 1=Y green, 2=Z blue, 3=translation orange
    private readonly LineRenderer[][] _alignCircles = new LineRenderer[CUBE_COUNT][];

    private const int   CIRCLE_SEGMENTS      = 48;
    private const float ALIGN_CIRCLE_RADIUS  = 0.025f;   // 5 cm diameter
    private static readonly Color[] CIRCLE_COLORS =
    {
        new Color(1f,    0.25f, 0.25f, 0.9f), // red    — X
        new Color(0.25f, 1f,    0.25f, 0.9f), // green  — Y
        new Color(0.25f, 0.50f, 1f,    0.9f), // blue   — Z
        new Color(1f,    1f,    1f,    0.9f), // white  — translation
    };

    // ─────────────────────────────────────────────────────────────────────
    void OnEnable()
    {
        ExperimentEventBus.OnConditionChanged += HandleConditionChange;
    }

    void OnDisable()
    {
        ExperimentEventBus.OnConditionChanged -= HandleConditionChange;
    }

    private void HandleConditionChange(string condition)
    {
        _cuesAllowed = (condition == "GuidingCues" || condition == "Cues and EyeGaze");
        // If cues just got disabled, immediately hide all proxy visuals
        if (!_cuesAllowed)
        {
            for (int i = 0; i < CUBE_COUNT; i++)
            {
                if (_arcHosts[i] != null) _arcHosts[i].SetActive(false);
                if (_arrows[i]   != null) _arrows[i].gameObject.SetActive(false);
                if (_alignCircles[i] != null)
                    foreach (var lr in _alignCircles[i])
                        if (lr != null) lr.gameObject.SetActive(false);
                _lastVis[i] = false; // force re-evaluation next time cues are enabled
            }
        }
    }

    void Start()
    {
        StartCoroutine(WaitForAnchor());
        HandleConditionChange(ExperimentEventBus.CurrentCondition);
    }

    /// <summary>
    /// Waits until MRUKRoomAligner has fully aligned ScannedRoom before
    /// caching the anchor — same pattern as ManualSync to prevent proxy
    /// visuals appearing at wrong world positions on remote restart.
    /// </summary>
    private IEnumerator WaitForAnchor()
    {
        MRUKRoomAligner aligner = FindObjectOfType<MRUKRoomAligner>();
        if (aligner != null)
        {
            // Wait for alignment, but cap at 10 s — if MRUK fails (e.g. error -2)
            // the loop would otherwise spin forever and block all cue rendering.
            float waited = 0f;
            while (!aligner.IsAligned && waited < 10f)
            {
                yield return null;
                waited += Time.deltaTime;
            }
            if (!aligner.IsAligned)
                Debug.LogWarning("[TrackingVisualsReceiver] MRUKRoomAligner did not align " +
                                 "within 10 s — proceeding without room anchor.");
        }
        else
        {
            yield return null;
        }

        var go = GameObject.Find(ROOM_ANCHOR_NAME);
        if (go != null)
            _roomAnchor = go.transform;
        else
            Debug.LogWarning("[TrackingVisualsReceiver] Room anchor not found after alignment.");

        _anchorReady = true;
    }

    void Update()
    {
        // Once all WireframeVisibility instances have registered, sort by name
        // (alphabetical = same order as TrackingVisualsSync's _vis[] array) and cache.
        if (_wfSorted == null && WireframeVisibility.All.Count == CUBE_COUNT)
        {
            _wfSorted = WireframeVisibility.All.ToArray();
            System.Array.Sort(_wfSorted, (a, b) =>
                string.Compare(a.gameObject.name, b.gameObject.name,
                               System.StringComparison.Ordinal));
            Debug.Log("[TrackingVisualsReceiver] WireframeVisibility list ready — " +
                      "wireframes already hidden by WireframeVisibility.Awake.");
        }

        if (!_anchorReady) return;

        if (!photonView.IsMine)
            ApplyState();
    }

    // ─────────────────────────────────────────────────────────────────────
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsReading)
        {
            ReadState(stream);
        }
        else
        {
            // Fallback: if this device unexpectedly owns the view, send empty data
            // so the local device's reader doesn't throw a stream-length mismatch.
            for (int i = 0; i < CUBE_COUNT; i++)
            {
                stream.SendNext(false);               // vis
                stream.SendNext(Vector3.zero);        // cubePos
                stream.SendNext(Quaternion.identity); // cubeRot
                stream.SendNext(Vector3.zero);        // arrowStart
                stream.SendNext(Vector3.zero);        // arrowEnd
                stream.SendNext(0f);                  // arcX
                stream.SendNext(0f);                  // arcY
                stream.SendNext(0f);                  // arcZ
                stream.SendNext(false);               // reverseFlow
                stream.SendNext(false);               // nearTranslation
                stream.SendNext(false);               // nearRotX
                stream.SendNext(false);               // nearRotY
                stream.SendNext(false);               // nearRotZ
            }
        }
    }

    // ── Read stream ───────────────────────────────────────────────────────
    private void ReadState(PhotonStream stream)
    {
        for (int i = 0; i < CUBE_COUNT; i++)
        {
            _vis[i]        = (bool)      stream.ReceiveNext();
            _cubePos[i]    = (Vector3)   stream.ReceiveNext();
            _cubeRot[i]    = (Quaternion)stream.ReceiveNext();
            _aStart[i]     = (Vector3)   stream.ReceiveNext();
            _aEnd[i]       = (Vector3)   stream.ReceiveNext();
            _arcX[i]       = (float)     stream.ReceiveNext();
            _arcY[i]       = (float)     stream.ReceiveNext();
            _arcZ[i]       = (float)     stream.ReceiveNext();
            _reverse[i]         = (bool) stream.ReceiveNext();
            _nearTranslation[i] = (bool) stream.ReceiveNext();
            _nearRotX[i]        = (bool) stream.ReceiveNext();
            _nearRotY[i]        = (bool) stream.ReceiveNext();
            _nearRotZ[i]        = (bool) stream.ReceiveNext();
        }
    }

    // ── Apply received state to proxy visuals ─────────────────────────────
    private void ApplyState()
    {
        for (int i = 0; i < CUBE_COUNT; i++)
        {
            // Arc rings / arrows: gated by BOTH tracking state AND condition
            bool showCues = _cuesAllowed && _vis[i];
            if (showCues != _lastVis[i])
            {
                _lastVis[i] = showCues;
                if (showCues)
                {
                    EnsureProxy(i);
                    _arcHosts[i].SetActive(true);
                    if (_arrows[i] != null) _arrows[i].gameObject.SetActive(true);
                }
                else
                {
                    if (_arcHosts[i] != null) _arcHosts[i].SetActive(false);
                    if (_arrows[i]   != null) _arrows[i].gameObject.SetActive(false);
                    if (_alignCircles[i] != null)
                        foreach (var lr in _alignCircles[i])
                            if (lr != null) lr.gameObject.SetActive(false);
                }
            }

            // Mirror local tracking state: show/hide WireframeModel renderers
            if (_wfSorted != null && i < _wfSorted.Length && _wfSorted[i] != null)
                _wfSorted[i].SetVisible(_vis[i]);

            if (!showCues || _arcHosts[i] == null) continue;

            // Update arc proxy transform to mirror the remote soma cube
            _arcHosts[i].transform.position = ToWorld(_cubePos[i]);
            _arcHosts[i].transform.rotation = ToWorldRot(_cubeRot[i]);

            // Rotation rings: hide arrow ring when aligned → smooth circle; arc when not
            _arcComps[i].hideRingX   = _nearRotX[i];
            _arcComps[i].hideRingY   = _nearRotY[i];
            _arcComps[i].hideRingZ   = _nearRotZ[i];
            _arcComps[i].arcSpanX    = _arcX[i];
            _arcComps[i].arcSpanY    = _arcY[i];
            _arcComps[i].arcSpanZ    = _arcZ[i];
            _arcComps[i].reverseFlow = _reverse[i];

            // Smooth circles at proxy position for aligned axes
            EnsureAlignCircles(i);
            Vector3    center = _arcHosts[i].transform.position;
            Quaternion rot    = _arcHosts[i].transform.rotation;
            UpdateProxyCircle(i, 0, _nearRotX[i],         rot * Vector3.right,   center, ALIGN_CIRCLE_RADIUS);
            UpdateProxyCircle(i, 1, _nearRotY[i],         rot * Vector3.up,      center, ALIGN_CIRCLE_RADIUS);
            UpdateProxyCircle(i, 2, _nearRotZ[i],         rot * Vector3.forward, center, ALIGN_CIRCLE_RADIUS);
            UpdateProxyCircle(i, 3, _nearTranslation[i],  Vector3.up,            center, 0.035f);  // 7 cm diameter

            // Translation arrow: hide when position is aligned, show otherwise
            bool showArrow = !_nearTranslation[i];
            if (_arrows[i] != null)
            {
                _arrows[i].gameObject.SetActive(showArrow);
                if (showArrow)
                    _arrows[i].SetPositions(ToWorld(_aStart[i]), ToWorld(_aEnd[i]));
            }
        }
    }

    // ── Lazy proxy creation ───────────────────────────────────────────────
    /// <summary>
    /// Creates proxy visuals on first use for cube index i.
    ///
    /// Arc rings:
    ///   ArcProxy_i  (empty host — mirrors the soma cube's world transform)
    ///   └─ ArcRings (AxisDottedArcArrows3D with autoUseParentAsTarget = true)
    ///
    /// Arrow:
    ///   RemoteArrow_i  (LineRenderer, orange, world-space, no prefab needed)
    /// </summary>
    private void EnsureProxy(int i)
    {
        if (_arcHosts[i] != null) return;

        // Host whose transform is driven to match the remote soma cube position/rotation
        var host = new GameObject($"ArcProxy_{i}");
        _arcHosts[i] = host;

        // AxisDottedArcArrows3D on a child: autoUseParentAsTarget → target = host
        var arcGO = new GameObject("ArcRings");
        arcGO.transform.SetParent(host.transform, false);

        var arc = arcGO.AddComponent<AxisDottedArcArrows3D>();
        arc.autoUseParentAsTarget = true;
        arc.useLocalBoundsCenter  = false; // proxy has no renderers; orbit exact centre
        arc.radius                = arcRadius;
        arc.flowDegPerSec         = arcFlowDegPerSec;
        arc.arcSpanDeg            = 180f;  // fallback; overridden each frame via arcSpanX/Y/Z
        _arcComps[i] = arc;

        // Direction arrow — same DirectionArrow prefab as the local Quest side
        var arrowPrefab = Resources.Load<GameObject>("DirectionArrow");
        if (arrowPrefab == null)
        {
            Debug.LogError("[TrackingVisualsReceiver] DirectionArrow prefab not found in Resources!");
        }
        else
        {
            var arrowGO      = Object.Instantiate(arrowPrefab);
            arrowGO.name     = $"RemoteArrow_{i}";
            arrowGO.SetActive(false);
            _arrows[i]       = arrowGO.GetComponent<ArrowRenderer>();
            if (_arrows[i] == null)
                Debug.LogError("[TrackingVisualsReceiver] DirectionArrow prefab is missing ArrowRenderer component!");
            else
                _arrows[i].tintColor = Color.white;   // match local Quest side
        }
    }

    // ── Smooth circle helpers ─────────────────────────────────────────────

    private void EnsureAlignCircles(int i)
    {
        if (_alignCircles[i] != null) return;
        _alignCircles[i] = new LineRenderer[4];   // 0=X 1=Y 2=Z 3=translation

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        for (int k = 0; k < 4; k++)
        {
            var go = new GameObject($"AlignCircle_{i}_{k}");
            var lr = go.AddComponent<LineRenderer>();
            lr.loop          = true;
            lr.positionCount = CIRCLE_SEGMENTS;
            lr.startWidth    = 0.005f;
            lr.endWidth      = 0.005f;
            lr.useWorldSpace = true;
            if (sh != null)
            {
                lr.material       = new Material(sh);
                lr.material.color = CIRCLE_COLORS[k];
            }
            lr.gameObject.SetActive(false);
            _alignCircles[i][k] = lr;
        }
    }

    private void UpdateProxyCircle(int i, int k, bool show, Vector3 worldAxis, Vector3 center, float radius)
    {
        if (_alignCircles[i] == null || _alignCircles[i][k] == null) return;
        _alignCircles[i][k].gameObject.SetActive(show);
        if (!show) return;

        Vector3 perp = Vector3.Cross(worldAxis, Vector3.up);
        if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(worldAxis, Vector3.right);
        perp.Normalize();
        Vector3 perp2 = Vector3.Cross(worldAxis, perp).normalized;

        for (int s = 0; s < CIRCLE_SEGMENTS; s++)
        {
            float   a   = s * Mathf.PI * 2f / CIRCLE_SEGMENTS;
            Vector3 pos = center + (perp * Mathf.Cos(a) + perp2 * Mathf.Sin(a)) * radius;
            _alignCircles[i][k].SetPosition(s, pos);
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────
    private Vector3    ToWorld(Vector3 local) =>
        _roomAnchor != null ? _roomAnchor.TransformPoint(local)                   : local;
    private Quaternion ToWorldRot(Quaternion localRot) =>
        _roomAnchor != null ? _roomAnchor.rotation * localRot                     : localRot;

    // ── Cleanup ───────────────────────────────────────────────────────────
    void OnDestroy()
    {
        for (int i = 0; i < CUBE_COUNT; i++)
        {
            if (_arcHosts[i] != null) Destroy(_arcHosts[i]);
            if (_arrows[i]   != null) Destroy(_arrows[i].gameObject);
            if (_alignCircles[i] != null)
                foreach (var lr in _alignCircles[i])
                    if (lr != null) Destroy(lr.gameObject);
        }
    }
}
