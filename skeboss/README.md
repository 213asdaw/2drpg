# SkeBoss — ModelEngine 해골 보스 플러그인

말씀하신 세 가지 문제를 반영한 **수정 예시 코드**입니다.

| 문제 | 원인 | 수정 |
|------|------|------|
| 모델이 좀비랑 겹침 | 바닐라 좀비가 그대로 보임 | `setBaseEntityVisible(false)` |
| 몸이 반대 | yaw / 모델 facing 불일치 | 스폰 시 `yaw-offset` (기본 180°) |
| 스킬 후 원래대로 안 돌아옴 | idle 재생 누락 | 스킬 끝에 `stopAnimation` + `idle` 재생 |

## 빌드 방법 (IntelliJ IDEA)

1. 서버 `plugins` 폴더에서 **ModelEngine JAR** 복사  
   → `skeboss/libs/ModelEngine-R4.0.4.jar`  
   (버전이 다르면 `pom.xml`의 `systemPath` 수정)

2. IntelliJ에서 `skeboss/pom.xml` → **Maven 프로젝트로 열기**

3. 터미널:
   ```bash
   cd skeboss
   mvn package
   ```
   결과: `target/skeboss-1.0-SNAPSHOT.jar`

4. JAR를 서버 `plugins/`에 넣고 재시작

## 설정 (`config.yml`)

```yaml
model-id: ske_boss          # ModelEngine blueprint 이름
animations:
  idle: idle
  skill: skill_attack
yaw-offset: 180             # 방향이 맞으면 0으로 변경
```

## 테스트 명령어

- `/skeboss spawn` — 보스 스폰
- `/skeboss skill` — 바라보는 보스 스킬 (끝나면 idle 복귀)
- `/skeboss remove` — 보스 제거

## 핵심 코드 위치

`ModelEngineBossService.java` — 스폰·스킬·복귀 로직 전부 여기 있습니다.

기존 프로젝트에 붙일 때는 **해당 클래스의 spawn / finishSkill 부분**만 복사해도 됩니다.

## ModelEngine 버전

이 예시는 **ModelEngine R4** API 기준입니다. R3를 쓰면 import/API 이름이 조금 다를 수 있습니다.
