using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(ToggleGroup))]
public class RadioExperimentsGroupHandler : MonoBehaviour
{
	[Header("Context Reference")]
	[Tooltip("Drag the Conditions ToggleGroup here (the sibling group with Training / VR / GuidingCues toggles)")]
	public ToggleGroup groupOne;

	public GameObject[] somaCubeSolutions = new GameObject[10];

	private ToggleGroup _toggleGroup;

	// Cached condition context — updated whenever a condition toggle changes.
	private string _currentContext = "";

	// Direct toggle references — set in Start() by hierarchy order.
	// Using object-identity comparison is far more reliable than parsing
	// child Text/TMP content, which can have spaces, empty legacy Text
	// components, or other mismatches.
	private Toggle _exp1Toggle;   // first child toggle  = Experiment 1
	private Toggle _exp2Toggle;   // second child toggle = Experiment 2

	void Awake()
	{
		_toggleGroup = GetComponent<ToggleGroup>();
	}

	void Start()
	{
		// 1. Subscribe to the CONDITIONS group so _currentContext stays up-to-date.
		if (groupOne != null)
		{
			Toggle[] conditionToggles = groupOne.GetComponentsInChildren<Toggle>(includeInactive: true);
			foreach (Toggle ct in conditionToggles)
			{
				Toggle captured = ct;
				captured.onValueChanged.AddListener((isOn) =>
				{
					if (isOn)
					{
						_currentContext = captured.name;
						Debug.Log($"[RadioExperiments] condition context → '{_currentContext}'");
					}
				});

				// Capture whichever condition is already selected at Start
				if (ct.isOn)
				{
					_currentContext = ct.name;
					Debug.Log($"[RadioExperiments] initial context = '{_currentContext}'");
				}
			}
		}
		else
		{
			Debug.LogError("[RadioExperimentsGroupHandler] groupOne is NOT assigned in the Inspector! " +
						   "Drag the Conditions ToggleGroup into the 'Group One' field.");
		}

		// 2. Register listeners on all EXPERIMENT toggles.
		Toggle[] expToggles = GetComponentsInChildren<Toggle>(includeInactive: true);

		// Store the first two toggles by hierarchy order as Exp1 / Exp2.
		// This avoids any dependency on label text content.
		if (expToggles.Length >= 1) _exp1Toggle = expToggles[0];
		if (expToggles.Length >= 2) _exp2Toggle = expToggles[1];
		Debug.Log($"[RadioExperiments] Exp1Toggle='{_exp1Toggle?.name}'  Exp2Toggle='{_exp2Toggle?.name}'");

		foreach (Toggle toggle in expToggles)
		{
			toggle.group = _toggleGroup;

			Toggle t = toggle;
			t.onValueChanged.AddListener((isOn) =>
			{
				if (isOn) OnExperimentToggleClicked(t);
			});
		}

		PrintSelected();
	}

	// ---------------------------------------------------------------
	void OnExperimentToggleClicked(Toggle selectedToggle)
	{
		// Use object identity — no text parsing, no whitespace issues.
		bool isExp1 = (selectedToggle == _exp1Toggle);

		Debug.Log($"[RadioExperiments] clicked '{selectedToggle.name}'  isExp1={isExp1}  context='{_currentContext}'");

		if (string.IsNullOrEmpty(_currentContext))
		{
			Debug.LogWarning("[RadioExperimentsGroupHandler] No condition selected yet – cannot choose a solution.");
			return;
		}

		// Null-safe: hide every solution first
		for (int i = 0; i < somaCubeSolutions.Length; i++)
			if (somaCubeSolutions[i] != null) somaCubeSolutions[i].SetActive(false);

		if (_currentContext == "Toggle_Training")
			SetSolutionActive(isExp1 ? 5 : 6);
		else if (_currentContext == "Toggle_Virtual_Replicas")
			SetSolutionActive(isExp1 ? 7 : 8);
		else if (_currentContext == "Toggle_GuidingCues")
			SetSolutionActive(isExp1 ? 9 : 0);
		else if (_currentContext == "Toggle_GuidingCues_EyeGaze")
			SetSolutionActive(isExp1 ? 1 : 2);
		else
			Debug.LogWarning($"[RadioExperimentsGroupHandler] Unknown context: '{_currentContext}'. " +
							 "Check that the condition toggle GameObjects are named exactly: " +
							 "Toggle_Training / Toggle_Virtual_Replicas / Toggle_GuidingCues / Toggle_Experimentation");
	}

	void SetSolutionActive(int index)
	{
		if (index < 0 || index >= somaCubeSolutions.Length || somaCubeSolutions[index] == null)
		{
			Debug.LogWarning($"[RadioExperimentsGroupHandler] somaCubeSolutions[{index}] is null or out of range.");
			return;
		}

		somaCubeSolutions[index].SetActive(true);

		Debug.Log($"[RadioExperiments] Enabled [{index}] '{somaCubeSolutions[index].name}'  " +
				  $"activeSelf={somaCubeSolutions[index].activeSelf}  " +
				  $"activeInHierarchy={somaCubeSolutions[index].activeInHierarchy}");

		// Diagnose parent hierarchy if the object is still not visible
		if (!somaCubeSolutions[index].activeInHierarchy)
		{
			Transform t = somaCubeSolutions[index].transform.parent;
			while (t != null)
			{
				if (!t.gameObject.activeSelf)
					Debug.LogError($"[RadioExperiments] Parent '{t.name}' is INACTIVE — " +
								   "that is why the solution stays hidden despite SetActive(true).");
				t = t.parent;
			}
		}
	}

	public void PrintSelected()
	{
		Toggle active = _toggleGroup.ActiveToggles().FirstOrDefault();
		if (active != null)
			Debug.Log("[RadioExperiments] currently selected: " + active.name);
	}
}
