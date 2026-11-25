using UnityEngine;
using System.Collections.Generic;

namespace Com.IVE.ECL.RemoteCollaboration
{

    public class MyContollerMgr : MonoBehaviour
    {
        public stateManager mStateManager;
        public GameObject controllerColTarget;
        private Vector3 controllerPosition;
        private Quaternion controllerRotation;

        private Transform attachedObject;
        private string attachedObjectName = "";

        private Dictionary<string, float> detachTimestamps = new Dictionary<string, float>();
        private float cooldownTime = 3.0f;

        [SerializeField] private GameObject[] taskObjs;
        [SerializeField] private GameObject targetObj;

        public void HandleCollision(GameObject obj)
        {
            Debug.Log($"Object handle collision: {obj.name}");
            AttachObject(obj);
        }

        private void AttachObject(GameObject obj)
        {
            string objName = obj.name;
            if (detachTimestamps.ContainsKey(objName) && (Time.time - detachTimestamps[objName]) < cooldownTime)
            {
                Debug.Log($"{objName} cannot be attached (cool time)");
                return;
            }


            if (attachedObject != null)
            {
                Debug.Log("Object already attached");
                return;
            }

            attachedObject = obj.transform;
            attachedObject.GetComponent<ObjectTrackingStatus>().SetTrackingStatus("tracked");
            // mStateManager.SendTrackingStateToNetworkCall(obj.transform.GetChild(0).name);


            attachedObject.SetParent(targetObj.transform);
            obj.transform.localPosition = new Vector3(0, 0, 0);

            Quaternion plusRotation = Quaternion.Euler(new Vector3(-90, 90, 0));
            obj.transform.localRotation = plusRotation;

            // Debug.Log($"{attachedObjectName} follow the controller");
        }

        private void DetachObject()
        {
            if (attachedObject == null)
            {
                Debug.Log("No Object attached");
                return;
            }

            Vector3 attachedPosition;
            attachedPosition = attachedObject.transform.position;

            string detachedObjectName = attachedObject.name;

            attachedObject.SetParent(null);
            attachedObject.GetComponent<ObjectTrackingStatus>().SetTrackingStatus("idle");
            // for now mStateManager.SendTrackingStateToNetworkCall("idle");
            //Rigidbody rb = attachedObject.GetComponent<Rigidbody>();
            //if (rb != null)
            //{
            //    rb.isKinematic = false;
            //}

            // Debug.Log($"{attachedObjectName} is detached.");
            // objectStates[attachedObjectName] = false;
            attachedObjectName = "";
            attachedObject = null;


            // Detach
            detachTimestamps[detachedObjectName] = Time.time;

            // if(detectEvent(attachedPosition))
            {
                // mStateManager.SendBookmarkNetworkNetworkCall(detachedObjectName);
            }
            attachedPosition = Vector3.zero;



        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            // 1. INSTANT PRESS (GetDown)
            // Returns true ONLY on the specific frame the button was pressed.
            // Use for: Jumping, Shooting, UI Clicks.
            // ---------------------------------------------------------

            // Check "A" Button
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                Debug.Log("Right A Button Pressed!");

                DetachObject();
            }

            // Check "B" Button
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            {
                Debug.Log("Right B Button Pressed!");
                DetachObject();
            }

            // ---------------------------------------------------------
            // 2. CONTINUOUS HOLD (Get)
            // Returns true every frame the button is held down.
            // Use for: Charging an attack, Sprinting, Dragging objects.
            // ---------------------------------------------------------

            // Check Front Trigger (Index Finger)
            // Note: The trigger acts as a button here (sensitive to > 50% press)
            if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                Debug.Log("Right Trigger is being held...");
                DetachObject();
            }

            // ---------------------------------------------------------
            // 3. ANALOG VALUES (0.0 to 1.0)
            // Use for: Throttle, delicate gripping.
            // ---------------------------------------------------------

            // Get how much the Grip (Side Trigger) is squeezed
            float gripValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);

            if (gripValue > 0.1f)
            {
                // Do something based on pressure
            }
        }
    }
}
