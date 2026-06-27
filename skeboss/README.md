# SkeBoss 1.0 — ModelEngine 해골 보스 플러그인

처음부터 새로 만든 **완성형** 보스 플러그인입니다.  
기존에 겪던 3가지 문제를 모두 반영했습니다.

| 문제 | 해결 |
|------|------|
| 좀비랑 모델 겹침 | `setBaseEntityVisible(false)` |
| 몸이 반대 | `yaw-offset` (기본 180°) |
| 스킬 후 idle 안 돌아옴 | 스킬 끝 `stopAnimation` + idle 재생 |

## 포함 기능

- ModelEngine 커스텀 모델 보스 스폰
- 자동 AI (추적, 근접 공격, 스킬)
- 스킬 2종 (베기 / 내려찍기) — config에서 추가 가능
- BossBar 체력 표시
- `/skeboss` 명령어

## 빌드 (IntelliJ / Maven)

**ModelEngine JAR 복사 불필요** — 런타임에 서버 plugins 폴더의 ModelEngine을 사용합니다.

```bash
cd skeboss
mvn package
```

결과 JAR: `target/skeboss-1.0-SNAPSHOT.jar`

## 서버 설치

1. `plugins/`에 넣을 것:
   - `skeboss-1.0-SNAPSHOT.jar`
   - `ModelEngine.jar` (이미 있으면 OK)
   - ModelEngine blueprint (`plugins/ModelEngine/blueprints/ske.bbmodel`)

2. 서버 재시작

3. `plugins/SkeBoss/config.yml` 에서 모델/애니메이션 이름 수정

## 명령어

| 명령어 | 설명 |
|--------|------|
| `/skeboss spawn` | 보스 스폰 |
| `/skeboss skill [이름]` | 스킬 테스트 (slash, slam) |
| `/skeboss remove` | 보스 제거 |
| `/skeboss reload` | config 리로드 |

## config.yml 설정 예시

```yaml
model-id: ske_boss       # blueprint 폴더 이름
animations:
  idle: idle
  walk: walk
yaw-offset: 180          # 방향 맞으면 0
skills:
  slash:
    animation: attack  # bbmodel 애니메이션 이름
```

## IntelliJ에서 열기

1. **File → Open** → `skeboss/pom.xml` 선택
2. Maven import 대기
3. 우측 Maven → **Lifecycle → package** 실행
4. `target/skeboss-1.0-SNAPSHOT.jar` 를 서버 `plugins/`에 복사

## 모델 애니메이션 이름 맞추기

Blockbench에서 export한 애니메이션 이름과 `config.yml`의 `animations`, `skills.*.animation` 이 **정확히 같아야** 합니다.  
이름이 다르면 스킬 후 idle 복귀가 안 되는 것처럼 보일 수 있습니다.
