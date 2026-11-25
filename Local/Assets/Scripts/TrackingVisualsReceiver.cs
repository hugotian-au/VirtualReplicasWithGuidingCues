using UnityEngine;
using Photon.Pun;
using Com.IVE.ECL.RemoteCollaboration;

/// <summary>
/// REMOTE-SIDE ONLY — mirrors wireframe visibility from the local device.
///
/// Reads the same Photon stream written by TrackingVisualsSync and calls
/// ObjectTrackingStatus.SetWireframeVisible() on each cube so the remote
/// device shows exactly the same wireframe on/off state as the local device.
///
/// ── Scene Setup (REMOTE scene only) ──────────────────────────────────────
///   1. Create an empty GameObject named "TrackingVisualsMgr".
///   2. Add a PhotonView: ViewID = 999, OwnershipTransfer = Takeover,
///      Synchronization = Unreliable On Change.
///   3. Add this TrackingVisualsReceiver component.
///   4. Drag it into the PhotonView's ObservedComponents list.
/// </summary>
public class TrackingVisualsReceiver : MonoBehaviourPun, IPunObservable
{
    private const int   CUBE_COUNT      = TrackingVisualsSync.CUBE_COUNT;
    private const float SEARCH_INTERVAL = 0.5f;
    private const float SEARCH_TIMEOUT  = 20f;

    private ObjectTrackingStatus[] _trackers;
    private bool  _searchDone;
    private float _searchTimer;
    private float _searchNextTime;

    // One visibility flag per cube — updated in OnPhotonSerializeView, applied in Update
    private readonly bool[] _vis = new bool[CUBE_COUNT];

    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        // Throttled search: trackers may appear after the scene finishes loading
        if (!_searchDone && Time.time >= _searchNextTime)
        {
            _searchNextTime = Time.time + SEARCH_INTERVAL;
            _searchTimer   += SEARCH_INTERVAL;

            var found = FindObjectsOfType<ObjectTrackingStatus>();
            if (found != null && found.Length > 0)
            {
                // Same sort as TrackingVisualsSync so indices line up
                System.Array.Sort(found,
                    (a, b) => string.Compare(a.gameObject.name, b.gameObject.name,
                                             System.StringComparison.Ordinal));
                _trackers   = found;
                _searchDone = true;
                Debug.Log($"[TrackingVisualsReceiver] Found {_trackers.Length} trackers.");
            }
            else if (_searchTimer >= SEARCH_TIMEOUT)
            {
                _searchDone = true;
                Debug.Log("[TrackingVisualsReceiver] Search timed out; no trackers found.");
            }
        }

        if (_trackers == null) return;

        int count = Mathf.Min(_trackers.Length, CUBE_COUNT);
        for (int i = 0; i < count; i++)
        {
            if (_trackers[i] != null)
                _trackers[i].SetWireframeVisible(_vis[i]);
        }
    }

    // ── IPunObservable ────────────────────────────────────────────────────
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Remote device should never own this view.
            // Write zeros to keep the stream length valid if ownership unexpectedly flips.
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
        else
        {
            for (int i = 0; i < CUBE_COUNT; i++)
            {
                _vis[i] = (bool)stream.ReceiveNext(); // ← the only field we use
                // Consume the remaining 12 fields to keep the stream in sync
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
    }
}
