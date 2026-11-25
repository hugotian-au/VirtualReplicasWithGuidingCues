using UnityEngine;

namespace Com.IVE.ECL.RemoteCollaboration
{
    public class ObjectTrackingStatus : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Drag your 'DirectionArrow' prefab here")]
        public GameObject arrowPrefab = null;

        public Transform childWireframeTransform = null;

        private GameObject arrowInstance;
        private string mStatus = "idle";
        private AxisDottedArcArrows3D axisDottedArrow = null;
        private GameObject targetObject = null;
        private string targetName = "";
        private bool _arrowsAllowed = false;

        // ── Remote visibility override (set by TrackingVisualsReceiver on the remote device) ──
        // Default false → local side is unaffected; only the receiver writes this field.
        private bool _remoteVisible = false;

        // Cached renderers — populated in Start(), enforced every frame in Update()
        private Renderer[] _wireframeRenderers = null;

        // ── Per-direction alignment thresholds ────────────────────────────
        private const float ALIGN_ANGLE_THRESHOLD = 10f;   // degrees — per rotation axis
        private const float ALIGN_DIST_THRESHOLD  = 0.10f; // metres  — translation arrow

        // ── Public state for TrackingVisualsSync ──────────────────────────
        /// <summary>True when the guiding-cue arrows are currently visible.</summary>
        public bool IsVisible => mStatus == "tracked" && _arrowsAllowed && targetObject != null;

        /// <summary>True when image tracking is active — drives wireframe visibility on both sides.</summary>
        public bool IsTracked => mStatus == "tracked";

        /// <summary>True when the positional error is below threshold (translation arrow).</summary>
        public bool IsNearTranslation { get; private set; }
        /// <summary>True when the X-axis rotational error is below threshold (arcSpanX ring).</summary>
        public bool IsNearRotX { get; private set; }
        /// <summary>True when the Y-axis rotational error is below threshold (arcSpanY ring).</summary>
        public bool IsNearRotY { get; private set; }
        /// <summary>True when the Z-axis rotational error is below threshold (arcSpanZ ring).</summary>
        public bool IsNearRotZ { get; private set; }

        /// <summary>World-space start of the direction arrow (updated every frame).</summary>
        public Vector3 ArrowStartWorld { get; private set; }

        /// <summary>World-space end of the direction arrow (updated every frame).</summary>
        public Vector3 ArrowEndWorld { get; private set; }

        public float ArcSpanX => axisDottedArrow != null ? axisDottedArrow.arcSpanX : 0f;
        public float ArcSpanY => axisDottedArrow != null ? axisDottedArrow.arcSpanY : 0f;
        public float ArcSpanZ => axisDottedArrow != null ? axisDottedArrow.arcSpanZ : 0f;
        public bool  ReverseFlow => axisDottedArrow != null && axisDottedArrow.reverseFlow;

        #region Condition Logic (Event Bus System)

        private void OnEnable()
        {
            // 订阅全局实验条件变更事件
            ExperimentEventBus.OnConditionChanged += HandleConditionChange;
        }

        private void OnDisable()
        {
            // 退订事件
            ExperimentEventBus.OnConditionChanged -= HandleConditionChange;
        }

        private void HandleConditionChange(string condition)
        {
            // 逻辑处理：判断当前模式是否允许显示箭头
            _arrowsAllowed = (condition == "GuidingCues" || condition == "Cues and EyeGaze");
            RefreshArrowVisibility();
        }

        #endregion

        void Start()
        {
            targetName = gameObject.name.Replace("WireFrame", "Both");

            if (childWireframeTransform != null)
            {
                // Keep GameObject active so the Collider inside can trigger tracking,
                // but start with all Renderers hidden — they will be shown only when
                // tracking begins (SetTrackingStatus("tracked")).
                childWireframeTransform.gameObject.SetActive(true);
                axisDottedArrow = childWireframeTransform.GetComponent<AxisDottedArcArrows3D>();

                // Force the arc ring target to THIS transform so the ring normals
                // (X/Y/Z axes) are in the same coordinate frame as the rotation-diff
                // euler decomposition in ComputeAlignmentBools(), and in the same frame
                // as the remote proxy (which is built from ObjectTrackingStatus's pose).
                // Without this, autoUseParentAsTarget may pick childWireframeTransform.parent,
                // which could be a different object with a different orientation → axis mismatch.
                if (axisDottedArrow != null)
                {
                    axisDottedArrow.autoUseParentAsTarget = false;
                    axisDottedArrow.target               = transform;
                }

                _wireframeRenderers = childWireframeTransform.GetComponentsInChildren<Renderer>(true);
                foreach (var r in _wireframeRenderers)
                    r.enabled = false;
            }
            else
            {
                Debug.LogWarning("[ObjectTrackingStatus] Wireframe Child not set!");
            }

            // 初始同步：刚生成时，主动去总线拉取当前状态
            HandleConditionChange(ExperimentEventBus.CurrentCondition);
        }

        private bool TryFindTarget()
        {
            if (targetObject != null) return true;
            GameObject found = GameObject.Find(targetName);
            if (found != null) { targetObject = found; return true; }
            return false;
        }

        public void SetTrackingStatus(string status)
        {
            mStatus = status;

            if (mStatus == "tracked")
            {
                TryFindTarget();
                if (childWireframeTransform != null)
                {
                    // Keep GameObject active (collider must stay on for re-triggering).
                    // Re-enable renderers that were hidden during idle.
                    foreach (var r in childWireframeTransform.GetComponentsInChildren<Renderer>(true))
                        r.enabled = true;
                }
                RefreshArrowVisibility();
            }
            else  // idle, lost, or any other non-tracked state → always hide
            {
                if (childWireframeTransform != null)
                {
                    // Hide visuals but DO NOT call SetActive(false) — that would
                    // disable the Collider inside and prevent re-triggering tracking.
                    foreach (var r in childWireframeTransform.GetComponentsInChildren<Renderer>(true))
                        r.enabled = false;
                }
                if (axisDottedArrow != null)
                    axisDottedArrow.enabled = false;
                if (arrowInstance != null) { Destroy(arrowInstance); arrowInstance = null; }
                for (int k = 0; k < _alignCircles.Length; k++)
                    if (_alignCircles[k] != null) _alignCircles[k].gameObject.SetActive(false);
            }
        }

        private void RefreshArrowVisibility()
        {
            if (mStatus != "tracked") return;

            if (_arrowsAllowed)
            {
                if (axisDottedArrow != null) axisDottedArrow.enabled = true;
                if (targetObject != null) CreateArrowIfNeeded();
            }
            else
            {
                if (axisDottedArrow != null) axisDottedArrow.enabled = false;
                if (arrowInstance != null) { Destroy(arrowInstance); arrowInstance = null; }
                for (int k = 0; k < _alignCircles.Length; k++)
                    if (_alignCircles[k] != null) _alignCircles[k].gameObject.SetActive(false);
            }
        }

        // Raw rotation diffs, stored so ApplyLocalDisplay can read them without recomputing
        private float _rotDiffX, _rotDiffY, _rotDiffZ;

        // ── Smooth "aligned" circles (0=X red, 1=Y green, 2=Z blue, 3=translation orange)
        private const int   CIRCLE_SEGMENTS = 48;
        private const float ALIGN_CIRCLE_RADIUS = 0.025f;   // 5 cm diameter
        private readonly LineRenderer[] _alignCircles = new LineRenderer[4];
        private static readonly Color[] CIRCLE_COLORS =
        {
            new Color(1f,    0.25f, 0.25f, 0.9f), // red    — X
            new Color(0.25f, 1f,    0.25f, 0.9f), // green  — Y
            new Color(0.25f, 0.50f, 1f,    0.9f), // blue   — Z
            new Color(1f,    1f,    1f,    0.9f), // white  — translation
        };

        void Update()
        {
            // Enforce wireframe renderer visibility every frame — prevents external re-enabling.
            // _remoteVisible is written by TrackingVisualsReceiver; on the local device it stays false.
            if (_wireframeRenderers != null)
            {
                bool shouldShow = _remoteVisible || (mStatus == "tracked");
                foreach (var r in _wireframeRenderers)
                    if (r.enabled != shouldShow)
                        r.enabled = shouldShow;
            }

            if (mStatus != "tracked") return;
            if (!TryFindTarget()) return;

            // World positions needed by both rotation rings and translation arrow
            ArrowStartWorld = GetGroupCenter(gameObject);
            ArrowEndWorld   = GetGroupCenter(targetObject);

            // Alignment booleans — always compute when tracked+target found
            ComputeAlignmentBools();

            // Rotation rings + translation circle (independent of arrowPrefab)
            if (_arrowsAllowed)
                ApplyRotationDisplay();

            // Translation arrow — requires arrowPrefab to be assigned in Inspector
            if (_arrowsAllowed)
                CreateArrowIfNeeded();

            if (arrowInstance != null)
                ApplyTranslationDisplay();

            // Translation align circle (shows when position is aligned, arrows are allowed)
            if (_arrowsAllowed)
                ApplyTranslationCircle();
        }

        /// <summary>
        /// Computes IsNearTranslation, IsNearRotX/Y/Z and caches raw diffs.
        /// Pure state calculation — no side effects on visuals.
        /// </summary>
        private void ComputeAlignmentBools()
        {
            // Translation
            IsNearTranslation = Vector3.Distance(ArrowStartWorld, ArrowEndWorld) < ALIGN_DIST_THRESHOLD;

            // Rotation (per axis) — direct mapping: X diff → X ring, Y diff → Y ring, Z diff → Z ring
            Vector3 euler = (Quaternion.Inverse(transform.rotation) * targetObject.transform.rotation).eulerAngles;
            _rotDiffX = WrapAngle(euler.x);
            _rotDiffY = WrapAngle(euler.y);
            _rotDiffZ = WrapAngle(euler.z);

            IsNearRotX = Mathf.Abs(_rotDiffX) < ALIGN_ANGLE_THRESHOLD;
            IsNearRotY = Mathf.Abs(_rotDiffY) < ALIGN_ANGLE_THRESHOLD;
            IsNearRotZ = Mathf.Abs(_rotDiffZ) < ALIGN_ANGLE_THRESHOLD;
        }

        /// <summary>
        /// Rotation rings: hide arrow ring when aligned → show smooth LineRenderer circle instead.
        /// Show partial arc proportional to rotation error when not aligned.
        /// </summary>
        private void ApplyRotationDisplay()
        {
            if (axisDottedArrow == null) return;

            // Aligned axis  → hide arrow ring, show smooth circle
            // Misaligned axis → show partial arc, hide smooth circle
            axisDottedArrow.hideRingX = IsNearRotX;
            axisDottedArrow.hideRingY = IsNearRotY;
            axisDottedArrow.hideRingZ = IsNearRotZ;

            axisDottedArrow.arcSpanX    = Mathf.Clamp(Mathf.Abs(_rotDiffX), 1f, 359f);
            axisDottedArrow.arcSpanY    = Mathf.Clamp(Mathf.Abs(_rotDiffY), 1f, 359f);
            axisDottedArrow.arcSpanZ    = Mathf.Clamp(Mathf.Abs(_rotDiffZ), 1f, 359f);

            // Per-axis direction: each ring independently shows whether to rotate
            // positive or negative around that axis to reach the target.
            // reverseFlow (Y-based) is kept so TrackingVisualsSync can still stream
            // a single representative direction to the remote side unchanged.
            axisDottedArrow.reverseFlowX = _rotDiffX < 0;
            axisDottedArrow.reverseFlowY = _rotDiffY < 0;
            axisDottedArrow.reverseFlowZ = _rotDiffZ < 0;
            axisDottedArrow.reverseFlow  = _rotDiffY < 0;  // stream-only, not used by local rings

            // Smooth circles drawn around the wireframe center
            EnsureAlignCircles();
            Vector3 center = GetGroupCenter(gameObject);
            UpdateAlignCircle(0, IsNearRotX, transform.TransformDirection(Vector3.right),   center, ALIGN_CIRCLE_RADIUS);
            UpdateAlignCircle(1, IsNearRotY, transform.TransformDirection(Vector3.up),      center, ALIGN_CIRCLE_RADIUS);
            UpdateAlignCircle(2, IsNearRotZ, transform.TransformDirection(Vector3.forward), center, ALIGN_CIRCLE_RADIUS);
        }

        // ── Smooth circle helpers ─────────────────────────────────────────────

        private void EnsureAlignCircles()
        {
            if (_alignCircles[0] != null) return;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");

            for (int k = 0; k < _alignCircles.Length; k++)
            {
                var go = new GameObject($"AlignCircle_{k}");
                var lr = go.AddComponent<LineRenderer>();
                lr.loop           = true;
                lr.positionCount  = CIRCLE_SEGMENTS;
                lr.startWidth     = 0.005f;
                lr.endWidth       = 0.005f;
                lr.useWorldSpace  = true;
                if (sh != null)
                {
                    lr.material       = new Material(sh);
                    lr.material.color = CIRCLE_COLORS[k];
                }
                lr.gameObject.SetActive(false);
                _alignCircles[k] = lr;
            }
        }

        private void UpdateAlignCircle(int k, bool show, Vector3 worldAxis, Vector3 center, float radius)
        {
            if (_alignCircles[k] == null) return;
            _alignCircles[k].gameObject.SetActive(show);
            if (!show) return;

            // Build two orthogonal vectors in the plane perpendicular to worldAxis
            Vector3 perp = Vector3.Cross(worldAxis, Vector3.up);
            if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(worldAxis, Vector3.right);
            perp.Normalize();
            Vector3 perp2 = Vector3.Cross(worldAxis, perp).normalized;

            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                float   a   = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
                Vector3 pos = center + (perp * Mathf.Cos(a) + perp2 * Mathf.Sin(a)) * radius;
                _alignCircles[k].SetPosition(i, pos);
            }
        }

        /// <summary>
        /// Translation arrow: show when distance > threshold, hide when near.
        /// Requires arrowInstance to exist (arrowPrefab assigned in Inspector).
        /// </summary>
        private void ApplyTranslationDisplay()
        {
            if (arrowInstance == null) return;

            ArrowRenderer arrowScript = arrowInstance.GetComponent<ArrowRenderer>();
            arrowInstance.SetActive(!IsNearTranslation);
            if (!IsNearTranslation && arrowScript != null)
                arrowScript.SetPositions(ArrowStartWorld, ArrowEndWorld);
        }

        /// <summary>
        /// Translation align circle: shows a horizontal ring when position is aligned.
        /// Circle lies in the XZ plane (faces upward) at the wireframe center.
        /// </summary>
        private void ApplyTranslationCircle()
        {
            EnsureAlignCircles();
            // Use world up as the circle's normal → horizontal ring the user can see from above/side
            UpdateAlignCircle(3, IsNearTranslation, Vector3.up,
                              GetGroupCenter(gameObject), 0.035f);   // 7 cm diameter
        }

        private float WrapAngle(float angle) => angle > 180f ? angle - 360f : angle;

        private void CreateArrowIfNeeded()
        {
            if (arrowInstance != null) return;
            if (arrowPrefab == null) { Debug.LogError("[ObjectTrackingStatus] arrowPrefab is missing!"); return; }
            arrowInstance = Instantiate(arrowPrefab);
            arrowInstance.name = $"Arrow_{gameObject.name}_to_{(targetObject != null ? targetObject.name : "remote")}";
        }

        /// <summary>
        /// Called every frame by <see cref="TrackingVisualsReceiver"/> on the remote device
        /// to mirror the local side's tracking visibility.
        /// The renderer-enforcement loop in Update() picks this value up automatically.
        /// </summary>
        public void SetWireframeVisible(bool vis) => _remoteVisible = vis;

        public Vector3 GetGroupCenter(GameObject parentObject)
        {
            Renderer[] renderers = parentObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return parentObject.transform.position;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.center;
        }

        void OnDestroy()
        {
            if (arrowInstance != null) Destroy(arrowInstance);
            for (int k = 0; k < _alignCircles.Length; k++)
                if (_alignCircles[k] != null) Destroy(_alignCircles[k].gameObject);
        }
    }
}