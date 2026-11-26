# Virtual Replicas with Automated Guidance and Head-and-Gaze Visualization

Unity source code of the system used in the study reported in the manuscript *"Automated guidance
and head-and-gaze visualization added no detectable benefit to virtual replicas in asymmetric mixed
reality remote collaboration"* (Tian et al.).

In this asymmetric mixed reality system, a **remote expert** in virtual reality (VR, Meta Quest Pro)
guides a **local worker** in augmented reality (AR, Meta Quest 3) through the assembly of physical
Soma cube puzzles. The expert shows target poses by manipulating virtual replicas. Two further
layers can be added: automated 6-DoF guidance cues (translation arrows and rotation arcs computed
from the tracked pose of each piece) and a head-and-gaze visualization (the expert's head avatar and
eye-gaze ray, shown to the worker; the expert sees a small gaze reticle).

## Study conditions

Conditions are selected on the experiment panel of the Remote project. They are cumulative.

| Panel label | Name in the manuscript | Content |
|-------------|------------------------|---------|
| Training | Training | Practice session |
| Virtual Replicas | C1: virtual replicas (baseline) | Tracked physical pieces, expert-manipulated virtual replicas and the shared room representation |
| GuidingCues | C2: automated guidance | C1 + automated translation and rotation guidance cues |
| Cues and EyeGaze | C3: guidance + head-and-gaze | C2 + the expert's head avatar and eye-gaze ray |

## Repository layout

```
Local/    Unity project for the local worker (Meta Quest 3, passthrough AR)
Remote/   Unity project for the remote expert (Meta Quest Pro, VR, eye tracking, point cloud)
supplementary_material/   Study instruments, coding definitions, aggregated results, and sensitivity analyses
```

Both projects use **Unity 6000.1.13f1**, Meta XR SDK 78.0.0, OpenXR and Photon PUN 2, and are
connected over Photon.

## Setup

1. Open `Local/` and `Remote/` as separate projects in Unity 6000.1.13f1. Unity rebuilds `Library/`
   on first open.
2. **Photon App ID**: the App ID was removed from the repository. Create a Photon PUN app at
   <https://dashboard.photonengine.com> and enter its App ID in
   `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`
   (or *Window → Photon Unity Networking → Highlight Server Settings*) in **both** projects.
3. **Final IK (Remote only)**: the Remote avatar (`Assets/Prefabs/Player.prefab`) uses
   [Final IK](https://assetstore.unity.com/packages/tools/animation/final-ik-14290) by RootMotion,
   a paid Asset Store package that is not included. Import it into `Remote/Assets/Plugins/RootMotion/`.
4. The Remote side streams the room point cloud through the Flowbit server plugin in
   `Remote/Assets/DetermFlowbitServer/` (Windows x64 only).

## Scenes

- `Assets/Scenes/Launcher.unity`: connects to Photon
- `Assets/Scenes/RemoteCollaborationLocalScene.unity`: main study scene (same name in both projects)
