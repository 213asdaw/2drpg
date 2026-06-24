# 2drpg

A 2D fighting game built with **Unity 6000.4.11f1**.

## Cursor Cloud specific instructions

### Repository layout note (important)
- The `main` branch is currently almost empty (just this file and `README.md`). The actual Unity
  project lives on feature branches, e.g. `origin/cursor/2d-fighting-game-c687`. To work on the game,
  checkout that branch directly or export its tree to a working directory.

### Unity Editor
- This project targets **Unity 6000.4.11f1** (changeset `b0a1d6caadd2`).
- Open the project with Unity Hub using editor version **6000.4.11f1**.

### Licensing (REQUIRED before any build / test / play)
- Unity refuses to do anything without an activated license.
- Activate with `unity-editor -batchmode -nographics -logFile - -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -quit`
  (append `-serial "$UNITY_SERIAL"` for Pro).

### Running headlessly
- Use `-batchmode -nographics` for headless editor operations.
- `Library/` is generated on first open and not committed.
