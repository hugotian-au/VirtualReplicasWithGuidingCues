using UnityEngine;
using Photon.Pun;
using Com.IVE.ECL.RemoteCollaboration;

/// <summary>
/// LOCAL-SIDE ONLY — streams guiding-cue state (direction arrow + axis arc rings) to
/// the remote device via Photon PUN2.
///
/// ── Scene Setup (LOCAL side only) ────────────────────────────────────────
///   1. Create an empty GameObject named "TrackingVisualsMgr".
///   2. Add a PhotonView: ViewID = 999, OwnershipTransfer = Takeover,
///      Synchronization = Unreliable On Change.
///   3. Add this TrackingVisualsSync component.
///   4. Drag TrackingVisualsSync into the PhotonView's ObservedComponents list.
///
///   On the REMOTE side, use TrackingVisualsReceiver instead.
///
/// ── Stream format (per cube, 7 cubes total, fixed length) ────────────────
///   bool   visible
///   Vector3    cubePos   (room-anchor local)
///   Quaternion cubeRot   (room-anchor local)
///   Vector3    arrowStart (room-anchor local)
///   Vector3    arrowEnd   (room-anchor local)
///   float  arcSpanX / arcSpanY / arcSpanZ
///   bool   reverseFlow
///   bool   nearTranslation  ← true → hide direction arrow
///   bool   nearRotX         ← true → show full circle on X ring
///   bool   nearRotY         ← true → show full circle on Y ring
///   bool   nearRotZ         ← true → show full circle on Z ring
/// </summary>
public class TrackingVisualsSync : MonoBehaviourPun, IPunObservable
{
    // ── Constants ─────────────────────────────────────────────────────────
    internal const int    CUBE_COUNT      = 7;
    private  const float  SEARCH_INTERVAL = 0.5f;
    private  const float  SEARCH_TIMEOUT  = 20f;
    private  const string ROOM_ANCHOR_NAME = "ScannedRoom";

    // ── State ─────────────────────────────────────────────────────────────
    private ObjectTrackingStatus[] _trackers;
    private bool  _searchDone;
    private float _searchTimer;
    private float _searchNextTime;
    private Transform _roomAnchor;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        var go = GameObject.Find(ROOM_ANCHOR_NAME);
        if (go != null)
            _roomAnchor = go.transform;
        else
            Debug.LogWarning("[TrackingVisualsSync] Room anchor not found; " +
                             "falling back to world coordinates.");
    }

    void Update()
    {
        // Throttled search: ObjectTrackingStatus instances appear after MRUK loads
        if (!_searchDone && Time.time >= _searchNextTime)
        {
            _searchNextTime = Time.time + SEARCH_INTERVAL;
            _searchTimer   += SEARCH_INTERVAL;

            var found = FindObjectsOfType<ObjectTrackingStatus>();
            if (found != null && found.Length > 0)
            {
                // Sort by name so order matches TrackingVisualsReceiver on the remote device
                System.Array.Sort(found,
                    (a, b) => string.Compare(a.gameObject.name, b.gameObject.name,
                                             System.StringComparison.Ordinal));
                _trackers   = found;
                _searchDone = true;

                // Take ownership so we can write the stream
                if (!photonView.IsMine)
                    photonView.RequestOwnership();

                Debug.Log($"[TrackingVisualsSync] Found {_trackers.Length} trackers; " +
                          (photonView.IsMine ? "already owner." : "requesting ownership."));
            }
            else if (_searchTimer >= SEARCH_TIMEOUT)
            {
                _searchDone = true;
                Debug.Log("[TrackingVisualsSync] Search timed out with no trackers.");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
            WriteState(stream);
        else
            SkipRead(stream); // local side doesn't consume remote data
    }

    // ── Write 7 cubes' state ──────────────────────────────────────────────
    private void WriteState(PhotonStream stream)
    {
        for (int i = 0; i < CUBE_COUNT; i++)
        {
            var  t   = (_trackers != null && i < _trackers.Length) ? _trackers[i] : null;
            bool vis = t != null && t.IsTracked; // wireframe visible ⟺ tracking active

            stream.SendNext(vis);
            stream.SendNext(vis ? ToLocal(t.transform.position)    : Vector3.zero);
            stream.SendNext(vis ? ToLocalRot(t.transform.rotation) : Quaternion.identity);
            stream.SendNext(vis ? ToLocal(t.ArrowStartWorld)       : Vector3.zero);
            stream.SendNext(vis ? ToLocal(t.ArrowEndWorld)         : Vector3.zero);
            stream.SendNext(t != null ? t.ArcSpanX                 : 0f);
            stream.SendNext(t != null ? t.ArcSpanY                 : 0f);
            stream.SendNext(t != null ? t.ArcSpanZ                 : 0f);
            stream.SendNext(t != null && t.ReverseFlow);
            stream.SendNext(t != null && t.IsNearTranslation);
            stream.SendNext(t != null && t.IsNearRotX);
            stream.SendNext(t != null && t.IsNearRotY);
            stream.SendNext(t != null && t.IsNearRotZ);
        }
    }

    // Keep receiver side's stream valid if ownership ever flips unexpectedly
    private void SkipRead(PhotonStream stream)
    {
        for (int i = 0; i < CUBE_COUNT; i++)
        {
            stream.ReceiveNext(); // vis
            stream.ReceiveNext(); // cubePos
            stream.ReceiveNext(); // cubeRot
            stream.ReceiveNext(); // arrowStart
            stream.ReceiveNext(); // arrowEnd
            stream.ReceiveNext(); // arcX
            stream.ReceiveNext(); // arcY
            stream.ReceiveNext(); // arcZ
            stream.ReceiveNext(); // reverseFlow
            stream.ReceiveNext(); // nearTranslation
            stream.ReceiveNext(); // nearRotX
            stream.ReceiveNext(); // nearRotY
            stream.ReceiveNext(); // nearRotZ
        }
    }

    // ── Coordinate helpers ────────────────────────────────────────────────
    private Vector3    ToLocal(Vector3 world) =>
        _roomAnchor != null ? _roomAnchor.InverseTransformPoint(world)              : world;
    private Quaternion ToLocalRot(Quaternion worldRot) =>
        _roomAnchor != null ? Quaternion.Inverse(_roomAnchor.rotation) * worldRot   : worldRot;
}
