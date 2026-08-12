# STS2 커스텀 카드 만들기 (쉬운 버전)

원문: [모드 만들기 - 4. 캐릭터 모드 (2) 카드 만들기](https://gall.dcinside.com/mgallery/board/view/?id=slay&no=317149)

전제: **캐릭터 모드 (1)** 까지 끝난 상태 + BaseLib 캐릭터 템플릿이 있음.

---

## 한 줄 요약

1. VSCode에 카드 스니펫 넣기  
2. `customcard` 치고 카드 클래스 생성  
3. 로컬라이즈(`cards.json`) 채우기  
4. 효과 코드 작성 (`OnPlay` / `OnUpgrade`)  
5. 일러스트 2장 넣기  
6. `[Pool(...)]` 로 카드풀 등록  
7. `dotnet publish` 후 테스트  

---

## 0) VSCode 스니펫 (한 번만)

1. VSCode → **파일 → 기본 설정 → 코드 조각 구성**
2. 모드 프로젝트용 새 스니펫 파일 만들기
3. 아래 파일 내용 통째로 붙여넣기:

`snippets/sts2-card.code-snippets` (이 폴더에 동일 내용 있음)

이후 `.cs` 파일에서 `customcard` 입력 → Enter 하면 카드 뼈대가 생김.

JetBrains(Rider)면 `.DotSettings`를 LLM에게  
「내가 쓰는 에디터 스니펫으로 바꿔줘」라고 하면 됨.

---

## 1) 카드 클래스 만들기

1. 카드용 `.cs` 파일 생성
2. `customcard` 입력 → Enter
3. `MyCard` → 원하는 이름 (예: `Reaper`)
4. 빨간 줄에 커서 → **빠른 수정**
5. **Generate localization that must be copied to 'cards.json'** 클릭
6. 나온 JSON을 `localization/eng/cards.json`에 붙여넣기  
   (한국어면 `localization/kor/cards.json`도 같이)

### 생성자 숫자/옵션 의미

```text
Card(비용, CardType, CardRarity, TargetType)
```

| 항목 | 예시 | 뜻 |
|------|------|----|
| 비용 | `2` | 에너지 |
| CardType | `Attack` | Attack / Skill / Power / Status / Curse / Quest |
| CardRarity | `Rare` | Basic / Common / Uncommon / Rare / Ancient / Event / Token ... |
| TargetType | `AllEnemies` | Self / AnyEnemy / AllEnemies / Osty(골골이) / ... |

- `CanonicalVars` = 데미지·방어도 같은 숫자
- `OnPlay` = 카드 낼 때 실행
- `OnUpgrade` = 강화됐을 때 수치 변경

---

## 2) 효과 작성 예시 (사신)

목표: **전체 적에게 피해 → 막히지 않은 피해만큼 힐 + 소멸**

```csharp
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace CharModTest;

[Pool(typeof(IroncladCardPool))] // ← 안 넣으면 카드가 풀에 안 뜸
public class Reaper() : CharModTest.CharModTestCode.Cards.CharModTestCard(
    2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(4m, DamageProps.card) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState, nameof(CombatState));

        // 전체 적: Targeting(play.Target) 쓰지 말고 이거
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // 막힌 피해 제외 → UnblockedDamage
        if (cardSource == this && result.UnblockedDamage > 0)
        {
            await CreatureCmd.Heal(base.Owner.Creature, result.UnblockedDamage);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
```

### 원문에서 강조한 함정

| 잘못된 것 | 올바른 것 |
|-----------|-----------|
| 전체 적에 `Targeting(play.Target)` | `TargetingAllOpponents(CombatState)` |
| 힐에 `TotalDamage` | `UnblockedDamage` (원본 사신과 동일) |
| `[Pool]` 안 붙임 | 카드풀에 안 나와서 “없는 카드” |

데미지 숫자는 `DamageProps.card` / `BlockProps` 쓰는 게 `ValueProp.Move`보다 직관적.

골골이 회복은 `CreatureCmd.Heal(...)` 패턴을 참고.

---

## 3) 일러스트

일반 카드(고대 아님):

| 용도 | 크기 |
|------|------|
| 큰 그림 (`big/`) | **1000×760** (또는 500×380) |
| 작은 그림 | **250×190** |

고대 카드면:

- 큰: 606×852  
- 작은: 250×350  

경로 예:

```text
card_portraits/
  reaper.png          ← 작은 것
  big/reaper.png      ← 큰 것
```

(`big` 안 = 큰 이미지, 바깥 = 작은 이미지)

---

## 4) 텍스트 (`cards.json`)

```json
{
  "REAPER.title": "사신",
  "REAPER.description": "적 전체에게 {Damage:diff()} 피해를 줍니다. 막히지 않은 피해만큼 회복합니다. 소멸."
}
```

자주 쓰는 치환자:

- `{Damage:diff()}` 데미지
- `{Block:diff()}` 방어도
- `{Energy:energyIcons()}` 에너지 아이콘

소멸/선천성 같은 키워드는 설명에 안 적어도 키워드로 자동 표시되는 경우가 많음.

---

## 5) 빌드 & 테스트

```bash
dotnet publish
```

게임 실행 → 해당 캐릭터/카드풀에서 카드 확인.

---

## 체크리스트

- [ ] 스니펫으로 클래스 생성
- [ ] `cards.json` 로컬라이즈 넣음
- [ ] `OnPlay` 효과 작성
- [ ] 전체 적이면 `TargetingAllOpponents`
- [ ] 사신형 힐이면 `UnblockedDamage`
- [ ] 일러스트 big/small 넣음
- [ ] `[Pool(typeof(...CardPool))]` 붙임
- [ ] `dotnet publish` 후 인게임 확인

---

## 원문 시리즈

- 0. 기초 세팅  
- 0.5. 파일 언팩  
- 1. HarmonyLib  
- 2. 원하는 노드 찾기  
- 3. 캐릭터 모드 (1)  
- **4. 캐릭터 모드 (2) 카드 만들기** ← 이 글  

이 문서는 원문 흐름을 “따라만 하면 되는 순서”로 재구성한 것입니다.
