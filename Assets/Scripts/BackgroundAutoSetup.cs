using UnityEngine;

/// <summary>
/// 씬을 직접 구성하지 않아도, 게임을 실행(Play)하면 멋진 다층 패럴랙스 배경을
/// 자동으로 만들어 주는 부트스트랩.
/// 어떤 씬에서든 재생하면 BeforeSceneLoad 이후 자동 실행된다.
/// 배경 이미지는 Resources/Background/ 에서 불러온다.
/// (직접 씬을 꾸미고 싶다면 이 스크립트가 만든 "__AutoBackground" 오브젝트를 참고하거나,
///  README의 수동 설정 방법을 따르면 된다.)
/// </summary>
public static class BackgroundAutoSetup
{
    const float PixelsPerUnit = 100f;
    const string RootName = "__AutoBackground";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Build()
    {
        // 이미 만들어져 있으면 중복 생성하지 않는다.
        if (GameObject.Find(RootName) != null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }
        cam.orthographic = true;

        var root = new GameObject(RootName);

        // 하늘(가장 뒤): 화면을 꽉 채운다.
        Transform sky = CreateLayer(root.transform, "Sky", "Background/bg_sky", -30);
        if (sky != null)
            sky.gameObject.AddComponent<BackgroundFill>().fitMode = BackgroundFill.FitMode.Cover;

        // 먼 산(중간): 카메라를 많이 따라가 멀리 있는 느낌.
        Transform mountains = CreateLayer(root.transform, "Mountains", "Background/bg_mountains", -20);
        if (mountains != null)
        {
            mountains.localScale = new Vector3(1.3f, 1.3f, 1f);
            mountains.gameObject.AddComponent<ParallaxLayer>().parallaxFactor = 0.6f;
        }

        // 전경 숲(가장 앞): 카메라를 덜 따라가 빠르게 흘러가는 느낌.
        Transform forest = CreateLayer(root.transform, "Forest", "Background/bg_forest", -10);
        if (forest != null)
        {
            forest.localScale = new Vector3(1.3f, 1.3f, 1f);
            forest.gameObject.AddComponent<ParallaxLayer>().parallaxFactor = 0.2f;
        }
    }

    static Transform CreateLayer(Transform parent, string name, string resourcePath, int sortingOrder)
    {
        // 임포트 설정(Sprite/Texture)에 상관없이 동작하도록 Texture2D 로 불러와 런타임에 Sprite 를 만든다.
        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning("[BackgroundAutoSetup] 텍스처를 찾을 수 없습니다: Resources/" + resourcePath);
            return null;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        sr.sortingOrder = sortingOrder;

        return go.transform;
    }
}
