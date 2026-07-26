# 디스코드 노래봇 (조연우)

원본 `bot.py`에 아래 기능을 추가한 버전입니다.

## 추가된 기능

1. **추천 / 고급추천 → 노래 영상만**
   - Shorts URL, 세로형 짧은 영상 제외
   - 1분 미만 / 12분 초과 제외
   - 제목에 쇼츠·브이로그·게임·리액션·모음집 등이 있으면 제외
   - Music 카테고리 / Topic·VEVO·Official / Official Audio·가사 영상 우선

2. **`!반복재생` / `!일시정지` / `!자동재생` 토글**
   - 한 번 → 활성화
   - 다시 → 비활성화(재개)

3. **자동재생 (유사곡)**
   - 대기열이 비면 직전 곡의 아티스트·제목·영상 ID를 분석
   - YouTube Music 라디오(`get_watch_playlist(radio=True)`)로 비슷한 곡 선정
   - Official Audio / MV 우선, 최근 재생곡·쇼츠·잡영상 제외

4. **재생 컨트롤 버튼**
   - `다음` · `반복재생` · `일시정지` · `자동재생` · `추천` · `고급추천`

## 설치 / 실행 (Windows)

```bat
cd discord-music-bot
py -m pip install -r requirements.txt
set DISCORD_TOKEN=여기에_봇_토큰
py bot.py
```

또는 `bot.py` 맨 아래 `main()` 안의 `token = ""`에 토큰을 넣어도 됩니다.  
**토큰을 GitHub 등에 올리지 마세요.**

FFmpeg가 PATH에 있어야 합니다. 채널 이름에 `노래봇`이 포함된 텍스트 채널에서만 동작합니다.

## 기존 폴더에 적용

이 `bot.py`를  
`c:\Users\admin\OneDrive\Desktop\노래봇 조연우\bot.py`  
에 덮어쓴 뒤 토큰만 다시 넣으면 됩니다.

## 보안

업로드된 원본에 봇 토큰이 들어 있었습니다.  
[Discord Developer Portal](https://discord.com/developers/applications)에서 **토큰을 재발급(Reset)** 한 뒤 새 토큰을 사용하세요.
