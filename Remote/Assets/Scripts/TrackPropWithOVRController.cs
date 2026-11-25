using UnityEngine;

namespace Com.IVE.ECL.RemoteCollaboration
{

    public class TrackPropWithOVRController : MonoBehaviour
    {
        [Header("Assign the controller anchor (e.g., RightControllerAnchor)")]
        public Transform controllerAnchor;

        [Header("Mount offset from controller to prop (local to controller)")]
        public Vector3 localPositionOffset; // meters
        public Vector3 localEulerOffset;    // degrees

        Transform _offset;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _offset = new GameObject("ControllerToPropOffset").transform;
            _offset.SetParent(controllerAnchor, false);
            _offset.localPosition = localPositionOffset;
            _offset.localRotation = Quaternion.Euler(localEulerOffset);
        }

        void LateUpdate()
        {
            // Prop follows controller * offset
            transform.position = _offset.position;
            transform.rotation = _offset.rotation;
        }
    }

}
