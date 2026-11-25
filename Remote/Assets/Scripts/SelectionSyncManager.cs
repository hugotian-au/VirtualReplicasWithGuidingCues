using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Runs on the remote PC (experimenter side).
/// Sends experiment selections to the MasterClient (Quest) and writes the session log locally.
///
/// Training HUD timing is driven by the Quest — when the Quest processes "Training" it sends
/// RPC_StartTraining with the actual phase durations configured in its own Inspector.
/// The remote PC never needs to manually match duration values.
/// </summary>
public class SelectionSyncManager : MonoBehaviourPun
{
    [Header("Training HUD")]
    [Tooltip("Drag the RemoteTrainingHUD component here, or leave empty for auto-create.")]
    public RemoteTrainingHUD trainingHUD;

    private DateTime  _startTime;
    private string    _condition  = "";
    private string    _experiment = "";
    private bool      _isRunning  = false;
    private Coroutine _trainingCoroutine;

    // Phase durations received from the Quest via RPC_StartTraining
    private float _phase1Duration;
    private float _phase2Duration;
    private float _phase3Duration;

    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------
    void Start()
    {
        if (trainingHUD == null)
        {
            var go = new GameObject("RemoteTrainingHUD");
            trainingHUD = go.AddComponent<RemoteTrainingHUD>();
            Debug.Log("[SelectionSyncManager] RemoteTrainingHUD created automatically.");
        }
    }

    // -------------------------------------------------------
    // Called by ExperimentUIController buttons
    // -------------------------------------------------------
    public void StartExperiment(string condition, string experiment)
    {
        if (_isRunning)
        {
            Debug.LogWarning("[SelectionSyncManager] Already running — click End first.");
            return;
        }

        _condition   = condition;
        _experiment  = experiment;
        _startTime   = DateTime.Now;
        _isRunning   = true;

        // 通知本地订阅者（EyeGaze 等）条件已变更
        ExperimentEventBus.CurrentCondition = condition;

        // Training 的倒计时由 Quest 端通过 RPC_StartTraining 触发，这里不启动协程

        if (PhotonNetwork.IsConnected)
            photonView.RPC("RPC_ReceiveSelection", RpcTarget.MasterClient,
                           condition, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    public void StopExperiment()
    {
        if (!_isRunning)
        {
            Debug.LogWarning("[SelectionSyncManager] No experiment is running.");
            return;
        }

        // 停止 Training 倒计时（如果正在运行）
        if (_trainingCoroutine != null)
        {
            StopCoroutine(_trainingCoroutine);
            _trainingCoroutine = null;
        }
        if (trainingHUD != null) trainingHUD.Hide();

        if (PhotonNetwork.IsConnected)
            photonView.RPC("RPC_ReceiveSelection", RpcTarget.MasterClient,
                           "_Stop", PhotonNetwork.LocalPlayer.ActorNumber);

        // 清除条件
        ExperimentEventBus.CurrentCondition = "";

        double duration = (DateTime.Now - _startTime).TotalSeconds;
        WriteLog(_startTime, _condition, _experiment, duration);
        _isRunning = false;
    }

    // -------------------------------------------------------
    // Photon RPC — Quest 端发来的 Training 时长
    // -------------------------------------------------------
    [PunRPC]
    void RPC_StartTraining(float p1, float p2, float p3)
    {
        _phase1Duration = p1;
        _phase2Duration = p2;
        _phase3Duration = p3;

        Debug.Log($"[SelectionSyncManager] RPC_StartTraining received: {p1}s / {p2}s / {p3}s");

        if (_trainingCoroutine != null)
        {
            StopCoroutine(_trainingCoroutine);
            _trainingCoroutine = null;
        }
        _trainingCoroutine = StartCoroutine(RunTrainingPhases());
    }

    // -------------------------------------------------------
    // Training phase coroutine (mirrors Quest-side RunThreePhases)
    // -------------------------------------------------------
    IEnumerator RunTrainingPhases()
    {
        // Phase 1 — Virtual Replicas only
        ExperimentEventBus.CurrentCondition = "Virtual Replicas";
        if (trainingHUD != null) trainingHUD.ShowPhase("Virtual Replicas", _phase1Duration);
        yield return new WaitForSeconds(_phase1Duration);

        // Phase 2 — Guiding Cues
        ExperimentEventBus.CurrentCondition = "GuidingCues";
        if (trainingHUD != null) trainingHUD.ShowPhase("Guiding Cues", _phase2Duration);
        yield return new WaitForSeconds(_phase2Duration);

        // Phase 3 — Cues and Eye Gaze
        ExperimentEventBus.CurrentCondition = "Cues and EyeGaze";
        if (trainingHUD != null) trainingHUD.ShowPhase("Cues and Eye Gaze", _phase3Duration);
        yield return new WaitForSeconds(_phase3Duration);

        // Training complete
        ExperimentEventBus.CurrentCondition = "";
        if (trainingHUD != null) trainingHUD.Hide();
        _trainingCoroutine = null;
    }

    // -------------------------------------------------------
    // Log — written locally on the remote PC
    // -------------------------------------------------------
    private void WriteLog(DateTime startTime, string condition, string experiment, double durationSeconds)
    {
        string path  = Path.Combine(Application.persistentDataPath, "experiment_log.csv");
        bool   isNew = !File.Exists(path);

        try
        {
            using (var sw = new StreamWriter(path, append: true))
            {
                if (isNew)
                    sw.WriteLine("Date,StartTime,Condition,Experiment,DurationSeconds");

                sw.WriteLine($"{startTime:yyyy-MM-dd},{startTime:HH:mm:ss},{condition},{experiment},{durationSeconds:F1}");
            }
            Debug.Log($"[SelectionSyncManager] Log saved: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SelectionSyncManager] Log write failed: {e.Message}");
        }
    }
}
