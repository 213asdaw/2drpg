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
            mainCamera.transform.position = new Vector3(0f, 0.5f, -10f);
            mainCamera.backgroundColor = new Color(0.08f, 0.09f, 0.14f);

            GameObject background = new GameObject("Background");
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = ProceduralArt.CreateStageBackground();
            backgroundRenderer.sortingOrder = -20;
            background.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            background.transform.position = new Vector3(0f, 0.4f, 1f);

            GameObject floor = new GameObject("Floor");
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = ProceduralArt.CreateRectSprite(256, 16, new Color(0.42f, 0.34f, 0.48f), "Floor");
            floorRenderer.sortingOrder = -10;
            floor.transform.position = new Vector3(0f, FightConstants.GroundY - 0.15f, 0f);
            floor.transform.localScale = new Vector3(FightConstants.ArenaHalfWidth * 2.2f, 0.35f, 1f);

            return mainCamera;
        }

        public static void ConfigureDisplay()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
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
