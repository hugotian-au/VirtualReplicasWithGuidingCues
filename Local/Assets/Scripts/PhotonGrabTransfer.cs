using Photon.Pun;
using UnityEngine;
using Oculus.Interaction;

public class PhotonGrabTransfer : MonoBehaviourPun
{
    private Grabbable _grabbable;

    void Awake()   => _grabbable = GetComponent<Grabbable>();
    void OnEnable()  => _grabbable.WhenPointerEventRaised += HandlePointerEvent;
    void OnDisable() => _grabbable.WhenPointerEventRaised -= HandlePointerEvent;

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select && !photonView.IsMine)
            photonView.RequestOwnership();
    }
}
