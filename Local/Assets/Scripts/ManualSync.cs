using UnityEngine;
using Photon.Pun;

/// <summary>
/// Syncs soma-cube position/rotation over Photon using ScannedRoom-relative
/// coordinates — the same convention as RemotePresenceSync — so the objects
/// appear at the correct physical location on both devices regardless of how
/// their parent hierarchy is positioned in Unity world space.
///
/// Stream format (unchanged): Vector3 anchorPos, Quaternion anchorRot
/// </summary>
public class ManualSync : MonoBehaviourPun, IPunObservable
{
    private Rigidbody _rb;
    private bool      _wasMine;
    private bool      _firstSyncDone;
    private Transform _roomAnchor;

    private const string RoomAnchorName = "ScannedRoom";

    void Start()
    {
        _rb      = GetComponent<Rigidbody>();
        _wasMine = photonView.IsMine;

        var go = GameObject.Find(RoomAnchorName);
        if (go != null) _roomAnchor = go.transform;
        else Debug.LogWarning("[ManualSync] ScannedRoom anchor not found.");

        ApplyOwnershipPhysics();
    }

    void Update()
    {
        if (photonView.IsMine == _wasMine) return;
        _wasMine = photonView.IsMine;
        ApplyOwnershipPhysics();
    }

    private void ApplyOwnershipPhysics()
    {
        if (_rb == null) return;

        if (photonView.IsMine)
        {
            _rb.isKinematic    = false;
            _rb.useGravity     = true;
            _rb.linearDamping  = 1f;
            _rb.angularDamping = 0.5f;
        }
        else
        {
            _firstSyncDone     = false;
            _rb.isKinematic    = true;
            _rb.useGravity     = false;
            _rb.linearDamping  = 10f;
            _rb.angularDamping = 30f;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
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
            var anchorPos = (Vector3)   stream.ReceiveNext();
            var anchorRot = (Quaternion)stream.ReceiveNext();

            Vector3    worldPos = _roomAnchor != null
                ? _roomAnchor.TransformPoint(anchorPos)
                : anchorPos;
            Quaternion worldRot = _roomAnchor != null
                ? _roomAnchor.rotation * anchorRot
                : anchorRot;

            transform.position = worldPos;
            transform.rotation = worldRot;

            if (!_firstSyncDone && _rb != null)
            {
                _firstSyncDone  = true;
                _rb.isKinematic = false;
            }
        }
    }
}
