using UnityEngine;

namespace FightingGame
{
    public static class FightSceneBuilder
    {
        public static Camera BuildStage()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 4.5f;
            mainCamera.transform.position = new Vector3(0f, 0.15f, -10f);
            mainCamera.backgroundColor = new Color(0.55f, 0.62f, 0.72f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;

            GameObject background = new GameObject("Background");
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = ProceduralArt.CreateStageBackground();
            backgroundRenderer.sortingOrder = -20;
            background.transform.localScale = new Vector3(1.45f, 1.45f, 1f);
            background.transform.position = new Vector3(0f, 0.35f, 5f);

            GameObject arenaPlatform = new GameObject("Arena Platform");
            SpriteRenderer arenaRenderer = arenaPlatform.AddComponent<SpriteRenderer>();
            arenaRenderer.sprite = ProceduralArt.CreateArenaPlatform();
            arenaRenderer.sortingOrder = -8;
            arenaPlatform.transform.position = new Vector3(0f, FightConstants.GroundY - 0.05f, 0f);
            arenaPlatform.transform.localScale = new Vector3(FightConstants.ArenaHalfWidth * 2.05f, 1.15f, 1f);

            GameObject floor = new GameObject("Floor Edge");
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = ProceduralArt.CreateRectSprite(256, 16, new Color(0.72f, 0.58f, 0.38f), "Floor");
            floorRenderer.sortingOrder = -9;
            floor.transform.position = new Vector3(0f, FightConstants.GroundY - 0.28f, 0f);
            floor.transform.localScale = new Vector3(FightConstants.ArenaHalfWidth * 2.2f, 0.28f, 1f);

            return mainCamera;
        }

        public static void ConfigureDisplay()
        {
            Application.runInBackground = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        public static FighterController CreateFighter(int index, Vector3 position, FighterArchetypeId archetype)
        {
            FighterArchetypeDefinition definition = FighterArchetypes.Get(archetype);
            GameObject fighterObject = new GameObject(definition.DisplayName);
            FighterController fighter = fighterObject.AddComponent<FighterController>();
            fighter.Initialize(index, archetype, position);
            return fighter;
        }

        public static FighterController CreateFighter(string name, int index, Color primary, Color accent, Vector3 position)
        {
            GameObject fighterObject = new GameObject(name);
            FighterController fighter = fighterObject.AddComponent<FighterController>();
            fighter.Initialize(index, name, primary, accent, position);
            return fighter;
        }
    }
}
