using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Com.IVE.ECL.RemoteCollaboration
{

	public class AxisDottedArcArrows3D : MonoBehaviour
	{
		[Header("Target")]
		public Transform target;
		public bool autoUseParentAsTarget = true;

		[Header("Orbit Center (target LOCAL space)")]
		public bool useLocalBoundsCenter = true;
		public Vector3 centerLocalOffset = Vector3.zero;

		[Header("Arc Placement (degrees)")]
		[Range(0f, 359.9f)] public float startAngleDeg = 0f;
		[Range(1f, 360f)] public float arcSpanDeg = 180f;

        // three axis control
        [Range(0f, 360f)] public float arcSpanX = 0f;
        [Range(0f, 360f)] public float arcSpanY = 0f;
        [Range(0f, 360f)] public float arcSpanZ = 0f;

        // per-ring visibility — set from ObjectTrackingStatus / TrackingVisualsReceiver
        [HideInInspector] public bool hideRingX = false;
        [HideInInspector] public bool hideRingY = false;
        [HideInInspector] public bool hideRingZ = false;

        [Header("Geometry")]
		[Min(0.01f)] public float radius = 0.25f;

		[Header("Arrow-as-Dot Style")]
		[Min(1f)] public float dashDeg = 6f;
		[Min(0f)] public float gapDeg = 4f;

		[Header("Arrow Shape (3D wedge)")]
		[Min(0.001f)] public float arrowLength = 0.03f;
		[Min(0.001f)] public float arrowWidth = 0.02f;
		[Min(0.001f)] public float arrowHeight = 0.004f;

		[Header("Motion")]
		public float flowDegPerSec = 30f;
		public bool reverseFlow = false;

		[Header("Optional base material (URP Unlit recommended)")]
		public Material baseMaterial;

		[Range(0f, 1f)] public float arrowAlpha = 0.4f;

		class Ring
		{
			public Vector3 localAxis;
			public Transform root;
			public float phaseDeg;
			public List<Transform> arrows = new List<Transform>();
		}

		// readonly List<Ring> _rings = new List<Ring>();
		Ring x_rotating_arrow = null;
		Ring y_rotating_arrow = null;
		Ring z_rotating_arrow = null;


		Mesh _arrowMesh;

		Material _matX;
		Material _matY;
		Material _matZ;

		Vector3 _cachedLocalCenter;
		bool _centerCached;
		bool _built;

		void Start()
		{
			// Build();
		}

		void Update()
		{
            if (!_built || !target) return;

            if (x_rotating_arrow != null) x_rotating_arrow.root.gameObject.SetActive(!hideRingX);
            if (y_rotating_arrow != null) y_rotating_arrow.root.gameObject.SetActive(!hideRingY);
            if (z_rotating_arrow != null) z_rotating_arrow.root.gameObject.SetActive(!hideRingZ);

            if (!hideRingX) Animate(Time.deltaTime, x_rotating_arrow, arcSpanX > 0 ? arcSpanX : arcSpanDeg);
            if (!hideRingY) Animate(Time.deltaTime, y_rotating_arrow, arcSpanY > 0 ? arcSpanY : arcSpanDeg);
            if (!hideRingZ) Animate(Time.deltaTime, z_rotating_arrow, arcSpanZ > 0 ? arcSpanZ : arcSpanDeg);
        }

		void OnEnable()
		{
			if (!_built)
				Build();
		}

		void OnDisable()
		{
			// Optionally, could clean up created objects here.

			Destroy(x_rotating_arrow.root.gameObject);
			Destroy(y_rotating_arrow.root.gameObject);
			Destroy(z_rotating_arrow.root.gameObject);

			_built = false;
		}

		void Build()
		{
			ResolveTarget();
			if (!target) return;

			_arrowMesh = BuildArrowWedgeMesh(arrowLength, arrowWidth, arrowHeight);

			CreateAxisMaterials();

			CacheLocalCenter();

			x_rotating_arrow = CreateRing("Ring_X", Vector3.right, _matX);
			y_rotating_arrow = CreateRing("Ring_Y", Vector3.up, _matY);
			z_rotating_arrow = CreateRing("Ring_Z", Vector3.forward, _matZ);

			_built = true;
		}

		void ResolveTarget()
		{
			if (!target && autoUseParentAsTarget && transform.parent)
				target = transform.parent;
		}

		// ----------------- Materials (the real fix) -----------------

		void CreateAxisMaterials()
		{
			// Pick a shader that exists in URP projects.
			Shader sh = null;

			if (baseMaterial != null && baseMaterial.shader != null)
			{
				sh = baseMaterial.shader;
			}
			else
			{
				sh = Shader.Find("Universal Render Pipeline/Unlit");
				if (!sh) sh = Shader.Find("Universal Render Pipeline/Lit");
			}

			if (!sh)
			{
				Debug.LogError("Could not find a URP shader (URP/Unlit or URP/Lit). Assign a material in Inspector.");
				return;
			}

			// Create 3 independent materials so colors do not overwrite each other.
			_matX = (baseMaterial != null) ? new Material(baseMaterial) : new Material(sh);
			_matY = (baseMaterial != null) ? new Material(baseMaterial) : new Material(sh);
			_matZ = (baseMaterial != null) ? new Material(baseMaterial) : new Material(sh);

			// Force transparent state in a robust way (only if the properties exist).
			SetupTransparent(_matX);
			SetupTransparent(_matY);
			SetupTransparent(_matZ);

			SetMatColor(_matX, new Color(1f, 0f, 0f, arrowAlpha)); // red
			SetMatColor(_matY, new Color(0f, 1f, 0f, arrowAlpha)); // green
			SetMatColor(_matZ, new Color(0f, 0f, 1f, arrowAlpha)); // blue
		}

		static void SetMatColor(Material m, Color c)
		{
			if (!m) return;

			// URP shaders commonly use _BaseColor. Some use _Color.
			if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
			if (m.HasProperty("_Color")) m.SetColor("_Color", c);

			// Some URP/Lit setups also have _BaseMap etc; color still works via _BaseColor.
		}

		static void SetupTransparent(Material m)
		{
			if (!m) return;

			// URP "surface type"
			if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 1 = Transparent

			// Disable alpha clipping
			if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
			if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0f);

			// ZWrite off for transparent
			if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);

			// Set blend factors if available (this is what often makes transparency actually work)
			if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			if (m.HasProperty("_BlendOp")) m.SetFloat("_BlendOp", (float)BlendOp.Add);

			// Keywords used by many URP shaders
			m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
			m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

			// Render queue
			m.renderQueue = (int)RenderQueue.Transparent;
		}

		// ----------------- Center -----------------

		void CacheLocalCenter()
		{
			if (!useLocalBoundsCenter)
			{
				_cachedLocalCenter = Vector3.zero;
				_centerCached = true;
				return;
			}

			Renderer[] rs = target.GetComponentsInChildren<Renderer>(true);
			if (rs == null || rs.Length == 0)
			{
				_cachedLocalCenter = Vector3.zero;
				_centerCached = true;
				return;
			}

			bool has = false;
			Vector3 min = Vector3.zero;
			Vector3 max = Vector3.zero;

			foreach (Renderer r in rs)
			{
				if (!r || !r.enabled) continue;

				Bounds b = r.bounds;
				Vector3 c = b.center;
				Vector3 e = b.extents;

				for (int x = -1; x <= 1; x += 2)
					for (int y = -1; y <= 1; y += 2)
						for (int z = -1; z <= 1; z += 2)
						{
							Vector3 w = c + Vector3.Scale(e, new Vector3(x, y, z));
							Vector3 l = target.InverseTransformPoint(w);

							if (!has)
							{
								min = l;
								max = l;
								has = true;
							}
							else
							{
								min = Vector3.Min(min, l);
								max = Vector3.Max(max, l);
							}
						}
			}

			_cachedLocalCenter = has ? (min + max) * 0.5f : Vector3.zero;
			_centerCached = true;
		}

		Vector3 OrbitCenterWorld()
		{
			if (!_centerCached) CacheLocalCenter();
			return target.TransformPoint(_cachedLocalCenter + centerLocalOffset);
		}

		// ----------------- Rings -----------------

		Ring CreateRing(string name, Vector3 localAxis, Material mat)
		{
			Ring ring = new Ring();
			ring.localAxis = localAxis.normalized;
			ring.phaseDeg = 0f;

			GameObject rootGO = new GameObject(name);
			rootGO.transform.SetParent(transform, false);
			ring.root = rootGO.transform;

			float step = Mathf.Max(0.0001f, dashDeg + gapDeg);
			int count = Mathf.Max(1, Mathf.FloorToInt(arcSpanDeg / step));

			for (int i = 0; i < count; i++)
			{
				GameObject go = new GameObject("Arrow_" + i.ToString("000"));
				go.transform.SetParent(ring.root, false);

				MeshFilter mf = go.AddComponent<MeshFilter>();
				MeshRenderer mr = go.AddComponent<MeshRenderer>();
				mf.sharedMesh = _arrowMesh;

				// Use sharedMaterial to avoid implicit per-renderer instancing.
				mr.sharedMaterial = mat;

				ring.arrows.Add(go.transform);
			}

			return ring;
		}

		// ----------------- Animate -----------------

		void Animate(float dt, Ring ring, float span)
		{
            Vector3 center = OrbitCenterWorld();
            float dir = reverseFlow ? -1f : 1f;
            span = Mathf.Max(0.0001f, span); // ← 用传入的 span 替换原来的 arcSpanDeg
            float step = Mathf.Max(0.0001f, dashDeg + gapDeg);

            Vector3 normalW = target.TransformDirection(ring.localAxis).normalized;

            ring.root.position = center;
            ring.root.rotation =
                Quaternion.FromToRotation(Vector3.forward, normalW) *
                Quaternion.AngleAxis(startAngleDeg, Vector3.forward);

            ring.phaseDeg = Mathf.Repeat(ring.phaseDeg + flowDegPerSec * dt * dir, span);

            for (int i = 0; i < ring.arrows.Count; i++)
            {
                float baseDeg = i * step + dashDeg * 0.5f;
                float aDeg = Mathf.Repeat(baseDeg + ring.phaseDeg, span);
                float a = aDeg * Mathf.Deg2Rad;

                Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                Vector3 forward = tangent.normalized * dir;

                Transform t = ring.arrows[i];
                t.localPosition = radial * radius;
                t.localRotation = Quaternion.FromToRotation(Vector3.right, forward);
            }
        }

		// ----------------- Mesh -----------------

		static Mesh BuildArrowWedgeMesh(float len, float width, float height)
		{
			Mesh m = new Mesh();
			m.name = "ArcArrowWedge";

			float hw = width * 0.5f;
			float hz = height * 0.5f;

			List<Vector3> v = new List<Vector3>
		{
			new Vector3(len, 0f,  hz),
			new Vector3(0f,  hw, hz),
			new Vector3(0f, -hw, hz),

			new Vector3(len, 0f, -hz),
			new Vector3(0f,  hw,-hz),
			new Vector3(0f, -hw,-hz)
		};

			List<int> t = new List<int>
		{
			0,1,2,  5,4,3,
			0,4,1,  0,3,4,
			2,5,0,  5,3,0,
			1,4,5,  1,5,2
		};

			m.SetVertices(v);
			m.SetTriangles(t, 0);
			m.RecalculateNormals();
			m.RecalculateBounds();
			return m;
		}
	}
}
