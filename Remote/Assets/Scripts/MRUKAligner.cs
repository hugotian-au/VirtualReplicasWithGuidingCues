using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKRoomAligner : MonoBehaviour
{
    [Header("Alignment Settings")]
    [SerializeField] private bool alignOnLoad = true;
    [SerializeField] private bool disableEffectMeshAfterAlign = true; // 对齐后是否禁用网格

    [Header("Fine Tuning")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private bool _isAligned = false;

    /// <summary>True after ExecuteAlignmentRoutine() has successfully aligned ScannedRoom.</summary>
    public bool IsAligned => _isAligned;
    private bool _sceneLoaded = false;

    private void Start()
    {
        if (alignOnLoad && MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.AddListener(HandleSceneLoaded);

            // Guard: if SceneLoadedEvent already fired before we registered
            // (can happen on rapid restarts), kick off alignment now.
            if (MRUK.Instance.IsInitialized && MRUK.Instance.GetCurrentRoom() != null)
                HandleSceneLoaded();
        }
    }

    private void OnDestroy()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(HandleSceneLoaded);
        }
    }

    public void HandleSceneLoaded()
    {
        _sceneLoaded = true;
    }

    private void LateUpdate()
    {
        if (_isAligned) return;
        if (!_sceneLoaded) return;

        if (MRUK.Instance == null || !MRUK.Instance.IsInitialized || MRUK.Instance.GetCurrentRoom() == null) return;

        if (ExecuteAlignmentRoutine())
        {
            _isAligned = true;

            // 对齐成功后，执行禁用网格逻辑
            if (disableEffectMeshAfterAlign)
            {
                DisableAllEffectMeshes();
            }
        }
    }

    private bool ExecuteAlignmentRoutine()
    {
        MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();
        Bounds roomBounds = new Bounds();
        bool boundsInitialized = false;

        MRUKAnchor floorAnchor = null;
        MRUKAnchor longestWall = null;
        float maxWallWidth = 0;

        foreach (var anchor in currentRoom.Anchors)
        {
            if (anchor.name.Contains("GLOBAL_MESH")) continue;

            MeshRenderer mr = anchor.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                if (!boundsInitialized) { roomBounds = mr.bounds; boundsInitialized = true; }
                else { roomBounds.Encapsulate(mr.bounds); }
            }

            if (floorAnchor == null && anchor.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR))
                floorAnchor = anchor;

            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE))
            {
                float currentWidth = anchor.PlaneRect?.width ?? (anchor.VolumeBounds?.size.x ?? 0);
                if (currentWidth > maxWallWidth)
                {
                    maxWallWidth = currentWidth;
                    longestWall = anchor;
                }
            }
        }

        if (!boundsInitialized || floorAnchor == null || longestWall == null) return false;

        // 计算中心和旋转
        Vector3 finalPosition = new Vector3(roomBounds.center.x, floorAnchor.transform.position.y, roomBounds.center.z);
        Vector3 forward = Vector3.ProjectOnPlane(longestWall.transform.forward, Vector3.up).normalized;

        transform.position = finalPosition + positionOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(rotationOffset);

        return true;
    }

    private void DisableAllEffectMeshes()
    {
        // 方案 1：禁用当前房间的所有 Effect Mesh 渲染
        // 如果你使用了 MRUK 的 EffectMesh 预制体，它通常挂载在这些组件上
        EffectMesh[] meshes = FindObjectsOfType<EffectMesh>();
        foreach (var mesh in meshes)
        {
            mesh.gameObject.SetActive(false);
        }

        // 方案 2：如果网格是由各 Anchor 下的 MeshRenderer 显示的
        // 我们可以禁用它们的渲染器
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room != null)
        {
            foreach (var anchor in room.Anchors)
            {
                var renderers = anchor.GetComponentsInChildren<MeshRenderer>();
                foreach (var r in renderers)
                {
                    r.enabled = false;
                }
            }
        }

        Debug.Log("[MRUKAligner] 对齐完成，Effect Mesh 已隐藏。");
    }
}