using UnityEngine;
using UnityEngine.UI;

namespace Com.IVE.ECL.RemoteCollaboration
{
    /// <summary>
    /// 挂在远端 Panel_Experiments（或其父节点）上。
    /// 读取 Condition / Experiment Toggle，在 Start / End 按钮点击时调用 SelectionSyncManager。
    /// </summary>
    public class ExperimentUIController : MonoBehaviour
    {
        [Header("Condition Toggles")]
        public Toggle toggleTraining;
        public Toggle toggleVirtualReplicas;
        public Toggle toggleGuidingCues;
        public Toggle toggleCuesAndEyeGaze;

        [Header("Experiment Toggles")]
        public Toggle toggleExperiment1;
        public Toggle toggleExperiment2;

        [Header("Buttons")]
        public Button buttonStart;
        public Button buttonEnd;

        [Header("SelectionSyncManager")]
        public SelectionSyncManager selectionSyncManager;

        private (Toggle toggle, string conditionName)[] _conditionMap;

        void Awake()
        {
            _conditionMap = new[]
            {
                (toggleTraining,        "Training"),
                (toggleVirtualReplicas, "Virtual Replicas"),
                (toggleGuidingCues,     "GuidingCues"),
                (toggleCuesAndEyeGaze,  "Cues and EyeGaze"),
            };

            buttonStart.onClick.AddListener(OnStartClicked);
            buttonEnd.onClick.AddListener(OnEndClicked);

            buttonEnd.interactable   = false;
            buttonStart.interactable = true;
        }

        void OnStartClicked()
        {
            string condition  = GetSelectedCondition();
            string experiment = GetSelectedExperiment();

            if (string.IsNullOrEmpty(condition))
            {
                Debug.LogWarning("[ExperimentUIController] 请先选择一个 Condition。");
                return;
            }
            if (string.IsNullOrEmpty(experiment))
            {
                Debug.LogWarning("[ExperimentUIController] 请先选择一个 Experiment。");
                return;
            }

            selectionSyncManager.StartExperiment(condition, experiment);
            buttonStart.interactable = false;
            buttonEnd.interactable   = true;
        }

        void OnEndClicked()
        {
            selectionSyncManager.StopExperiment();
            buttonEnd.interactable   = false;
            buttonStart.interactable = true;
        }

        private string GetSelectedCondition()
        {
            foreach (var (toggle, name) in _conditionMap)
                if (toggle != null && toggle.isOn) return name;
            return "";
        }

        private string GetSelectedExperiment()
        {
            if (toggleExperiment1 != null && toggleExperiment1.isOn) return "Experiment1";
            if (toggleExperiment2 != null && toggleExperiment2.isOn) return "Experiment2";
            return "";
        }
    }
}
