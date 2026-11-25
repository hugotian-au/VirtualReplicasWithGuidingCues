using UnityEngine;
using Photon.Pun;

/// <summary>
/// 挂载在 Resources/ 文件夹下的 "RemotePresence" 预制件上。
/// 使用 Event Bus 系统实时同步实验状态，并驱动头像与注视射线。
/// </summary>
public class RemotePresenceSync : MonoBehaviourPun, IPunObservable
{
    [Header("Owner-side (发送端配置)")]
    public Transform headTransform;
    public EyeGazeRay eyeGazeRay;

    [Header("Non-owner visualization (接收端配置)")]
    public GameObject headAvatarRoot;
    public Transform gazeReticle;

    [Header("Gaze Ray Cylinder")]
    public GameObject gazeRayCylinder;
    public float gazeRayRadius = 0.005f;
    public Color gazeRayColor = new Color(0f, 0.8f, 1f, 0.6f);

    [Tooltip("最小头部高度过滤，防止头显放置在地面时产生干扰")]
    public float minHeadAnchorY = 0.1f;

    [Header("SomaCube Blink Settings")]
    public float blinkInterval = 0.3f;

    private const string RoomAnchorName = "ScannedRoom";
    private Transform _roomAnchor;

    // 内部状态
    private bool _eyeGazeEnabled = false;
    private GameObject _blinkTarget;
    private Renderer[] _blinkRenderers;
    private float _blinkTimer;
    private bool _blinkVisible = true;

    // 联网同步数据
    private Vector3 _headPos;
    private Quaternion _headRot = Quaternion.identity;
    private Vector3 _gazeDirLocal = Vector3.forward;

    #region Unity 生命周期与事件订阅

    private void OnEnable()
    {
        // 订阅全局实验条件变更事件
        ExperimentEventBus.OnConditionChanged += HandleConditionChange;
    }

    private void OnDisable()
    {
        // 退订事件，防止内存泄漏
        ExperimentEventBus.OnConditionChanged -= HandleConditionChange;
    }

    void Start()
    {
        // 1. 查找空间锚点
        var anchorGO = GameObject.Find(RoomAnchorName);
        if (anchorGO != null) _roomAnchor = anchorGO.transform;

        // 2. 初始显示设置
        if (headAvatarRoot != null) headAvatarRoot.SetActive(!photonView.IsMine);
        if (gazeReticle != null) gazeReticle.gameObject.SetActive(false);

        // 3. 自动创建射线柱体（仅针对远程玩家）
        CreateGazeCylinderIfNeeded();

        // 4. 重要：初始状态同步
        // 刚生成时，主动去事件总线查询当前的实验条件
        HandleConditionChange(ExperimentEventBus.CurrentCondition);
    }

    #endregion

    #region 核心逻辑处理

    /// <summary>
    /// 事件处理函数：当 ExperimentEventBus 发出信号时自动触发
    /// </summary>
    private void HandleConditionChange(string newCondition)
    {
        Debug.Log($"[RemotePresenceSync] {gameObject.name} 响应新条件: {newCondition}");

        // 判断是否开启注视功能
        _eyeGazeEnabled = (newCondition == "Cues and EyeGaze");

        // 如果是本地玩家，不需要显示自己的头像和射线
        if (photonView.IsMine)
        {
            if (headAvatarRoot != null) headAvatarRoot.SetActive(false);
            return;
        }

        // 处理远程玩家头像的显示
        // 逻辑：仅在 EyeGaze 模式下显示对方的头（可根据需求调整）
        if (headAvatarRoot != null)
            headAvatarRoot.SetActive(_eyeGazeEnabled);

        // 如果关闭了注视模式，立即隐藏相关视觉元素
        if (!_eyeGazeEnabled)
        {
            if (gazeRayCylinder != null) gazeRayCylinder.SetActive(false);
            if (gazeReticle != null) gazeReticle.gameObject.SetActive(false);

            // 重置闪烁物体，防止其停留在透明状态
            ResetBlinkEffect();
        }
    }

    void Update()
    {
        if (photonView.IsMine) return;

        // 1. 更新头像位置（基于空间锚点）
        Vector3 headWorldPos = _roomAnchor != null ? _roomAnchor.TransformPoint(_headPos) : _headPos;
        Quaternion headWorldRot = _roomAnchor != null ? _roomAnchor.rotation * _headRot : _headRot;

        if (headAvatarRoot != null)
        {
            headAvatarRoot.transform.position = headWorldPos;
            headAvatarRoot.transform.rotation = headWorldRot;
        }

        // 2. 射线检测与注视逻辑
        UpdateGazeLogic(headWorldPos, headWorldRot);
    }

    private void UpdateGazeLogic(Vector3 headWorldPos, Quaternion headWorldRot)
    {
        Vector3 gazeDir = _roomAnchor != null
            ? _roomAnchor.TransformDirection(_gazeDirLocal)
            : _gazeDirLocal;
        Vector3 hitWorldPt = headWorldPos + gazeDir * 10f;
        Vector3 hitNormal = Vector3.up;
        bool localHit = false;
        GameObject newBlinkTarget = null;

        // 执行本地射线检测
        if (Physics.Raycast(headWorldPos, gazeDir, out RaycastHit hit, 20f))
        {
            localHit = true;
            hitWorldPt = hit.point;
            hitNormal = hit.normal;

            // 寻找 SomaCube 目标
            Transform t = hit.collider.transform;
            while (t != null)
            {
                if (t.name.Contains("SomaCube")) { newBlinkTarget = t.gameObject; break; }
                t = t.parent;
            }
        }

        // 更新 Reticle (准星)
        if (gazeReticle != null)
        {
            bool show = _eyeGazeEnabled && localHit;
            gazeReticle.gameObject.SetActive(show);
            if (show)
            {
                gazeReticle.position = hitWorldPt + hitNormal * 0.002f;
                gazeReticle.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
            }
        }

        // 更新 Cylinder Ray (射线柱)
        if (gazeRayCylinder != null && _eyeGazeEnabled)
        {
            Vector3 dir = hitWorldPt - headWorldPos;
            float dist = Mathf.Max(dir.magnitude, 0.01f);
            gazeRayCylinder.SetActive(true);
            gazeRayCylinder.transform.position = headWorldPos + dir * 0.5f;
            gazeRayCylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            gazeRayCylinder.transform.localScale = new Vector3(gazeRayRadius, dist * 0.5f, gazeRayRadius);
        }

        // 更新 SomaCube 闪烁效果
        UpdateBlinkEffect(newBlinkTarget);
    }

    #endregion

    #region 辅助功能 (Blink & Cylinder Creation)

    private void UpdateBlinkEffect(GameObject newTarget)
    {
        if (newTarget != _blinkTarget)
        {
            ResetBlinkEffect();
            _blinkTarget = newTarget;
            _blinkRenderers = newTarget != null ? newTarget.GetComponentsInChildren<Renderer>() : null;
            _blinkTimer = 0f;
            _blinkVisible = true;
        }

        if (_blinkTarget != null && _eyeGazeEnabled)
        {
            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= blinkInterval)
            {
                _blinkTimer -= blinkInterval;
                _blinkVisible = !_blinkVisible;
                if (_blinkRenderers != null)
                    foreach (var r in _blinkRenderers) if (r != null) r.enabled = _blinkVisible;
            }
        }
    }

    private void ResetBlinkEffect()
    {
        if (_blinkTarget != null && _blinkRenderers != null)
        {
            foreach (var r in _blinkRenderers) if (r != null) r.enabled = true;
        }
        _blinkTarget = null;
        _blinkRenderers = null;
    }

    private void CreateGazeCylinderIfNeeded()
    {
        if (!photonView.IsMine && gazeRayCylinder == null)
        {
            gazeRayCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gazeRayCylinder.name = "GazeRayCylinder";
            gazeRayCylinder.transform.SetParent(transform);
            Destroy(gazeRayCylinder.GetComponent<Collider>());

            // Use URP Unlit shader (same approach as EyeGazeRay).
            // "Standard" is not included in URP builds and causes a magenta fallback.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");

            var mat = new Material(sh);

            // Apply color — URP uses _BaseColor; built-in uses _Color (mat.color)
            mat.color = gazeRayColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gazeRayColor);

            // Enable transparency
            if (mat.HasProperty("_Surface"))
            {
                // URP path: Surface = 1 (Transparent), Blend = 0 (Alpha)
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                // Built-in Standard fallback
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
            mat.renderQueue = 3000;

            gazeRayCylinder.GetComponent<Renderer>().material = mat;
            gazeRayCylinder.SetActive(false);
        }
    }

    #endregion

    #region Photon 序列化

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            bool isPresent = OVRManager.instance != null && OVRManager.instance.isUserPresent;
            stream.SendNext(isPresent);

            Vector3 sendPos = Vector3.zero;
            Quaternion sendRot = Quaternion.identity;

            if (_roomAnchor != null && headTransform != null)
            {
                sendPos = _roomAnchor.InverseTransformPoint(headTransform.position);
                sendRot = Quaternion.Inverse(_roomAnchor.rotation) * headTransform.rotation;
            }
            stream.SendNext(sendPos);
            stream.SendNext(sendRot);

            // 保持流长度一致，发送占位符数据（虽然接收端现在用本地 Raycast）
            stream.SendNext(false); // hasHit
            stream.SendNext(0);     // viewID
            stream.SendNext(Vector3.zero);
            stream.SendNext(Vector3.up);
        }
        else
        {
            stream.ReceiveNext(); // rxPresent
            Vector3 rxPos = (Vector3)stream.ReceiveNext();
            Quaternion rxRot = (Quaternion)stream.ReceiveNext();

            if (rxPos.y > minHeadAnchorY)
            {
                _headPos = rxPos;
                _headRot = rxRot;
            }

            // 接收远端发来的注视方向（anchor 局部坐标系）
            _gazeDirLocal = (Vector3)stream.ReceiveNext();
        }
    }

    #endregion
}