# 성진우 네크로바인더 스킨 (슬레이 더 스파이어 2)

슬더스2 **네크로바인더**를 《나 혼자만 레벨업》 **성진우(그림자 군주)** 스킨으로 교체하는 모드입니다.

## 포함 내용

| 구성 | 설명 |
|------|------|
| 캐릭터 선택 일러스트 | `char_select_necrobinder.png` 교체 |
| 이름/설명 | 한국어·영어 로컬라이즈 (성진우 / Sung Jin-Woo) |
| 카드 일러스트 팩 | Strike/Defend 포함 주요 네크로바인더 카드 ~30장 |
| DLL (선택) | Harmony 패치로 이름 표시 보강 |
| Spine 슬롯 | 전투 스켈레톤 드롭인 경로 제공 |

전투 중 **완전 애니메이션 교체**는 Spine(`.skel/.atlas/.png`) 파일이 필요합니다. 아래 CustomSkeletonLoader / PCK 슬롯에 넣으면 됩니다.

## 설치 방법

### 1) 메인 스킨 모드

1. `release/mods/JinwooNecrobinderSkin/` 폴더 전체를  
   `Slay the Spire 2/mods/JinwooNecrobinderSkin/` 으로 복사합니다.
2. 폴더 안에 최소 다음이 있어야 합니다.
   - `JinwooNecrobinderSkin.json`
   - `JinwooNecrobinderSkin.pck`
   - `preview.png` (Skin Manager 미리보기용, 선택)
   - `JinwooNecrobinderSkin.dll` (있으면 이름 패치 활성)

### 2) 카드 일러스트 (Custom Card Texture Loader)

1. [Custom Card Texture Loader](https://github.com/PhantomGamers/CustomCardTextureLoader) 를 설치합니다.
2. `release/mods/CustomCardTextures/` 를  
   `Slay the Spire 2/mods/CustomCardTextures/` 로 복사합니다.  
   (또는 게임 루트의 `CustomCardTextures/necrobinder/` 로 복사)

### 3) (권장) Skin Manager

여러 캐릭터 스킨을 쓰는 경우 [Sts2SkinManager](https://github.com/skay138/Sts2SkinManager) 로  
네크로바인더 드롭다운에서 **JinwooNecrobinderSkin** 을 선택하세요.

### 4) (선택) 전투 Spine — CustomSkeletonLoader

1. [Custom Skeleton Loader](https://www.nexusmods.com/slaythespire2/mods/505) 설치
2. 이 모드의 `ReplaceResources/necrobinder/` 에 Spine export 배치:

```
ReplaceResources/necrobinder/
  necrobinder.atlas
  necrobinder.skel
  necrobinder.png
```

## 빌드 (개발자)

필요: .NET 9 SDK, (선택) 게임 설치 경로의 `sts2.dll` / `0Harmony.dll`

```bash
# 스텁으로 컴파일 검증 + PCK 생성
export PATH="$HOME/.dotnet:$PATH"
bash scripts/build.sh

# 실제 게임 DLL로 런타임용 빌드
export STS2_DIR="/path/to/Slay the Spire 2"
dotnet build JinwooNecrobinderSkin.csproj -c Release
bash scripts/pack_pck.sh
```

> 스텁으로 만든 DLL은 게임 로더가 `ModInitializer` 를 인식하지 못할 수 있습니다.  
> **배포용 DLL은 반드시 실제 `sts2.dll` 을 참조해 다시 빌드**하세요.  
> PCK + CustomCardTextures 만으로도 선택 화면/카드 스킨은 동작합니다.

## 폴더 구조

```
JinwooNecrobinderSkin/
  assets/                 # 원본 아트
  localization/           # kor / eng
  src/                    # C# Harmony 모드
  scripts/build.sh
  scripts/pack_pck.sh
  ReplaceResources/       # CustomSkeletonLoader 슬롯
  release/mods/           # 설치용 산출물
```

## 크레딧

- 컨셉: Solo Leveling / 성진우 × STS2 Necrobinder
- 아트: 프로시저럴 생성 후 모드 패키징
- 호환: STS2 Early Access, Sts2SkinManager, CustomCardTextureLoader, CustomSkeletonLoader
