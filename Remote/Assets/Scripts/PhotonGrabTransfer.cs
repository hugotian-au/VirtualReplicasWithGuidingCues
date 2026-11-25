using Photon.Pun;
using UnityEngine;
using Oculus.Interaction;

public class PhotonGrabTransfer : MonoBehaviourPun
{
    private Grabbable    _grabbable;
    private Collider[]   _colliders;

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()  => _grabbable.WhenPointerEventRaised += HandlePointerEvent;
    void OnDisable() => _grabbable.WhenPointerEventRaised -= HandlePointerEvent;

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                if (!photonView.IsMine)
                    photonView.RequestOwnership();
                // Disable colliders so the grabbed cube can't push other cubes
                // while being moved. ISDK doesn't need colliders to control the object.
                SetCollidersEnabled(false);
                break;

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                // Re-enable colliders once the cube is placed.
                SetCollidersEnabled(true);
                break;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (var col in _colliders)
            if (col != null) col.enabled = enabled;
    }
}
