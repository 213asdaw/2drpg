using UnityEngine;

/// <summary>
/// 배경 스프라이트를 카메라 화면에 꽉 채워주는 스크립트.
/// 직교(Orthographic) 카메라 기준으로, 어떤 해상도/화면 비율에서도
/// 빈 공간이 보이지 않도록 배경 크기와 위치를 자동으로 맞춘다.
/// 배경으로 쓸 SpriteRenderer 오브젝트에 붙여서 사용한다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFill : MonoBehaviour
{
    public enum FitMode
    {
        // 화면 비율을 무시하고 정확히 화면에 맞춘다(이미지가 늘어날 수 있음).
        Stretch,
        // 화면 비율을 유지하면서 화면을 꽉 채운다(이미지 일부가 잘릴 수 있음).
        Cover
    }

    [Tooltip("배경을 채우는 방식")]
    public FitMode fitMode = FitMode.Cover;

    [Tooltip("배경이 따라갈 카메라. 비워두면 Camera.main 을 사용한다.")]
    public Camera targetCamera;

    SpriteRenderer _spriteRenderer;

    void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Resize();
    }

    void Update()
    {
        // 해상도나 화면 비율이 바뀌어도 항상 꽉 차 있도록 매 프레임 갱신한다.
        Resize();
    }

    /// <summary>배경 크기/위치를 현재 카메라 화면에 맞춰 다시 계산한다.</summary>
    public void Resize()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        Sprite sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        if (sprite == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        // 직교 카메라가 보여주는 월드 영역의 크기.
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth = worldHeight * cam.aspect;

        // 스케일 1일 때의 스프라이트 월드 크기.
        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float scaleX = worldWidth / spriteSize.x;
        float scaleY = worldHeight / spriteSize.y;

        Vector3 scale;
        if (fitMode == FitMode.Stretch)
        {
            scale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            // Cover: 더 큰 배율에 맞추면 비율을 유지하면서 화면을 빈틈없이 채운다.
            float uniform = Mathf.Max(scaleX, scaleY);
            scale = new Vector3(uniform, uniform, 1f);
        }

        transform.localScale = scale;

        // 배경을 카메라 정중앙에 둔다(z 값은 유지해 다른 오브젝트보다 뒤에 있게 한다).
        Vector3 camPos = cam.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }
}
