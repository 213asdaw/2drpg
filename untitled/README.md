# untitled (slash) — 불꽃의 검 / 검기 플러그인

## 변경 (1.0-SNAPSHOT 수정)

**원인:** 검기 이펙트용 **아머스탠드(아이템 거치대) 손에 FLINT(리소스팩 검기)** 를 장착하고 있었음.  
플레이어가 검기에 **우클릭**하면 마크 기본 동작으로 거치대 아이템이 바뀌거나, 손 무기가 검기로 바뀐 것처럼 보이는 현상.

**수정:**
- **FLINT(리소스팩 검기) 이펙트 유지** — 아머스탠드에 FLINT 장착
- 검기 마커 **우클릭/아머스탠드 조작 이벤트 차단** + 매 틱 FLINT 장착 복구
- 검기 사용 후 손이 FLINT로 바뀌면 **자동 복구**

리소스팩 `custom_model_data` 값이 다르면 `plugins/slash/config.yml` 의 `wave-visual.custom-model-data` 를 수정하세요.

## 서버 적용

```
plugins/untitled-1.0-SNAPSHOT.jar  ← target/untitled-1.0-SNAPSHOT.jar 로 교체
서버 재시작
```

## 스킬 (변경 없음)

| 입력 | 효과 |
|------|------|
| **불꽃의 검** 우클릭 | 불꽃의 검기 (데미지 = Skript `{공격력}` × 1.5 + 5) |
| **웅크린 채** 우클릭 | 불꽃 방패 (10초, 저항 III) |

필요 아이템: 다이아 검, 이름 **불꽃의 검**

## 빌드

```bash
cd untitled && mvn package
```

출력: `untitled/target/untitled-1.0-SNAPSHOT.jar`
