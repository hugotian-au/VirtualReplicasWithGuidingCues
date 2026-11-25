using UnityEngine;
using Photon.Pun;

/// <summary>
/// Attach to the "RemotePresence" PhotonView prefab (in Resources/).
///
/// Owner (remote player):  reads head + gaze data and writes to the PUN stream.
///   - Head pose is encoded relative to the shared room anchor
///     (Scaniverse_2026_04_25_091851), which exists in both scenes and represents
///     the same physical space. This makes the coordinates device-independent.
///
/// Non-owner (local player): reads stream, drives the head avatar and gaze reticle.
///   - Decoded using the local scene's instance of the same room anchor.
///
/// Prefab structure expected:
///   RemotePresence
///   ├── HeadAvatar   (any head mesh / renderer)
///   └── GazeReticle  (thin disc, moved freely in world space)
/// </summary>
public class RemotePresenceSync : MonoBehaviourPun, IPunObservable
{
    [Header("Owner-side (wire up on Remote scene)")]
    public Transform headTransform;
    public EyeGazeRay eyeGazeRay;

    [Header("Non-owner visualization (wire up in prefab)")]
    public GameObject headAvatarRoot;
    public Transform gazeReticle;

    [Tooltip("Vertical offset so the avatar head aligns with the received head position. " +
             "RPM avatars have their pivot at the feet, so ~-1.6 moves the avatar down correctly.")]
    public float avatarHeadOffsetY = -1.6f;

    [Tooltip("Minimum head Y in anchor-local space to accept. Filters out floor-level positions when headset is docked.")]
    public float minHeadAnchorY = 0.1f;

    // Name of the shared room-scan anchor present in both scenes
    private const string RoomAnchorName = "ScannedRoom";

    // Shared coordinate reference — same physical object in both scenes
    private Transform _roomAnchor;

    // Received state (non-owner only) — stored in room-anchor-local coords
    private Vector3 _headPos;
    private Quaternion _headRot = Quaternion.identity;
    private bool _remotePresent = true;
    private bool _hasHit;
    private int _hitViewID;
    private Vector3 _localHitPt;
    private Vector3 _localHitNorm = Vector3.up;

    // Eye gaze visibility — only shown when condition is "Cues and EyeGaze"
    private bool _eyeGazeAllowed = false;

    // ── Condition logic (Event Bus) ───────────────────────────────────────
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
        _eyeGazeAllowed = (condition == "Cues and EyeGaze");
        // 条件不允许时立即隐藏 reticle
        if (!_eyeGazeAllowed && gazeReticle != null)
            gazeReticle.gameObject.SetActive(false);
    }

    void Start()
    {
        // Both owner and non-owner need the anchor (owner for encoding, non-owner for decoding)
        var anchorGO = GameObject.Find(RoomAnchorName);
        if (anchorGO != null)
            _roomAnchor = anchorGO.transform;
        else
            Debug.LogWarning($"[RemotePresenceSync] Room anchor '{RoomAnchorName}' not found. " +
                             "Head pose will fall back to world coordinates.");

        // Auto-find EyeGazeRay if not wired up in Inspector
        if (eyeGazeRay == null)
            eyeGazeRay = GetComponentInChildren<EyeGazeRay>(true);
        if (eyeGazeRay == null)
            eyeGazeRay = FindObjectOfType<EyeGazeRay>();
        if (eyeGazeRay == null)
            Debug.LogWarning("[RemotePresenceSync] EyeGazeRay not found — gaze will fall back to head-forward.");

        if (headAvatarRoot != null)
            headAvatarRoot.SetActive(!photonView.IsMine);

        if (gazeReticle != null)
            gazeReticle.gameObject.SetActive(false);

        // 初始同步：拉取当前条件
        HandleConditionChange(ExperimentEventBus.CurrentCondition);
    }

    void Update()
    {
        if (photonView.IsMine) return;

        // Drive head avatar — convert from room-anchor-local coords to world coords
        if (headAvatarRoot != null)
        {
            Vector3 worldPos = _roomAnchor != null
                ? _roomAnchor.TransformPoint(_headPos)
                : _headPos;
            Quaternion worldRot = _roomAnchor != null
                ? _roomAnchor.rotation * _headRot
                : _headRot;

            headAvatarRoot.transform.position = worldPos;
            headAvatarRoot.transform.rotation = worldRot;
        }

        // Drive gaze reticle onto the soma cube surface
        // Only show when condition is "Cues and EyeGaze"
        if (gazeReticle != null)
        {
            if (_eyeGazeAllowed && _hasHit && _hitViewID != 0)
            {
                PhotonView hitView = PhotonView.Find(_hitViewID);
                if (hitView != null)
                {
                    gazeReticle.gameObject.SetActive(true);
                    gazeReticle.position = hitView.transform.TransformPoint(_localHitPt);
                    gazeReticle.rotation = Quaternion.FromToRotation(
                        Vector3.up, hitView.transform.TransformDirection(_localHitNorm));
                    return;
                }
            }
            gazeReticle.gameObject.SetActive(false);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Hide avatar when headset is on the dock (proximity sensor not triggered)
            bool isPresent = OVRManager.instance != null && OVRManager.instance.isUserPresent;
            stream.SendNext(isPresent);

            // Encode head pose relative to the shared room anchor.
            // Falls back to world coords if the anchor isn't found.
            Vector3 sendPos;
            Quaternion sendRot;
            if (_roomAnchor != null && headTransform != null)
            {
                sendPos = _roomAnchor.InverseTransformPoint(headTransform.position);
                sendRot = Quaternion.Inverse(_roomAnchor.rotation) * headTransform.rotation;
            }
            else
            {
                sendPos = headTransform != null ? headTransform.position : Vector3.zero;
                sendRot = headTransform != null ? headTransform.rotation : Quaternion.identity;
            }

            stream.SendNext(sendPos);
            stream.SendNext(sendRot);

            // Eye gaze direction in anchor-local space (falls back to head forward if no eye tracker)
            Vector3 gazeWorldDir = eyeGazeRay != null
                ? eyeGazeRay.GazeWorldDirection
                : (headTransform != null ? headTransform.forward : Vector3.forward);
            Vector3 gazeDirLocal = _roomAnchor != null
                ? _roomAnchor.InverseTransformDirection(gazeWorldDir)
                : gazeWorldDir;
            stream.SendNext(gazeDirLocal);
        }
        else
        {
            _remotePresent    = (bool)      stream.ReceiveNext();
            Vector3    rxPos  = (Vector3)   stream.ReceiveNext();
            Quaternion rxRot  = (Quaternion)stream.ReceiveNext();
            if (rxPos.y > minHeadAnchorY) { _headPos = rxPos; _headRot = rxRot; }
            _hasHit       = (bool)      stream.ReceiveNext();
            _hitViewID    = (int)       stream.ReceiveNext();
            _localHitPt   = (Vector3)   stream.ReceiveNext();
            _localHitNorm = (Vector3)   stream.ReceiveNext();
        }
    }

}
