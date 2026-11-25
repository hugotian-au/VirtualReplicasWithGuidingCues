using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Runs on the local Quest (MasterClient).
/// Receives experiment selections from the remote PC via Photon RPC,
/// broadcasts changes through ExperimentEventBus, and drives the Training HUD.
/// </summary>
public class SelectionSyncManager : MonoBehaviourPun
{
    [Header("Phase Durations (seconds)")]
    public float phase1Duration = 60f;
    public float phase2Duration = 60f;
    public float phase3Duration = 60f;

    [Header("Training HUD")]
    [Tooltip("Drag the TrainingPhaseHUD component here")]
    public TrainingPhaseHUD trainingHUD;

    private Coroutine _experimentCoroutine;

    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------
    void Start()
    {
        // Auto-create the HUD if it wasn't dragged in via the Inspector.
        // TrainingPhaseHUD.Start() will parent itself to Camera.main.
        if (trainingHUD == null)
        {
            var go = new GameObject("TrainingPhaseHUD");
            trainingHUD = go.AddComponent<TrainingPhaseHUD>();
            Debug.Log("[SelectionSyncManager] TrainingPhaseHUD created automatically.");
        }
    }

    // -------------------------------------------------------
    // Photon RPC — receives selection from remote PC
    // -------------------------------------------------------
    [PunRPC]
    void RPC_ReceiveSelection(string selection, int playerID)
    {
        ProcessMasterLogic(selection);
    }

    // -------------------------------------------------------
    // Experiment scene logic
    // -------------------------------------------------------
    void ProcessMasterLogic(string selection)
    {
        if (_experimentCoroutine != null)
        {
            StopCoroutine(_experimentCoroutine);
            _experimentCoroutine = null;
        }

        if (trainingHUD != null) trainingHUD.Hide();

        // Broadcast new condition to all local subscribers (arrows, eye gaze, etc.)
        ExperimentEventBus.CurrentCondition = selection;

        if (selection == "Training")
        {
            // 把本地 Inspector 里配置的时长发给 remote 端，remote 收到后再启动 HUD 倒计时
            photonView.RPC("RPC_StartTraining", RpcTarget.Others,
                           phase1Duration, phase2Duration, phase3Duration);
            _experimentCoroutine = StartCoroutine(RunThreePhases());
        }
    }

    IEnumerator RunThreePhases()
    {
        // Phase 1 — Virtual Replicas only
        ExperimentEventBus.CurrentCondition = "Virtual Replicas";
        if (trainingHUD != null) trainingHUD.ShowPhase("Virtual Replicas", phase1Duration);
        yield return new WaitForSeconds(phase1Duration);

        // Phase 2 — add Guiding Cues
        ExperimentEventBus.CurrentCondition = "GuidingCues";
        if (trainingHUD != null) trainingHUD.ShowPhase("Guiding Cues", phase2Duration);
        yield return new WaitForSeconds(phase2Duration);

        // Phase 3 — add Eye Gaze
        ExperimentEventBus.CurrentCondition = "Cues and EyeGaze";
        if (trainingHUD != null) trainingHUD.ShowPhase("Cues and Eye Gaze", phase3Duration);
        yield return new WaitForSeconds(phase3Duration);

        // Training complete
        if (trainingHUD != null) trainingHUD.Hide();
    }
}
