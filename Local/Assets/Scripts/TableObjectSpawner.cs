using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using System.Collections; 

namespace Com.IVE.ECL.RemoteCollaboration
{

	public class TableObjectSpawner : MonoBehaviourPunCallbacks
	{
		[Header("Settings")]
		[Tooltip("The object you want to place.")]
		public GameObject somacubeAToSpawn;
		public GameObject somacubeA_invisbleToSpawn;
		public GameObject somacubeA_OnlyCube;
		public GameObject somacubeBToSpawn;
		public GameObject somacubeB_invisbleToSpawn;
		public GameObject somacubeB_OnlyCube;
		public GameObject somacubeLToSpawn;
		public GameObject somacubeL_invisbleToSpawn;
		public GameObject somacubeL_OnlyCube;
		public GameObject somacubePToSpawn;
		public GameObject somacubeP_invisbleToSpawn;
		public GameObject somacubeP_OnlyCube;
		public GameObject somacubeTToSpawn;
		public GameObject somacubeT_invisbleToSpawn;
		public GameObject somacubeT_OnlyCube;
		public GameObject somacubeVToSpawn;
		public GameObject somacubeV_invisbleToSpawn;
		public GameObject somacubeV_OnlyCube;
		public GameObject somacubeZToSpawn;
		public GameObject somacubeZ_invisbleToSpawn;
		public GameObject somacubeZ_OnlyCube;

		[Tooltip("The table used to place Objects.")]
		public GameObject TablePlace;

        [Tooltip("The pivot parents.")]
        public GameObject pivotCubeA;
        public GameObject pivotCubeB;
        public GameObject pivotCubeL;
        public GameObject pivotCubeP;
        public GameObject pivotCubeT;
        public GameObject pivotCubeV;
        public GameObject pivotCubeZ;

		[Tooltip("Offset from the center of the table (in meters). X = Right/Left, Z = Forward/Back")]
		public Vector2 positionOffset = new Vector2(0.0f, 0.0f);

		[Tooltip("If true, this adds the object's rotation. If false, it stays world-aligned.")]
		public bool alignWithTableRotation = true;

		void Start()
		{
			if (MRUK.Instance == null)
			{
				Debug.LogError("MRUK Instance not found.");
				return;
			}

			MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
		}

		private void OnDestroy()
		{
			if (MRUK.Instance != null)
			{
				MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
			}
		}

		private void OnSceneLoaded()
		{
			MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();

			if (currentRoom == null) return;

			SpawnOnTable(TablePlace);

			//int i = 0;
			//foreach (var anchor in currentRoom.Anchors)
			//{
				
			//	if (anchor.Label == MRUKAnchor.SceneLabels.TABLE)
			//	{
			//		i++; 
			//		Debug.Log($"Found TABLE anchor: {anchor.Label}, Total TABLE anchors found so far: {i}");
			//		if (i == 2)
			//		{
			//			Bounds bounds = anchor.VolumeBounds.Value;
			//			if (bounds != null)
			//			{
			//				// Get the size (Width, Height, Depth) in meters
			//				// Vector3 size = bounds.size;
			//				// if (size.x > 2.5f)
			//				{
			//					SpawnOnTable(anchor);
			//				}
			//			}
			//			break;
			//		}
					
			//	}
			//}
		}

		private void SpawnOnTable(GameObject tablePlace)
		{
			if(tablePlace == null) return;
			Transform anchor = tablePlace.transform;

            Quaternion spawnRotation = Quaternion.identity;
            if (alignWithTableRotation)
            {
                spawnRotation = anchor.rotation;
            }

            Vector3 spawnPosition = anchor.position;

            // A
            SpawnPieceGroup(pivotCubeA, anchor, spawnRotation, spawnPosition, new Vector3(0.074f, 7f, -0.007f),
                somacubeAToSpawn,
                somacubeA_invisbleToSpawn,
                somacubeA_OnlyCube);

            // B
            SpawnPieceGroup(pivotCubeB, anchor, spawnRotation, spawnPosition, new Vector3(0.074f, 7f, -0.255f),
                somacubeBToSpawn,
                somacubeB_invisbleToSpawn,
                somacubeB_OnlyCube);

            // Z
            SpawnPieceGroup(pivotCubeZ, anchor, spawnRotation, spawnPosition, new Vector3(0.074f, 7f, -0.4f),
                somacubeZToSpawn,
                somacubeZ_invisbleToSpawn,
                somacubeZ_OnlyCube);

            // L
            SpawnPieceGroup(pivotCubeL, anchor, spawnRotation, spawnPosition, new Vector3(0.5f, 1.8f, -0.055f),
                somacubeLToSpawn,
                somacubeL_invisbleToSpawn,
                somacubeL_OnlyCube);

            // P
            SpawnPieceGroup(pivotCubeP, anchor, spawnRotation, spawnPosition, new Vector3(0.5f, 1.8f, -0.18f),
                somacubePToSpawn,
                somacubeP_invisbleToSpawn,
                somacubeP_OnlyCube);

            // T
            SpawnPieceGroup(pivotCubeT, anchor, spawnRotation, spawnPosition, new Vector3(0.5f, 1.8f, -0.35f),
                somacubeTToSpawn,
                somacubeT_invisbleToSpawn,
                somacubeT_OnlyCube);

            // V
            SpawnPieceGroup(pivotCubeV, anchor, spawnRotation, spawnPosition, new Vector3(0.5f, 1.8f, -0.47f),
                somacubeVToSpawn,
                somacubeV_invisbleToSpawn,
                somacubeV_OnlyCube);

        }

        // 新增方法，多个prefab共享同一个Pivot
        private void SpawnPieceGroup(GameObject parent, Transform anchor, Quaternion rotation, Vector3 worldPos, Vector3 localPos, params GameObject[] prefabs)
        {
            Quaternion pieceRotation = Quaternion.Euler(0, -90f, 0f);

            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];
                GameObject obj;

                if (i == 0 || i == 1)
                {
                    // 只有第一个（somacubeX_ToSpawn）使用 Photon 网络实例化
                    obj = PhotonNetwork.Instantiate(prefab.name, worldPos, rotation);

					obj.transform.SetParent(parent.transform, false);
					obj.name = prefab.name;
					obj.transform.localPosition = Vector3.zero;
					obj.transform.localRotation = pieceRotation;
					obj.transform.localScale = Vector3.one;
				}
                else
                {
                    // 其余两个本地实例化
                    // obj = Instantiate(prefab, worldPos, rotation);
                }


            }
        }

        private void SpawnPiece(GameObject prefab, Transform anchor, Quaternion rotation, Vector3 worldPos, Vector3 localPos)
        {
            // 创建一个中间空物体，挂在桌子下，负责抵消 scale
            GameObject pivot = new GameObject("Pivot_" + prefab.name);
            pivot.transform.SetParent(anchor, false);
            pivot.transform.localPosition = localPos;
            pivot.transform.localRotation = Quaternion.identity;

            // 抵消父级 scale
            Vector3 ps = anchor.lossyScale;
            pivot.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);

            Quaternion pieceRotation = Quaternion.Euler(0, -90f, 0f);


            // 物体挂在 pivot 下，此时父级 scale 已是 (1,1,1)，旋转不再产生形变
            GameObject obj = PhotonNetwork.Instantiate(prefab.name, worldPos, rotation);
            obj.transform.SetParent(pivot.transform, false);
            obj.name = prefab.name;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = pieceRotation;
            obj.transform.localScale = Vector3.one;
        }

        //      private void SpawnOnTable(MRUKAnchor anchor)
        //{
        //	// FIX 1: Check if the anchor actually has a volume (some might be planes)
        //	if (!anchor.VolumeBounds.HasValue)
        //	{
        //		return;
        //	}

        //	// FIX 2: Use .Value to access the underlying Bounds struct
        //	float surfaceHeight = anchor.VolumeBounds.Value.extents.y;

        //	// Calculate Local Position
        //	Vector3 localPosition = new Vector3(positionOffset.x, surfaceHeight, positionOffset.y);

        //	// Convert to World Position
        //	Vector3 spawnPosition = anchor.transform.TransformPoint(localPosition);

        //	// Handle Rotation
        //	Quaternion spawnRotation = Quaternion.identity;
        //	if (alignWithTableRotation)
        //	{
        //		spawnRotation = anchor.transform.rotation;
        //	}

        //	// Spawn Object A
        //	// GameObject spawnedObj = Instantiate(somacubeAToSpawn, spawnPosition, spawnRotation); //Instantiate("SomaCubeATOri_Both", spawnPosition, spawnRotation, 0);
        //	GameObject spawnedObjA = PhotonNetwork.Instantiate(somacubeAToSpawn.name, spawnPosition, spawnRotation);
        //	spawnedObjA.transform.SetParent(anchor.transform, false);
        //	spawnedObjA.name = somacubeAToSpawn.name;

        //	spawnedObjA.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //	Quaternion plusRotationA = Quaternion.Euler(new Vector3(-90, 0, 180));
        //	spawnedObjA.transform.localRotation = plusRotationA;

        //	// GameObject spawnedObj_hl = Instantiate(somacubeA_invisbleToSpawn, spawnPosition, spawnRotation);
        //	GameObject spawnedObjA_hl = PhotonNetwork.Instantiate(somacubeA_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //	spawnedObjA_hl.transform.SetParent(anchor.transform, false);
        //	spawnedObjA_hl.name = somacubeA_invisbleToSpawn.name;

        //	spawnedObjA_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //	spawnedObjA_hl.transform.localRotation = plusRotationA;

        //	GameObject spawnedObjA_OnlyCube = PhotonNetwork.Instantiate(somacubeA_OnlyCube.name, spawnPosition, spawnRotation);
        //	spawnedObjA_OnlyCube.transform.SetParent(anchor.transform, false);
        //	spawnedObjA_OnlyCube.name = somacubeA_OnlyCube.name;

        //          spawnedObjA_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjA_OnlyCube.transform.localRotation = plusRotationA;


        //          // Spawn Object B
        //          // GameObject spawnedObj = Instantiate(somacubeAToSpawn, spawnPosition, spawnRotation); //Instantiate("SomaCubeATOri_Both", spawnPosition, spawnRotation, 0);
        //          GameObject spawnedObjB = PhotonNetwork.Instantiate(somacubeBToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjB.transform.SetParent(anchor.transform, false);
        //          spawnedObjB.name = somacubeAToSpawn.name;

        //          spawnedObjB.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationB = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjB.transform.localRotation = plusRotationB;

        //          // GameObject spawnedObj_hl = Instantiate(somacubeA_invisbleToSpawn, spawnPosition, spawnRotation);
        //          GameObject spawnedObjB_hl = PhotonNetwork.Instantiate(somacubeB_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjB_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjB_hl.name = somacubeB_invisbleToSpawn.name;

        //          spawnedObjB_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjB_hl.transform.localRotation = plusRotationB;

        //          GameObject spawnedObjB_OnlyCube = PhotonNetwork.Instantiate(somacubeB_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjB_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjB_OnlyCube.name = somacubeB_OnlyCube.name;

        //          spawnedObjB_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjB_OnlyCube.transform.localRotation = plusRotationB;

        //          // Spawn Object L
        //          // GameObject spawnedObj = Instantiate(somacubeAToSpawn, spawnPosition, spawnRotation); //Instantiate("SomaCubeATOri_Both", spawnPosition, spawnRotation, 0);
        //          GameObject spawnedObjL = PhotonNetwork.Instantiate(somacubeLToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjL.transform.SetParent(anchor.transform, false);
        //          spawnedObjL.name = somacubeAToSpawn.name;

        //          spawnedObjL.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationL = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjL.transform.localRotation = plusRotationL;

        //          // GameObject spawnedObj_hl = Instantiate(somacubeA_invisbleToSpawn, spawnPosition, spawnRotation);
        //          GameObject spawnedObjL_hl = PhotonNetwork.Instantiate(somacubeL_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjL_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjL_hl.name = somacubeB_invisbleToSpawn.name;

        //          spawnedObjL_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjL_hl.transform.localRotation = plusRotationL;

        //          GameObject spawnedObjL_OnlyCube = PhotonNetwork.Instantiate(somacubeL_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjL_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjL_OnlyCube.name = somacubeL_OnlyCube.name;

        //          spawnedObjL_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjL_OnlyCube.transform.localRotation = plusRotationL;

        //          //Spawn Object P
        //          GameObject spawnedObjP = PhotonNetwork.Instantiate(somacubePToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjP.transform.SetParent(anchor.transform, false);
        //          spawnedObjP.name = somacubePToSpawn.name;
        //          spawnedObjP.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationP = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjP.transform.localRotation = plusRotationP;

        //          GameObject spawnedObjP_hl = PhotonNetwork.Instantiate(somacubeP_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjP_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjP_hl.name = somacubeP_invisbleToSpawn.name;
        //          spawnedObjP_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjP_hl.transform.localRotation = plusRotationP;

        //          GameObject spawnedObjP_OnlyCube = PhotonNetwork.Instantiate(somacubeP_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjP_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjP_OnlyCube.name = somacubeP_OnlyCube.name;
        //          spawnedObjP_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjP_OnlyCube.transform.localRotation = plusRotationP;

        //          //Spawn Object T 
        //          GameObject spawnedObjT = PhotonNetwork.Instantiate(somacubeTToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjT.transform.SetParent(anchor.transform, false);
        //          spawnedObjT.name = somacubeTToSpawn.name;
        //          spawnedObjT.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationT = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjT.transform.localRotation = plusRotationT;

        //          GameObject spawnedObjT_hl = PhotonNetwork.Instantiate(somacubeT_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjT_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjT_hl.name = somacubeT_invisbleToSpawn.name;
        //          spawnedObjT_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjT_hl.transform.localRotation = plusRotationT;

        //          GameObject spawnedObjT_OnlyCube = PhotonNetwork.Instantiate(somacubeT_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjT_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjT_OnlyCube.name = somacubeT_OnlyCube.name;
        //          spawnedObjT_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjT_OnlyCube.transform.localRotation = plusRotationT;

        //          //Spawn Object V
        //          GameObject spawnedObjV = PhotonNetwork.Instantiate(somacubeVToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjV.transform.SetParent(anchor.transform, false);
        //          spawnedObjV.name = somacubeVToSpawn.name;
        //          spawnedObjV.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationV = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjV.transform.localRotation = plusRotationV;

        //          GameObject spawnedObjV_hl = PhotonNetwork.Instantiate(somacubeV_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjV_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjV_hl.name = somacubeV_invisbleToSpawn.name;
        //          spawnedObjV_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjV_hl.transform.localRotation = plusRotationV;

        //          GameObject spawnedObjV_OnlyCube = PhotonNetwork.Instantiate(somacubeV_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjV_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjV_OnlyCube.name = somacubeV_OnlyCube.name;
        //          spawnedObjV_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjV_OnlyCube.transform.localRotation = plusRotationV;

        //          //Spawn Obejct Z 
        //          GameObject spawnedObjZ = PhotonNetwork.Instantiate(somacubeZToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjZ.transform.SetParent(anchor.transform, false);
        //          spawnedObjZ.name = somacubeZToSpawn.name;
        //          spawnedObjZ.transform.localPosition = new Vector3(-0.6f, -0.3f, 0.15f);
        //          Quaternion plusRotationZ = Quaternion.Euler(new Vector3(-90, 0, 180));
        //          spawnedObjZ.transform.localRotation = plusRotationZ;

        //          GameObject spawnedObjZ_hl = PhotonNetwork.Instantiate(somacubeZ_invisbleToSpawn.name, spawnPosition, spawnRotation);
        //          spawnedObjZ_hl.transform.SetParent(anchor.transform, false);
        //          spawnedObjZ_hl.name = somacubeZ_invisbleToSpawn.name;
        //          spawnedObjZ_hl.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjZ_hl.transform.localRotation = plusRotationZ;

        //          GameObject spawnedObjZ_OnlyCube = PhotonNetwork.Instantiate(somacubeZ_OnlyCube.name, spawnPosition, spawnRotation);
        //          spawnedObjZ_OnlyCube.transform.SetParent(anchor.transform, false);
        //          spawnedObjZ_OnlyCube.name = somacubeZ_OnlyCube.name;
        //          spawnedObjZ_OnlyCube.transform.localPosition = new Vector3(0.0f, -0.3f, 0.15f);
        //          spawnedObjZ_OnlyCube.transform.localRotation = plusRotationZ;
        //      }


        //      private IEnumerator<WaitForSeconds> WaitAndSpawn(MRUKAnchor anchor)
        //{
        //	Debug.Log("Waiting for Photon to join room...");

        //	// Loop until Photon is officially in a room
        //	while (!PhotonNetwork.InRoom)
        //	{
        //		// Print the current network state every 0.5 seconds
        //		Debug.Log($"[Photon Status]: {PhotonNetwork.NetworkClientState}");

        //		yield return new WaitForSeconds(0.5f);
        //	}

        //	Debug.Log("Photon ready! Proceeding with spawn.");
        //	SpawnOnTable(anchor);
        //}
    }
}