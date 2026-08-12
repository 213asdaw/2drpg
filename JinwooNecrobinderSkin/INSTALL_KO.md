# 빠른 설치 (v1.2.0 — 그림도 바뀌게 수정)

이름만 바뀌고 그림이 안 바뀌던 문제: 예전 ZIP은 PNG만 넣어서 게임이 무시했습니다.  
이번 버전은 Godot `.ctex` 변환을 포함합니다.

## 다운로드

https://github.com/213asdaw/2drpg/raw/cursor/jinwoo-necrobinder-skin-b503/releases/JinwooNecrobinderSkin-v1.2.0.zip

## 필수 (둘 다)

1. **이 스킨 ZIP** (위 링크)
2. **카드/선택화면 로더 중 하나**
   - 추천: [Custom Card Texture Loader SG](https://www.nexusmods.com/slaythespire2/mods/471)  
   - 또는: [Custom Card Texture Loader](https://www.nexusmods.com/slaythespire2/mods/264)

## 설치 순서

1. 위 ZIP 압축 해제
2. `mods/JinwooNecrobinderSkin` → `Slay the Spire 2/mods/`
3. `mods/CustomCardTextures` → `Slay the Spire 2/mods/`  
   (로더 SG를 쓰면, `CustomCardTextures` 내용을  
   `mods/CustomCardTextureLoaderSG/CustomCardTextures/` 안으로 복사해도 됨)
4. 로더 모드도 `mods/`에 설치
5. **게임 완전 종료 후 재실행**
6. 네크로바인더 선택 → 성진우 그림 / 이그리트 이름 확인

## 그래도 그림이 안 바뀌면

- 옛 `JinwooNecrobinderSkin` 폴더 삭제 후 v1.2.0으로 다시 설치
- 로더 설정에서 Texture Replacement = ON
- 전투 캐릭터 모션은 Spine 파일이 있어야 바뀜 (이름·선택화면·카드만 기본 지원)
