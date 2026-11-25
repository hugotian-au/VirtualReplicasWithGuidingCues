using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(ToggleGroup))]
public class RadioConditionsGroupHandler : MonoBehaviour
{
	public ToggleGroup groupTwo; // referred to the second toggle group

	private ToggleGroup _toggleGroup;

	void Awake()
	{
		_toggleGroup = GetComponent<ToggleGroup>();
	}

	void Start()
	{
		// 1. Acquire all Toggle components in children (assuming they are direct children or nested)
		Toggle[] toggles = GetComponentsInChildren<Toggle>();

		foreach (Toggle toggle in toggles)
		{
			// 2. Automatically register the Toggle to this Group to prevent manual dragging omissions
			toggle.group = _toggleGroup;

			// 3. Bind listener events
			// Note: Use a temporary variable to capture the current toggle to avoid Lambda closure pitfalls
			Toggle t = toggle;
			t.onValueChanged.AddListener((isOn) => {
				if (isOn)
				{
					OnRadioButtonClicked(t);
				}
			});
		}

		// Initialize: Print the currently selected item
		PrintSelected();
	}

	// Core processing logic
	void OnRadioButtonClicked(Toggle selectedToggle)
	{
		// Get the label for logging (try Text first, then TMP_Text, and fallback to GameObject name)
		string label = selectedToggle.GetComponentInChildren<Text>()?.text
					?? selectedToggle.GetComponentInChildren<TMPro.TMP_Text>()?.text
					?? selectedToggle.name;

		Debug.Log($"[RadioGroup] switch to: {label} (GameObject: {selectedToggle.name})");

		// Execute specific logic based on the selected toggle's name or label
		ExecuteSelectionLogic(selectedToggle.name);
	}

	void ExecuteSelectionLogic(string toggleName)
	{
		// Example logic branches
		switch (toggleName)
		{
			case "Mode_Local":
				// Execute local mode logic
				break;
			case "Mode_Remote":
				// Execute remote mode logic
				break;
		}
		groupTwo.SetAllTogglesOff();
		// 条件选择由 ExperimentUIController 的 Start 按钮统一发送，此处仅切换 Toggle 状态
	}

	// public method to print the currently selected toggle (can be called from UI buttons or other scripts)
	public void PrintSelected()
	{
		Toggle active = _toggleGroup.ActiveToggles().FirstOrDefault();
		if (active != null)
		{
			Debug.Log("Current Selection: " + active.name);
		}
	}
}