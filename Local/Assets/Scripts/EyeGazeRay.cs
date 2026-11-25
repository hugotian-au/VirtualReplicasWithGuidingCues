using UnityEngine;
using UnityEngine.Rendering;
using Photon.Pun;

/// <summary>
/// Eye Gaze Ray for Meta Quest Pro.
///
/// MUST be a child of CenterEyeAnchor.
/// Auto-generates:
///   - The ray itself: a 3D cylinder mesh, head-locked.
///   - A hit reticle: a flat orange disc that snaps to the hit surface.
/// </summary>
[DisallowMultipleComponent]
public class EyeGazeRay : MonoBehaviour
{
	// Gaze hit data for network sync — updated every LateUpdate
	public bool HasHit { get; private set; }
	/// <summary>Current smoothed gaze direction in world space.</summary>
	public Vector3 GazeWorldDirection { get; private set; }
	/// <summary>PhotonView ID of the hit object. 0 if nothing was hit or object has no PhotonView.</summary>
	public int HitPhotonViewID { get; private set; }
	/// <summary>Hit point in the hit PhotonView object's local space.</summary>
	public Vector3 LocalHitPoint { get; private set; }
	/// <summary>Hit normal in the hit PhotonView object's local space.</summary>
	public Vector3 LocalHitNormal { get; private set; }
	[Header("Input References")]
	[Tooltip("[BuildingBlock] Eye Gaze Left  (forward axis = left-eye gaze)")]
	public Transform leftEye;

	[Tooltip("[BuildingBlock] Eye Gaze Right (forward axis = right-eye gaze)")]
	public Transform rightEye;

	[Header("Ray Appearance")]
	[Tooltip("Show the cylindrical ray itself. Turn off if you only want the reticle.")]
	public bool showRay = true;

	public float maxDistance = 50f;

	[Tooltip("Cylinder radius in meters. 0.005 = 1cm thick.")]
	public float radius = 0.005f;

	[Tooltip("Push start forward from the eye so you don't see the ray from inside it (meters).")]
	public float startOffset = 0.10f;

	public Color rayColor = new Color(0.2f, 0.9f, 1f, 1f);

	[Tooltip("Higher = snappier, lower = smoother. 15 to 25 feels good.")]
	public float lerpSpeed = 20f;

	[Header("Hit Detection")]
	[Tooltip("If true, the ray's length is clipped to the first hit on hitMask.")]
	public bool stopOnHit = true;

	[Tooltip("Layers the gaze raycast can hit.")]
	public LayerMask hitMask = ~0;

	[Header("Hit Reticle (orange disc)")]
	[Tooltip("Show a disc at the hit point.")]
	public bool showReticle = true;

	[Tooltip("Reticle radius in meters.")]
	public float reticleRadius = 0.03f;

	public Color reticleColor = new Color(1f, 0.5f, 0.05f, 1f); // orange

	[Header("SomaCube Highlight")]
	[Tooltip("Blink interval in seconds when gazing at a SomaCube.")]
	public float blinkInterval = 0.3f;

	// ---- internal ----
	private MeshFilter _mf;
	private MeshRenderer _mr;
	private Material _rayMat;

	private GameObject _reticle;
	private Material _reticleMat;

	private Vector3 _smoothedLocalDir;
	private bool _initialized;
	private RaycastHit _lastHit;

	// SomaCube blink state
	private GameObject  _blinkTarget;
	private Renderer[]  _blinkRenderers;
	private float       _blinkTimer;
	private bool        _blinkVisible = true;

	void Awake()
	{
		// Get a cylinder mesh once and reuse for both ray and reticle.
		Mesh cylinderMesh = GrabCylinderMesh();

		Shader unlit = ResolveUnlitShader();

		// 1. Setup the ray itself (this GameObject).
		_mf = GetComponent<MeshFilter>();
		if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
		if (_mf.sharedMesh == null) _mf.sharedMesh = cylinderMesh;

		_mr = GetComponent<MeshRenderer>();
		if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();
		_rayMat = MakeMat(unlit, rayColor);
		_mr.sharedMaterial = _rayMat;
		ConfigureRenderer(_mr);

		foreach (var col in GetComponentsInChildren<Collider>(true))
			col.enabled = false;

		// 2. Setup the reticle as a sibling under CenterEyeAnchor.
		_reticle = new GameObject("HitReticle");
		_reticle.transform.SetParent(transform.parent, false);

		var rmf = _reticle.AddComponent<MeshFilter>();
		rmf.sharedMesh = cylinderMesh;

		var rmr = _reticle.AddComponent<MeshRenderer>();
		_reticleMat = MakeMat(unlit, reticleColor);
		rmr.sharedMaterial = _reticleMat;
		ConfigureRenderer(rmr);

		_reticle.SetActive(false);
	}

	void LateUpdate()
	{
		if (leftEye == null || rightEye == null || transform.parent == null) return;

		// 1. World-space gaze direction (averaged over both eyes).
		Vector3 worldDir = leftEye.forward + rightEye.forward;
		if (worldDir.sqrMagnitude < 1e-6f) worldDir = transform.parent.forward;
		worldDir.Normalize();

		// 2. Convert to LOCAL space of CenterEyeAnchor.
		Vector3 localDir = transform.parent.InverseTransformDirection(worldDir);

		// 3. Smooth.
		if (!_initialized) { _smoothedLocalDir = localDir; _initialized = true; }
		else _smoothedLocalDir = Vector3.Slerp(_smoothedLocalDir, localDir, Time.deltaTime * lerpSpeed).normalized;

		// Expose smoothed gaze direction in world space for network sync
		GazeWorldDirection = transform.parent.TransformDirection(_smoothedLocalDir);

		// 4. Raycast (in world space).
		float length = maxDistance - startOffset;
		bool didHit = false;
		Vector3 hitLocal = Vector3.zero;
		Vector3 hitNormalLocal = -_smoothedLocalDir;

		if ((stopOnHit || showReticle) && hitMask.value != 0)
		{
			Vector3 worldStart = transform.parent.TransformPoint(_smoothedLocalDir * startOffset);
			Vector3 worldRayDir = transform.parent.TransformDirection(_smoothedLocalDir);
			if (Physics.Raycast(worldStart, worldRayDir, out _lastHit,
								length, hitMask, QueryTriggerInteraction.Ignore))
			{
				didHit = true;
				if (stopOnHit) length = _lastHit.distance;
				hitLocal = transform.parent.InverseTransformPoint(_lastHit.point);
				hitNormalLocal = transform.parent.InverseTransformDirection(_lastHit.normal);
			}
		}
		length = Mathf.Max(0.01f, length);

		// Update public gaze hit properties for network sync
		HasHit = didHit;
		HitPhotonViewID = 0;
		LocalHitPoint = Vector3.zero;
		LocalHitNormal = Vector3.up;
		if (didHit)
		{
			var pv = _lastHit.collider.GetComponentInParent<PhotonView>();
			if (pv != null)
			{
				HitPhotonViewID = pv.ViewID;
				LocalHitPoint = pv.transform.InverseTransformPoint(_lastHit.point);
				LocalHitNormal = pv.transform.InverseTransformDirection(_lastHit.normal);
			}
		}

		// 5. Place ray cylinder in local space (and toggle visibility).
		if (_mr != null && _mr.enabled != showRay) _mr.enabled = showRay;
		if (showRay)
		{
			Vector3 localStart = _smoothedLocalDir * startOffset;
			Vector3 localMid = localStart + _smoothedLocalDir * (length * 0.5f);
			transform.localPosition = localMid;
			transform.localRotation = Quaternion.FromToRotation(Vector3.up, _smoothedLocalDir);
			transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
		}

		// 6. Place reticle (only when there is a hit).
		if (_reticle != null)
		{
			bool active = showReticle && didHit;
			if (_reticle.activeSelf != active) _reticle.SetActive(active);

			if (active)
			{
				// Push 2mm out along the surface normal to avoid z-fighting.
				_reticle.transform.localPosition = hitLocal + hitNormalLocal.normalized * 0.002f;
				// Cylinder's Y axis aligns with surface normal so the disc lies flat on the surface.
				_reticle.transform.localRotation = Quaternion.FromToRotation(Vector3.up, hitNormalLocal);
				// Thin disc: diameter on X/Z, very thin on Y.
				_reticle.transform.localScale = new Vector3(reticleRadius * 2f, 0.0005f, reticleRadius * 2f);

				ApplyColor(_reticleMat, reticleColor);
			}
		}

		// Live-update ray color.
		ApplyColor(_rayMat, rayColor);

		// ---- SomaCube blink ----
		// Find if hit object (or any ancestor) has "SomaCube" in its name
		GameObject newBlinkTarget = null;
		if (didHit)
		{
			Transform t = _lastHit.collider.transform;
			while (t != null)
			{
				if (t.name.Contains("SomaCube")) { newBlinkTarget = t.gameObject; break; }
				t = t.parent;
			}
		}

		// Switch target: restore old, start new
		if (newBlinkTarget != _blinkTarget)
		{
			if (_blinkTarget != null && _blinkRenderers != null)
				foreach (var r in _blinkRenderers) if (r != null) r.enabled = true;

			_blinkTarget    = newBlinkTarget;
			_blinkRenderers = newBlinkTarget != null
				? newBlinkTarget.GetComponentsInChildren<Renderer>()
				: null;
			_blinkTimer   = 0f;
			_blinkVisible = true;
		}

		// Tick blink
		if (_blinkTarget != null)
		{
			_blinkTimer += Time.deltaTime;
			if (_blinkTimer >= blinkInterval)
			{
				_blinkTimer  -= blinkInterval;
				_blinkVisible = !_blinkVisible;
				if (_blinkRenderers != null)
					foreach (var r in _blinkRenderers) if (r != null) r.enabled = _blinkVisible;
			}
		}
	}

	void OnDestroy()
	{
		if (_reticle != null)
		{
			if (Application.isPlaying) Destroy(_reticle);
			else DestroyImmediate(_reticle);
		}
	}

	// ---- helpers ----

	static Mesh GrabCylinderMesh()
	{
		GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		Mesh m = temp.GetComponent<MeshFilter>().sharedMesh;
		if (Application.isPlaying) Destroy(temp);
		else DestroyImmediate(temp);
		return m;
	}

	static Shader ResolveUnlitShader()
	{
		Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
		if (sh == null) sh = Shader.Find("Unlit/Color");
		if (sh == null) sh = Shader.Find("Sprites/Default");
		return sh;
	}

	static Material MakeMat(Shader sh, Color c)
	{
		Material m = new Material(sh);
		m.color = c;
		if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
		return m;
	}

	static void ApplyColor(Material m, Color c)
	{
		if (m == null) return;
		m.color = c;
		if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
	}

	static void ConfigureRenderer(MeshRenderer r)
	{
		r.shadowCastingMode = ShadowCastingMode.Off;
		r.receiveShadows = false;
		r.lightProbeUsage = LightProbeUsage.Off;
		r.allowOcclusionWhenDynamic = false;
	}
}
