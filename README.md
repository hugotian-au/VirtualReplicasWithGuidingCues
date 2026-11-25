# Beyond Virtual Replicas: Automatic Spatial Guidance and Gaze Sharing in Mixed Reality Remote Collaboration

Unity source code for the second user study: a mixed-reality remote collaboration system where a
**Remote Expert** (VR, Meta Quest Pro) guides a **Local User** (AR, Meta Quest 3) through assembling
physical Soma cube puzzles, using tracked virtual replicas, automatic spatial guiding cues and
eye-gaze sharing.

Study conditions (selected on the Remote side's experiment panel):

| Condition | Content |
|-----------|---------|
| Training | Practice session |
| Virtual Replicas (C1) | Baseline: tracked virtual replicas + shared room representation |
| GuidingCues (C2) | C1 + automatic translation/rotation guiding cues |
| Cues and EyeGaze (C3) | C2 + eye-gaze visualisation of the partner |

## Repository layout

```
Local/    Unity project for the Local User (Meta Quest 3, passthrough AR)
Remote/   Unity project for the Remote Expert (Meta Quest Pro, VR, eye tracking, point cloud)
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
