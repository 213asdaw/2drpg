# 2drpg

A 2D RPG game built with **Unity 2022.3.62f1 (LTS)**.

## Cursor Cloud specific instructions

### Repository layout note (important)
- The `main` branch is currently almost empty (just this file and `README.md`). The actual Unity
  project lives on feature branches, e.g. `origin/cursor/2d-idle-rpg-23e9` (the full playable idle
  RPG) and `origin/cursor/unity-2drpg-fullscreen-background-f659`. To work on the game, export a
  branch's tree to a working directory without leaving `main`, for example:
  `git archive origin/cursor/2d-idle-rpg-23e9 | tar -x -C ~/idle-rpg-project`.
- The game is a Unity 2D project (`com.unity.2d.sprite`). `IdleRpgGame` builds the camera, sprites,
  enemies, and HUD at runtime, so you can press Play on an empty scene. See the project branch's
  `README.md` for gameplay/controls (it is written in Korean).

### Unity Editor (pre-installed, persists in the VM snapshot)
- Unity Editor 2022.3.62f1 is installed at `/opt/unity/2022.3.62f1/Editor/Unity`.
- A convenience wrapper is on `PATH`: run `unity-editor ...`.
- It is NOT reinstalled by the update script (a ~4 GB download is too brittle for startup). If the
  snapshot ever loses it, reinstall once with:
  `curl -L -o /tmp/Unity.tar.xz "https://download.unity3d.com/download_unity/4af31df58517/LinuxEditorInstaller/Unity.tar.xz" && sudo mkdir -p /opt/unity/2022.3.62f1 && sudo chown $USER /opt/unity/2022.3.62f1 && tar -xf /tmp/Unity.tar.xz -C /opt/unity/2022.3.62f1`
  (changeset `4af31df58517`). Required Linux libs: `libgtk-3-0 libnss3 libxss1 libasound2t64
  libgbm1 libxtst6 libxrandr2 libxcursor1 libgl1 libglu1-mesa mesa-vulkan-drivers libglib2.0-0
  libnotify4 xvfb x11-utils`.

### Licensing (REQUIRED before any build / test / play — this is the main gotcha)
- Unity refuses to do anything (build, run editmode/playmode tests, open a project headlessly)
  without an activated license. With no license the editor logs:
  `No valid Unity Editor license found. Please activate your license.`
- Provide a Unity account via the `UNITY_EMAIL` and `UNITY_PASSWORD` secrets (a free Unity
  account grants a Personal license; set `UNITY_SERIAL` too only for Pro/Plus). Activate before use:
  `unity-editor -batchmode -nographics -logFile - -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -quit`
  (append `-serial "$UNITY_SERIAL"` for Pro). Activation is per-VM; re-run it if the snapshot lacks
  an active license.
- Manual/offline alternative: `unity-editor -batchmode -nographics -createManualActivationFile -quit`
  produces a `.alf`; upload it at https://license.unity3d.com/manual to get a `.ulf`, then
  `unity-editor -batchmode -nographics -manualLicenseFile <file>.ulf -quit`.

### Running headlessly
- A virtual display is available (`DISPLAY=:1`, Xvfb). Use `-batchmode -nographics` for headless
  editor operations (imports, builds, batchmode tests).
- To run a built Linux standalone player without a real GPU, use Xvfb + software GL:
  `LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a ./<PlayerBinary>`.
- Unity restores its packages into `Library/` on first project open; `Library/` is generated and
  not committed, so the first open is slow.
