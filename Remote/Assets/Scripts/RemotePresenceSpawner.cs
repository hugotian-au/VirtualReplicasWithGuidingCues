using UnityEngine;
using Photon.Pun;

namespace Com.IVE.ECL.RemoteCollaboration
{
    /// <summary>
    /// Attach this to CenterEyeAnchor.
    /// - headTransform  = CenterEyeAnchor itself (this.transform)
    /// - eyeGazeRay     = found automatically from the child EyeGazeRay component
    /// Spawns "RemotePresence" prefab (Resources/) when joining a Photon room.
    /// </summary>
    public class RemotePresenceSpawner : MonoBehaviourPunCallbacks
    {
        [Tooltip("Must match the prefab name under Resources/")]
        public string presencePrefabName = "RemotePresence";

        private EyeGazeRay _eyeGazeRay;
        private RemotePresenceSync _presence;

        void Awake()
        {
            // CenterEyeAnchor is this GameObject; EyeGazeRay is its child
            _eyeGazeRay = GetComponentInChildren<EyeGazeRay>();
            if (_eyeGazeRay == null)
                Debug.LogError("[RemotePresenceSpawner] EyeGazeRay not found under CenterEyeAnchor.");
        }

        void Start()
        {
            // If already in a room when this script starts (scene loaded after joining), spawn immediately
            if (PhotonNetwork.InRoom)
                OnJoinedRoom();
        }

        public override void OnJoinedRoom()
        {
            if (_presence != null) return;

            GameObject obj = PhotonNetwork.Instantiate(presencePrefabName, transform.position, Quaternion.identity);

            _presence = obj.GetComponent<RemotePresenceSync>();
            if (_presence != null)
            {
                _presence.headTransform = transform;   // CenterEyeAnchor
                _presence.eyeGazeRay    = _eyeGazeRay;
            }
            else
            {
                Debug.LogError($"[RemotePresenceSpawner] Prefab '{presencePrefabName}' is missing RemotePresenceSync component.");
            }
        }

        public override void OnLeftRoom()
        {
            _presence = null;
        }
    }
}
