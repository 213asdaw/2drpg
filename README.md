# 2drpg

Unity로 만든 2D 방치형 RPG 프로토타입입니다. 별도 에셋 없이 C# 코드가
카메라, 2D 캐릭터, 적, HUD를 런타임에 생성하므로 Unity 에디터에서 바로
플레이 테스트할 수 있습니다.

## 권장 Unity 버전

- Unity 2022.3 LTS
- Unity 6에서도 프로젝트 업그레이드를 허용하면 열 수 있습니다.

## 주요 기능

- Unity 2D SpriteRenderer 기반 전투 화면
- 달빛, 구름, 나무, 풀, 반딧불이를 코드로 생성하는 2D 숲 배경
- 자동 공격과 직접 공격 버튼
- 골드, 경험치, 레벨업, 스테이지 진행
- 공격력, 최대 HP, 회복력, 치명타 강화
- 골드 기반 유물 뽑기와 희귀 이상 보정
- 유물 중복 획득 시 레벨업 및 영구 능력치 보너스
- `PlayerPrefs` 저장 및 오프라인 보상
- 에디터 메뉴를 통한 저장 데이터 초기화

## 실행 방법

1. Unity Hub에서 이 저장소 폴더를 프로젝트로 추가합니다.
2. Unity 2022.3 LTS로 프로젝트를 엽니다.
3. 빈 씬 상태에서 Play를 누릅니다.

`IdleRpgGame`이 런타임에 자동으로 생성되므로 별도 씬 설정 없이 바로
전투 화면과 HUD가 표시됩니다.

## 가로 화면으로 보기

게임은 실행 시 `LandscapeLeft`와 1280x720 해상도를 적용합니다.

Unity 에디터에서 Game 탭이 세로처럼 보이면 Game 탭 상단의 해상도/비율
드롭다운에서 다음 중 하나를 선택하세요.

```text
16:9
1920x1080
1280x720
```

## 저장 데이터 초기화

Unity 상단 메뉴에서 다음을 실행하세요.

```text
Idle RPG > Reset Save Data
```

## 코드 구조

- `Assets/Scripts/IdleRPG/IdleRpgGame.cs`: 전투 루프, 저장, HUD, 2D 화면 생성
- `Assets/Scripts/IdleRPG/IdleRpgState.cs`: 저장 가능한 게임 상태 모델
- `Assets/Scripts/IdleRPG/IdleRpgBalance.cs`: 적/강화/유물 뽑기 밸런스 데이터
- `Assets/Editor/IdleRpgEditorMenu.cs`: 에디터 저장 데이터 초기화 메뉴
