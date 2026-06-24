# 2D Fighting Game Launcher

친구에게 **런처 exe 하나만** 보내면, 이후 업데이트는 런처가 zip을 받아서 자동 설치합니다.

## 친구에게 처음 보낼 것

1. `2DFightLauncher.exe` (빌드 결과)
2. `launcher-config.json` (exe와 **같은 폴더**)

> exe만 보내면 설정 파일이 없어서 실행되지 않습니다.

## 당신(개발자)이 할 일 — 런처 빌드

Windows PowerShell:

```powershell
cd Launcher
dotnet publish -c Release
```

결과물:

`Launcher/bin/Release/net8.0-windows/win-x64/publish/`

- `2DFightLauncher.exe`
- `launcher-config.json` (복사해서 같이 배포)

친구에게는 **publish 폴더의 exe + json** zip으로 보내면 됩니다.

## 업데이트 배포 방법

### 1) 게임 zip 만들기

Unity: **Fighting Game → Build Windows to Builds Folder**

`Builds/Windows` 폴더 전체를 zip:

`2DFight_v0.1.1.zip`

### 2) zip 업로드

GitHub Releases / Google Drive 등 **직접 다운로드 URL** 확보

### 3) `releases/version.json` 수정 후 push

```json
{
  "version": "0.1.1",
  "downloadUrl": "https://.../2DFight_v0.1.1.zip",
  "notes": "온라인 매칭 수정"
}
```

### 4) Unity Player Settings 버전도 맞추기

**Edit → Project Settings → Player → Version** = `0.1.1`

## 설정 파일

`launcher-config.json`:

| 항목 | 설명 |
|------|------|
| `versionUrl` | `version.json` raw URL |
| `gameExeName` | `2D Fighting Game.exe` |
| `installFolderName` | PC 로컬 설치 폴더 이름 |

게임 설치 위치:

`%LOCALAPPDATA%\2DFightGame\`

## 친구 사용법

1. 런처 실행
2. **게임 설치** / **업데이트** 클릭 (최초 1회)
3. **게임 시작**

이후 업데이트 나오면 런처만 켜서 **업데이트** 누르면 됩니다.

## 주의

- `downloadUrl`은 **브라우저에서 바로 다운로드**되는 링크여야 합니다.
- Google Drive는 `uc?export=download&id=...` 형식 필요.
- GitHub Releases asset URL 권장.
