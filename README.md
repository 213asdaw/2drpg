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

## 배경을 화면에 꽉 채우기

`Assets/Scripts/BackgroundFill.cs` 가 어떤 해상도/화면 비율에서도 배경을 빈틈없이 채워 줍니다.

### 사용 방법

1. 카메라를 **Orthographic(직교)** 으로 설정합니다. (2D 템플릿은 기본값이 직교입니다.)
2. 하이어라키에서 빈 게임오브젝트를 만들고 `Background` 라고 이름 짓습니다.
3. 그 오브젝트에 `Sprite Renderer` 컴포넌트를 추가하고, `Sprite` 칸에 배경 이미지를 넣습니다.
4. 같은 오브젝트에 `BackgroundFill` 스크립트를 추가합니다.
5. 배경이 다른 오브젝트보다 뒤에 보이도록 `Sprite Renderer`의 `Order in Layer`를 음수(예: `-10`)로 설정합니다.

### 옵션 (`Fit Mode`)

- **Cover (기본값):** 이미지 비율을 유지하면서 화면을 꽉 채웁니다. 가장자리가 약간 잘릴 수 있습니다. (왜곡 없음, 추천)
- **Stretch:** 이미지를 화면에 정확히 맞춰 늘립니다. 빈 공간은 없지만 이미지가 늘어나 보일 수 있습니다.

`Target Camera` 를 비워 두면 `Camera.main`(Tag가 `MainCamera`인 카메라)을 자동으로 사용합니다.
