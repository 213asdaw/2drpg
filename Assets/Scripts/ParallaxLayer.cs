using UnityEngine;

/// <summary>
/// 패럴랙스(다층 스크롤) 배경 레이어. 각 배경 레이어 오브젝트에 붙인다.
/// 카메라가 움직일 때 레이어마다 다른 속도로 따라 움직여 깊이감을 만든다.
/// 멀리 있는 레이어(하늘)는 카메라를 거의 그대로 따라가 화면에서 천천히 움직이고,
/// 가까운 레이어(전경 숲)는 덜 따라가 화면에서 빠르게 흘러간다.
/// 카메라가 멈춰 있어도 천천히 흐르게 하는 자동 스크롤(구름 등)도 지원한다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("기준 카메라. 비워두면 Camera.main 을 사용한다.")]
    public Camera targetCamera;

    [Range(0f, 1f)]
    [Tooltip("카메라 이동을 따라가는 정도. 1 = 가장 멀리(화면에 거의 고정), 0 = 가장 가까이(완전히 흘러감).")]
    public float parallaxFactor = 0.5f;

    [Tooltip("세로(상하) 이동도 따라갈지 여부. 횡스크롤 RPG라면 보통 꺼둔다.")]
    public bool followVertical = false;

    [Tooltip("카메라가 멈춰 있어도 가로로 흐르는 속도(월드 단위/초). 구름 등에 사용. 0이면 사용 안 함.")]
    public float autoScrollSpeed = 0f;

    SpriteRenderer _spriteRenderer;
    Vector3 _layerStart;
    Vector3 _camStart;
    float _autoOffset;
    float _loopWidth;

    void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _layerStart = transform.position;

        Camera cam = ResolveCamera();
        _camStart = cam != null ? cam.transform.position : new Vector3(0f, 0f, 0f);

        _autoOffset = 0f;
        _loopWidth = ComputeLoopWidth();
    }

    void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        Vector3 camDelta = new Vector3(
            cam.transform.position.x - _camStart.x,
            cam.transform.position.y - _camStart.y,
            0f);

        // 자동 스크롤 오프셋을 누적하고, 한 장 폭을 넘어가면 되감아 끊김 없이 반복시킨다.
        // (이음매 없는(seamless) 이미지를 Tiled 모드로 넓게 깔아두면 되감기가 보이지 않는다.)
        _autoOffset += autoScrollSpeed * Time.deltaTime;
        if (_loopWidth > 0f)
        {
            while (_autoOffset > _loopWidth) _autoOffset -= _loopWidth;
            while (_autoOffset < -_loopWidth) _autoOffset += _loopWidth;
        }

        float x = _layerStart.x + camDelta.x * parallaxFactor + _autoOffset;
        float y = followVertical ? _layerStart.y + camDelta.y * parallaxFactor : _layerStart.y;
        transform.position = new Vector3(x, y, _layerStart.z);
    }

    Camera ResolveCamera()
    {
        return targetCamera != null ? targetCamera : Camera.main;
    }

    float ComputeLoopWidth()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null)
            return 0f;
        return _spriteRenderer.sprite.bounds.size.x * transform.localScale.x;
    }
}
