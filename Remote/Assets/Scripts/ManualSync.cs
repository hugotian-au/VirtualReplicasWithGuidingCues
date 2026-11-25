using System.Collections;
using UnityEngine;
using Photon.Pun;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Syncs soma-cube position/rotation over Photon using ScannedRoom-relative
/// coordinates — the same convention as RemotePresenceSync.
///
/// Coordinate decoding is gated behind _anchorReady, which is set only after
/// MRUKRoomAligner has finished aligning ScannedRoom:
///   Frame N  : MRUK fires SceneLoadedEvent
///              → MRUKRoomAligner sets _sceneLoaded = true
///              → ManualSync.OnSceneLoaded starts coroutine, yields one frame
///   Frame N  : LateUpdate → MRUKRoomAligner aligns ScannedRoom
///   Frame N+1: coroutine resumes → _roomAnchor fetched (now aligned) → _anchorReady = true
///   Frame N+1 and onwards: Update() decodes positions with the correctly aligned anchor.
///
/// isKinematic is NEVER set to true for non-owners: Meta ISDK permanently
/// excludes kinematic objects from HandGrabInteractable candidate lists at
/// initialisation time; once excluded they cannot be re-added.
/// useGravity=false keeps the cube in place between network updates.
/// </summary>
public class ManualSync : MonoBehaviourPun, IPunObservable
{
    private Rigidbody _rb;
    private bool      _wasMine;
    private Transform _roomAnchor;
    private bool      _collisionDisabled;

    // Latest received state (ScannedRoom-anchor-relative)
    private Vector3    _receivedAnchorPos = Vector3.zero;
    private Quaternion _receivedAnchorRot = Quaternion.identity;
    private bool       _hasReceivedData;

    // True only after ScannedRoom has been aligned by MRUKRoomAligner
    private bool _anchorReady;
    private bool _alignCoroutineStarted;

    private const string RoomAnchorName = "ScannedRoom";

    void Start()
    {
        _rb      = GetComponent<Rigidbody>();
        _wasMine = photonView.IsMine;

        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);

            // Guard: if SceneLoadedEvent already fired before this Start() ran
            // (happens on rapid restarts), we would never receive the event.
            // Check directly and kick off the coroutine immediately if so.
            if (MRUK.Instance.IsInitialized && MRUK.Instance.GetCurrentRoom() != null)
                OnSceneLoaded();
        }
        else
            Debug.LogWarning("[ManualSync] MRUK.Instance not found.");

        ApplyOwnershipPhysics();
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
    }

    /// <summary>
    /// Called when MRUK finishes loading the room scene.
    /// MRUKRoomAligner aligns ScannedRoom in LateUpdate of *this same frame*.
    /// We yield one frame so that LateUpdate runs first, then fetch the
    /// already-aligned transform and enable position decoding.
    /// </summary>
    private void OnSceneLoaded()
    {
        if (_alignCoroutineStarted) return;   // prevent double-start if both paths fire
        _alignCoroutineStarted = true;
        StartCoroutine(EnableAfterAlignment());
    }

    private IEnumerator EnableAfterAlignment()
    {
        // Wait until MRUKRoomAligner has ACTUALLY finished aligning ScannedRoom.
        // ExecuteAlignmentRoutine() returns false when MRUK data is incomplete
        // (no bounds / floor / wall yet), so alignment may take several frames.
        // A fixed "yield return null" is not enough — we must poll IsAligned.
        MRUKRoomAligner aligner = FindObjectOfType<MRUKRoomAligner>();
        if (aligner != null)
        {
            while (!aligner.IsAligned)
                yield return null;
        }
        else
        {
            // No aligner in scene — wait one frame as a minimal safety gap
            yield return null;
        }

        var go = GameObject.Find(RoomAnchorName);
        if (go != null)
        {
            _roomAnchor = go.transform;
            Debug.Log($"[ManualSync] ScannedRoom aligned: worldPos={_roomAnchor.position}, " +
                      $"rot={_roomAnchor.rotation.eulerAngles}");
        }
        else
        {
            Debug.LogWarning("[ManualSync] ScannedRoom not found after alignment.");
        }

        _anchorReady = true;
    }

    void Update()
    {
        // React to ownership changes (e.g. grab transfer)
        if (photonView.IsMine != _wasMine)
        {
            _wasMine = photonView.IsMine;
            ApplyOwnershipPhysics();
        }

        // Do not decode positions until ScannedRoom has been aligned
        if (!_anchorReady || !_hasReceivedData || photonView.IsMine) return;

        Vector3    worldPos = _roomAnchor != null
            ? _roomAnchor.TransformPoint(_receivedAnchorPos)
            : _receivedAnchorPos;
        Quaternion worldRot = _roomAnchor != null
            ? _roomAnchor.rotation * _receivedAnchorRot
            : _receivedAnchorRot;

        if (_rb != null)
        {
            _rb.position = worldPos;
            _rb.rotation = worldRot;
        }
        else
        {
            transform.position = worldPos;
            transform.rotation = worldRot;
        }
    }

    private void ApplyOwnershipPhysics()
    {
        if (_rb == null) return;

        if (photonView.IsMine)
        {
            // When ownership arrives (e.g. returned after remote releases),
            // warp the Rigidbody to the last received network position so it
            // starts physics simulation from the correct place, not from the
            // stale pre-grab position that was frozen while we were non-owner.
            if (_hasReceivedData && _anchorReady)
            {
                Vector3 worldPos = _roomAnchor != null
                    ? _roomAnchor.TransformPoint(_receivedAnchorPos)
                    : _receivedAnchorPos;
                Quaternion worldRot = _roomAnchor != null
                    ? _roomAnchor.rotation * _receivedAnchorRot
                    : _receivedAnchorRot;

                _rb.position = worldPos;
                _rb.rotation = worldRot;
            }

            // Owner: no gravity — Remote has no physical floor at the right height,
            // so gravity would pull the cube back to table level after every release.
            _rb.isKinematic    = false;
            _rb.useGravity     = false;
            _rb.linearDamping  = 10f;
            _rb.angularDamping = 10f;

            // Disable collision with all other ManualSync objects while held —
            // prevents the grabbed cube from pushing or being deflected by them.
            SetCollisionWithOthers(false);
            _collisionDisabled = true;
        }
        else
        {
            // Non-owner: kinematic so network position is applied exactly
            // with no physics forces. No repulsion, no drift.
            _rb.isKinematic    = true;
            _rb.useGravity     = false;

            // Restore collision when ownership is released.
            SetCollisionWithOthers(true);
            _collisionDisabled = false;
        }
    }

    /// <summary>
    /// Toggles Physics.IgnoreCollision between this object's colliders and
    /// every other ManualSync object's colliders in the scene.
    /// </summary>
    private void SetCollisionWithOthers(bool enable)
    {
        var myColliders = GetComponentsInChildren<Collider>(true);
        if (myColliders.Length == 0) return;

        foreach (var other in FindObjectsOfType<ManualSync>())
        {
            if (other == this) continue;
            foreach (var myCol in myColliders)
                foreach (var otherCol in other.GetComponentsInChildren<Collider>(true))
                    Physics.IgnoreCollision(myCol, otherCol, !enable);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Encode as ScannedRoom-relative (mirrors RemotePresenceSync sender)
            Vector3    pos = _roomAnchor != null
                ? _roomAnchor.InverseTransformPoint(transform.position)
                : transform.position;
            Quaternion rot = _roomAnchor != null
                ? Quaternion.Inverse(_roomAnchor.rotation) * transform.rotation
                : transform.rotation;

            stream.SendNext(pos);
            stream.SendNext(rot);
        }
        else
        {
            _receivedAnchorPos = (Vector3)   stream.ReceiveNext();
            _receivedAnchorRot = (Quaternion)stream.ReceiveNext();
            _hasReceivedData   = true;
        }
    }
}
