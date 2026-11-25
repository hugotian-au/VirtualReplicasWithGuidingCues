using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Com.IVE.ECL.RemoteCollaboration
{

    public class TargetCollisionHandler : MonoBehaviour
    {

        [SerializeField] private MyContollerMgr attachDetachController; // AttachDetachController 

        private void OnTriggerEnter(Collider other)
        {
            HandleAttachable(other);
        }

        /// <summary>
        /// Handles the case where the controller never left the trigger zone after
        /// stopping tracking — OnTriggerEnter won't re-fire, so we poll via Stay.
        /// AttachObject's cooldown (3 s) and null-check prevent double-attaching.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            HandleAttachable(other);
        }

        private void HandleAttachable(Collider other)
        {
            if (!other.gameObject.CompareTag("Attachable")) return;

            // The Collider's parent visual may have been hidden (renderers off) but
            // the GameObject stays active — SetActive(true) is a safe no-op here.
            other.gameObject.transform.parent.gameObject.SetActive(true);

            Transform root = other.gameObject.transform.parent?.parent?.parent;
            if (root != null)
            {
                attachDetachController.HandleCollision(root.gameObject);
            }
            else
            {
                Debug.LogWarning("[TargetCollisionHandler]: Not enough parent levels on " + other.name);
            }
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
