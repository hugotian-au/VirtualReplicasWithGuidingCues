# Virtual Replicas with Automated Guidance and Head-and-Gaze Visualization

Unity source code of the system used in the study reported in the manuscript *"Automated guidance
and head-and-gaze visualization produced mixed feedback but no reliable gains over virtual
replicas in asymmetric mixed reality remote collaboration"* (Tian et al.).

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

Both projects use **Unity 6000.1.13f1**, the Meta XR SDK, OpenXR and Photon PUN 2, and are connected
over Photon. The Meta XR All-in-One SDK (`com.meta.xr.sdk.all`) is version **78.0.0** in `Local/` and
**83.0.0** in `Remote/`.

## Setup

The Meta XR SDK, Photon PUN 2, Final IK and the Determ Flowbit plugin are not stored in this
repository. Obtain them as described below; each remains subject to its own licence.

1. Open `Local/` and `Remote/` as separate projects in Unity 6000.1.13f1. Unity rebuilds `Library/`
   on first open. Until step 3 (and, for `Remote/`, steps 4 and 5) is done, Unity reports compile
   errors or missing references; if it offers Safe Mode, use it to import the missing packages.
2. **Meta XR SDK**: declared in each project's `Packages/manifest.json`, so the Unity Package Manager
   downloads it automatically. Keep the versions listed above. The configuration files under
   `Assets/Oculus/`, `Assets/MetaXR/` and `Assets/Resources/` are this project's settings and are
   included.
3. **Photon PUN 2**: in **both** projects, import
   [PUN 2 - FREE](https://assetstore.unity.com/packages/tools/network/pun-2-free-119922) from the
   Unity Asset Store (the projects use PUN 2.52). Keep the included
   `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`, which holds this
   project's settings and RPC list. The App ID was removed from it: create a Photon PUN app at
   <https://dashboard.photonengine.com> and enter its App ID in that file (or *Window → Photon Unity
   Networking → Highlight Server Settings*) in both projects.
4. **Final IK (Remote only)**: the Remote avatar (`Assets/Prefabs/Player.prefab`) uses
   [Final IK](https://assetstore.unity.com/packages/tools/animation/final-ik-14290) by RootMotion,
   a paid Asset Store package that is not included. Import it into `Remote/Assets/Plugins/RootMotion/`.
5. **Determ Flowbit Server Unity plugin (Remote only)**: the Remote side streams the room point
   cloud through this third-party plugin (v1.4.0, Windows x64 only), which is not included. Obtain
   it from Determ Technology and import it into `Remote/Assets/DetermFlowbitServer/`, then
   configure its `Config/` folder for your own cameras.

## Scenes

- `Assets/Scenes/Launcher.unity`: connects to Photon
- `Assets/Scenes/RemoteCollaborationLocalScene.unity`: main study scene (same name in both projects)
