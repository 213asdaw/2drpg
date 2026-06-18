# 2drpg

Unity로 만드는 2D RPG 프로젝트입니다.

## 개발 환경

- **엔진:** Unity (권장 버전: `2022.3 LTS`, `ProjectSettings/ProjectVersion.txt` 참고)
- **템플릿:** 2D (`com.unity.feature.2d`)

## 프로젝트 열기

1. [Unity Hub](https://unity.com/download)를 설치합니다.
2. Unity Hub에서 `Add` → 이 저장소 폴더를 선택합니다.
3. 설치된 Unity 버전이 `ProjectVersion.txt`와 다르면 Hub가 업그레이드/열기를 안내합니다(그대로 진행하면 됩니다).
4. Unity가 `Library/` 등 나머지 파일을 자동으로 생성합니다.

## 멋진 배경 (다층 패럴랙스)

판타지 RPG 느낌의 배경 아트와, 깊이감을 주는 **패럴랙스(여러 겹이 다른 속도로 스크롤)** 시스템이 들어 있습니다.

### 포함된 배경 아트 (`Assets/Sprites/Background/`)

| 파일 | 역할 | 비고 |
| --- | --- | --- |
| `bg_sky.png` | 하늘(가장 뒤) | 노을 하늘 · 불투명 |
| `bg_mountains.png` | 먼 산(중간) | 투명 배경 |
| `bg_forest.png` | 전경 숲(가장 앞) | 투명 배경 |

> 산/숲 이미지는 투명 영역이 있으므로 Unity에서 **Texture Type = `Sprite (2D and UI)`** 로 임포트되어야 합니다. (2D 템플릿은 기본값입니다.)

### 스크립트

- `Assets/Scripts/BackgroundFill.cs` — 배경을 화면에 빈틈없이 채웁니다. (하늘 레이어에 사용)
- `Assets/Scripts/ParallaxLayer.cs` — 카메라가 움직일 때 레이어마다 다른 속도로 따라 움직여 깊이감을 만듭니다.

### 설정 방법

1. 카메라를 **Orthographic(직교)** 으로 둡니다. (2D 템플릿 기본값)
2. 레이어별로 빈 게임오브젝트를 만들고 `Sprite Renderer` 를 추가한 뒤 위 이미지를 각각 넣습니다.
3. `Order in Layer` 로 앞뒤 순서를 정합니다: 하늘 `-30`, 산 `-20`, 숲 `-10`.
4. **하늘** 오브젝트: `BackgroundFill` 추가 → `Fit Mode = Cover` (화면 꽉 채움).
5. **산 / 숲** 오브젝트: `ParallaxLayer` 추가 → `Parallax Factor` 설정.
   - 산 ≈ `0.6`, 숲 ≈ `0.2` (값이 클수록 멀리 있는 느낌)
   - 구름처럼 가만히 있어도 흐르게 하려면 `Auto Scroll Speed` 를 살짝 줍니다.

### 옵션

- `BackgroundFill.Fit Mode`
  - **Cover (기본/추천):** 비율 유지하며 화면을 꽉 채움(가장자리 약간 잘릴 수 있음).
  - **Stretch:** 화면에 정확히 맞춰 늘림(빈 공간 없음, 이미지가 늘어날 수 있음).
- `ParallaxLayer`
  - **Parallax Factor (0~1):** 1=가장 멀리(화면에 거의 고정), 0=가장 가까이(완전히 흘러감).
  - **Follow Vertical:** 상하 이동도 따라갈지 여부.
  - **Auto Scroll Speed:** 카메라가 멈춰 있어도 가로로 흐르는 속도(구름 등).

`Target Camera` 를 비워 두면 `Camera.main`(Tag 가 `MainCamera` 인 카메라)을 자동으로 사용합니다.
