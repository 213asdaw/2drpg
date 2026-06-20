# 2D Fighting Game

Unity **6000.4.11f1** 로 만든 **2D 격투 게임** 프로토타입입니다.

빈 씬에서 **Play** 를 누르면 메인 메뉴가 뜨고, **오프라인(로컬 2P)** 또는 **온라인 대전**을 선택할 수 있습니다.

## 게임 규칙

- **2인 대전** (오프라인: 같은 PC / 온라인: Relay + Lobby)
- **3선승제** (2라운드 선승 시 매치 승리)
- 라운드당 **99초** 제한 시간
- HP 가 0 이 되면 **KO**
- **가드(S / ↓)** 시 받는 피해 75% 감소

## Unity Gaming Services 설정 (온라인 필수, 1회)

온라인 매칭(Relay + Lobby)을 쓰려면 Unity 프로젝트를 UGS에 연결해야 합니다.

1. [Unity Dashboard](https://dashboard.unity3d.com/) 에서 프로젝트 생성/선택
2. **Multiplayer > Lobby**, **Multiplayer > Relay** API 활성화
3. Unity Editor: **Edit > Project Settings > Services** 에서 같은 프로젝트 연결
4. Play 후 익명 로그인으로 자동 인증 (별도 계정 UI 없음)

설정이 안 되어 있으면 메뉴에서 연결 오류 메시지가 표시됩니다.

## 친구와 같이 하는 방법 (중요)

지금은 **Steam 같은 상점 게임이 아니라 Unity 프로젝트**입니다.  
친구가 Unity를 설치할 필요는 없고, **빌드된 실행 파일(exe)** 만 받으면 됩니다.

### 방장(나)이 할 일 — 게임 빌드해서 보내기

1. Unity Editor에서 **File > Build Settings**
2. **PC, Mac & Linux Standalone** 선택 (Windows면 Windows)
3. **Build** → 폴더 선택 → `2DFightingGame.exe` 생성
4. 생성된 **폴더 전체**를 zip으로 압축
5. 친구에게 전송 (카카오톡, 디스코드, 구글 드라이브 등)

> 빌드할 때 **Edit > Project Settings > Services** 에 UGS가 연결되어 있어야  
> 친구 PC에서도 온라인(로비 코드) 접속이 됩니다. 연결 정보는 빌드에 포함됩니다.

### 친구가 할 일 — 게임 켜고 참가

1. zip 받아서 **압축 해제**
2. **`2DFightingGame.exe`** (또는 Mac/Linux 실행 파일) **더블클릭**
3. 메인 메뉴 → **로비 코드** 칸에 방장이 알려준 코드 입력
4. **온라인 — 코드로 참가** 클릭
5. 접속되면 자동으로 대결 시작

Unity 설치, GitHub, IP 입력 **전부 필요 없습니다.**

### 방장이 할 일 — 방 만들기

1. 같은 exe 실행 (또는 Unity Editor에서 Play)
2. **온라인 — 방 만들기** 클릭
3. 화면에 뜨는 **로비 코드** (예: `ABC123`) 를 친구에게 카톡/디스코드로 보내기
4. 친구 접속되면 자동 매치

## 플레이 방법

### 메인 메뉴

| 메뉴 | 설명 |
|------|------|
| **오프라인 (로컬 2P)** | 한 키보드로 2명 |
| **온라인 — 방 만들기** | Relay+Lobby 방 생성 → **로비 코드** 표시 |
| **온라인 — 코드로 참가** | 상대가 알려준 로비 코드 입력 |
| **온라인 — 빠른 매칭** | 열린 방 자동 참가 / 없으면 방 생성 |
| **고급 LAN 접속** | IP 직접 입력 (같은 Wi‑Fi, 포트 7777) |

### 온라인 대전 (Relay + Lobby, 권장)

1. **플레이어 A**: `온라인 — 방 만들기` → 화면에 **로비 코드** 확인 (예: `ABC123`)
2. **플레이어 B**: 코드 입력 → `온라인 — 코드로 참가`
3. 두 명 접속 시 자동 매치 시작

IP/포트 포워딩 없이 **인터넷 상대**도 Relay 경유로 접속 가능합니다.

### 빠른 매칭

- `온라인 — 빠른 매칭` 클릭
- 비어 있는 방이 있으면 자동 참가, 없으면 새 방 생성 후 대기

## 조작법

**오프라인 P1:** `A/D W S J K L` | **U** 불꽃검기 | **I** 용암방어 (**카론**)  
**P2:** 방향키 + `1 2 3`

### 카론 — 불꽃의 검사 (P1 기본 캐릭터)

> 흰 머리, 붉은 눈의 남성 검사. 불꽃이 감싼 대검을 휘든다.

| 항목 | 특성 |
|------|------|
| 체력 | 118 (기본 100보다 높음) |
| 공격 | 약간 느리고, 타격력도 소폭 낮음 |
| 외형 | **흰색 머리**, **붉은 눈**, 검붉은 코트, 금색 장식 갑옷 |
| 무기 | 불꽃이 감싼 **대검** |
| **U — 불꽃 검기** | 전방으로 화염 검기 발사 (쿨 5초) |
| **I — 용암 방어** | 3.5초간 받는 피해 대폭 감소 (쿨 10초) |

## 온라인 아키텍처

- **Unity Lobby + Relay** (via `com.unity.services.multiplayer`) — 로비 코드로 방 찾기, Relay NAT 우회
- **Netcode for GameObjects** — 호스트 권한 전투 판정
- 최대 **2명** 접속

## 프로젝트 구조

```
Assets/Scripts/FightingGame/
  GameLauncher.cs                 # 메인 메뉴
  FightingRelayLobbyService.cs    # Relay + Lobby 연동
  LobbyHeartbeatRunner.cs         # 호스트 로비 유지
  OnlineFightingGame.cs           # 온라인 HUD/세션
  NetworkFighter.cs               # 네트워크 캐릭터
  NetworkMatchCoordinator.cs      # 라운드/매치
  ...
```

## Unity 에서 실행

1. Unity Hub **6000.4.11f1** 로 프로젝트 열기
2. Services 프로젝트 연결 (위 설정 참고)
3. 빈 씬 **Play**

첫 실행 시 Netcode/UGS 패키지 복원으로 시간이 걸릴 수 있습니다.

## 확장 아이디어

- Unity Lobby 검색/친구 초대 UI
- 롤백 넷코드로 입력 지연 개선
- 콤보, 필살기, 캐릭터 선택
