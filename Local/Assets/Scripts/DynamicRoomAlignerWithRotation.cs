using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;

public class DynamicRoomAlignerWithRotation : MonoBehaviour
{
    void Start()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
        }
    }

    private void OnSceneLoaded()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room != null)
        {
            AlignToRoom(room);
        }
    }

    public void AlignToRoom(MRUKRoom room)
    {
        Bounds totalBounds = new Bounds();
        bool boundsInitialized = false;
        float floorY = 0;

        Transform longestWall = null;
        float maxWallWidth = 0;

        foreach (var anchor in room.Anchors)
        {
            if (anchor.name.Contains("GLOBAL_MESH")) continue;

            MeshRenderer renderer = anchor.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                if (!boundsInitialized) { totalBounds = renderer.bounds; boundsInitialized = true; }
                else { totalBounds.Encapsulate(renderer.bounds); }
            }

            // 修正：将枚举转换为字符串进行匹配
            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR))
            {
                floorY = anchor.transform.position.y;
            }

            // 修正：寻找最长的墙面
            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE))
            {
                float currentWidth = 0;
                if (anchor.PlaneRect.HasValue)
                {
                    currentWidth = anchor.PlaneRect.Value.width;
                }
                else if (anchor.VolumeBounds.HasValue)
                {
                    currentWidth = anchor.VolumeBounds.Value.size.x;
                }

                if (currentWidth > maxWallWidth)
                {
                    maxWallWidth = currentWidth;
                    longestWall = anchor.transform;
                }
            }
        }

        if (boundsInitialized)
        {
            // 位置：中心 X/Z + 地面 Y
            Vector3 logicalCenter = new Vector3(totalBounds.center.x, floorY, totalBounds.center.z);

            // 旋转：对齐最长墙面的 Normal (forward)
            Quaternion logicalRotation = Quaternion.identity;
            if (longestWall != null)
            {
                Vector3 wallForward = longestWall.forward;
                wallForward.y = 0; // 确保水平
                logicalRotation = Quaternion.LookRotation(wallForward.normalized, Vector3.up);
            }

            this.transform.SetPositionAndRotation(logicalCenter, logicalRotation);
            Debug.Log($"对齐完成！中心位置: {logicalCenter}, 房间偏角: {logicalRotation.eulerAngles.y}");
        }
    }
}