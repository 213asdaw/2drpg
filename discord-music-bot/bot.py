import asyncio
import os
import random
import re
import urllib.parse
import urllib.request
from collections import deque
from typing import Deque, List, Optional, Set

import discord
import yt_dlp
from discord.ext import commands
from ytmusicapi import YTMusic

# ---------------------------------------------------------------------------
# 설정
# ---------------------------------------------------------------------------
# 토큰은 환경변수 권장. (파일에 직접 넣었다면 아래에 넣되, Git에 올리지 마세요!)
TOKEN = os.getenv("DISCORD_TOKEN", "").strip()

ydl_opts = {
    "format": "bestaudio/best",
    "source_address": "0.0.0.0",
    "noplaylist": True,
    "quiet": True,
    "no_warnings": True,
    "js_runtimes": {"node": {}},
    "remote_components": ["ejs:github"],
    # 봇 차단 우회 시도 (안 되면 cookies.txt 사용)
    "extractor_args": {
        "youtube": {
            "player_client": ["android", "ios", "tv_embedded", "web"],
        }
    },
}
# 선택: 환경변수 YTDLP_COOKIES=/path/to/cookies.txt
_cookies = os.getenv("YTDLP_COOKIES", "").strip()
if _cookies and os.path.isfile(_cookies):
    ydl_opts["cookiefile"] = _cookies

ffmpeg_options = {
    "options": "-vn",
    "before_options": "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5",
}

ytdl = yt_dlp.YoutubeDL(ydl_opts)
ytmusic = YTMusic()

intents = discord.Intents.default()
intents.message_content = True
bot = commands.Bot(command_prefix="!", intents=intents)

# 대기열 / 재생 상태
song_queue = []          # [{title, url, webpage_url, duration}, ...]
current_song = None
loop_enabled = False
autoplay_enabled = False  # 큐 끝나면 이전 곡과 비슷한 노래 자동 재생
skip_once = False        # 반복 ON이어도 다음 곡으로 넘길 때 사용
control_channel = None   # 버튼/상태 메시지를 보낼 텍스트 채널
control_channel_id = None
control_message = None
last_recommend_query = None
played_ids: Deque[str] = deque(maxlen=80)  # 최근 재생 videoId (자동재생 중복 방지)
last_seed_song = None  # 자동재생 분석용 직전 곡
stream_fail_streak = 0  # 스트림 연속 실패 시 자동재생 중단

# 쇼츠·일반 영상 필터
MIN_MUSIC_SEC = 60
MAX_MUSIC_SEC = 60 * 12  # 12분 초과는 라이브/모음집 가능성 큼

EXCLUDE_TITLE = re.compile(
    r"(shorts?|쇼츠|릴스|reels|브이로그|vlog|리뷰|review|"
    r"게임|gameplay|예능|토크|인터뷰|interview|"
    r"하이라이트|highlights?|reaction|리액션|asmr|"
    r"trailer|예고편|tutorial|튜토리얼|강의|"
    r"모음|1시간|10시간|playlist|#shorts)",
    re.IGNORECASE,
)

# 자동재생용: 더 약한 제외 (YTMusic 라디오는 이미 음악일 확률 높음)
AUTOPLAY_HARD_EXCLUDE = re.compile(
    r"(#?shorts?|쇼츠|브이로그|\bvlog\b|gameplay|리액션|\breaction\b|"
    r"asmr|예고편|\btrailer\b|튜토리얼|tutorial|"
    r"1시간|10시간|10\s*hour)",
    re.IGNORECASE,
)

MUSIC_TITLE_HINT = re.compile(
    r"(official\s*(audio|mv|music\s*video)|공식|lyrics?|가사|"
    r"audio|뮤직비디오|music\s*video|topic|커버|cover|ost|theme)",
    re.IGNORECASE,
)


def in_song_channel(ctx) -> bool:
    return "노래봇" in (ctx.channel.name or "")


def is_shorts_url(url: str) -> bool:
    return bool(url) and ("youtube.com/shorts/" in url or "/shorts/" in url)


def looks_like_music(info: dict) -> bool:
    """쇼츠·잡영상을 거르고 노래 영상만 통과."""
    if not info:
        return False

    url = info.get("webpage_url") or info.get("original_url") or ""
    if is_shorts_url(url):
        return False

    title = info.get("title") or ""
    if EXCLUDE_TITLE.search(title):
        return False

    duration = info.get("duration") or 0
    if duration < MIN_MUSIC_SEC or duration > MAX_MUSIC_SEC:
        return False

    # 세로형 + 짧은 영상 → 쇼츠
    w, h = info.get("width") or 0, info.get("height") or 0
    if w and h and h > w and duration <= 60:
        return False

    categories = info.get("categories") or []
    if "Music" in categories or "음악" in categories:
        return True

    channel = (info.get("channel") or info.get("uploader") or "").lower()
    if any(k in channel for k in ("topic", "vevo", "official", "records", "music")):
        return True

    if MUSIC_TITLE_HINT.search(title):
        return True

    # duration만 통과한 경우: 추천에서는 카테고리/힌트 있는 것만 쓰는 편이 안전
    return False


def song_dict_from_info(info: dict) -> dict:
    return {
        "title": info.get("title", "제목 없음"),
        "url": info.get("url"),
        "webpage_url": info.get("webpage_url")
        or info.get("original_url")
        or (f"https://www.youtube.com/watch?v={info['id']}" if info.get("id") else ""),
        "duration": info.get("duration") or 0,
        "uploader": info.get("uploader") or info.get("channel") or "",
        "id": info.get("id") or "",
    }


def duration_text(sec: int) -> str:
    if not sec:
        return "?:??"
    m, s = divmod(int(sec), 60)
    h, m = divmod(m, 60)
    return f"{h}:{m:02d}:{s:02d}" if h else f"{m}:{s:02d}"


def extract_video_id(url_or_id: str) -> Optional[str]:
    if not url_or_id:
        return None
    if re.fullmatch(r"[a-zA-Z0-9_-]{11}", url_or_id):
        return url_or_id
    m = re.search(r"(?:v=|/youtu\.be/|/shorts/|/embed/)([a-zA-Z0-9_-]{11})", url_or_id)
    return m.group(1) if m else None


def clean_title_for_seed(title: str) -> str:
    t = re.sub(r"[\(\[\{].*?[\)\]\}]", "", title or "")
    t = re.sub(
        r"(?i)official\s*(audio|mv|music\s*video)|lyrics?|가사|mv|4k|hd|"
        r"remaster(ed)?|주제곡|ost|full\s*version|오디오",
        "",
        t,
    )
    return re.sub(r"\s+", " ", t).strip(" -_|:")


_GENERIC_ARTISTS = {
    "someone",
    "unknown",
    "various artists",
    "various",
    "topic",
    "youtube",
    "auto-generated",
    "알 수 없음",
    "알 수 없는 아티스트",
}


def _is_generic_artist(name: str) -> bool:
    n = (name or "").strip().lower()
    if not n or len(n) < 2:
        return True
    if n in _GENERIC_ARTISTS:
        return True
    if n.endswith(" - topic"):
        return False
    return False


def analyze_song(song: dict) -> dict:
    """이전 곡에서 아티스트/제목/영상ID를 뽑아 유사곡 검색 시드로 쓴다."""
    title = song.get("title") or ""
    uploader = song.get("uploader") or ""
    artist = (
        uploader.replace(" - Topic", "")
        .replace("VEVO", "")
        .replace("Official", "")
        .strip()
    )
    if _is_generic_artist(artist):
        artist = ""

    cleaned = clean_title_for_seed(title)
    parts = re.split(r"\s*[-–—|:]\s*", cleaned, maxsplit=1)
    if len(parts) == 2 and len(parts[0]) >= 2 and len(parts[1]) >= 2:
        # "아티스트 - 곡명" 형태면 분리 (제목 파싱 우선)
        maybe_artist, maybe_title = parts[0].strip(), parts[1].strip()
        if (not artist) or _is_generic_artist(artist) or "topic" in uploader.lower():
            artist = maybe_artist
        cleaned = maybe_title
    video_id = song.get("id") or extract_video_id(song.get("webpage_url") or "")
    return {
        "video_id": video_id,
        "title": title,
        "clean_title": cleaned or title,
        "artist": artist,
        "duration": int(song.get("duration") or 0),
    }


def remember_played(song: dict):
    vid = song.get("id") or extract_video_id(song.get("webpage_url") or "")
    if vid and vid not in played_ids:
        played_ids.append(vid)


def avoided_ids() -> Set[str]:
    ids = set(played_ids)
    for s in song_queue:
        vid = s.get("id") or extract_video_id(s.get("webpage_url") or "")
        if vid:
            ids.add(vid)
    if current_song:
        vid = current_song.get("id") or extract_video_id(current_song.get("webpage_url") or "")
        if vid:
            ids.add(vid)
    return ids


# ---------------------------------------------------------------------------
# 검색
# ---------------------------------------------------------------------------
def fast_youtube_search(query: str, *, music_bias: bool = False):
    """유튜브 검색 결과 videoId 목록. music_bias면 노래 위주 쿼리/필터."""
    q = query
    if music_bias:
        q = f"{query} official audio OR lyrics -shorts -쇼츠 -브이로그 -gameplay"
    query_string = urllib.parse.urlencode(
        {
            "search_query": q,
            "sp": "EgIQAQ==",  # 동영상만
        }
    )
    html_content = urllib.request.urlopen(
        "https://www.youtube.com/results?" + query_string
    )
    return re.findall(r'"videoId":"([a-zA-Z0-9_-]{11})"', html_content.read().decode())


# ---------------------------------------------------------------------------
# 재생 엔진 + 버튼 패널
# ---------------------------------------------------------------------------
def make_status_embed():
    global current_song, loop_enabled, autoplay_enabled
    if not current_song:
        return discord.Embed(title="재생 중인 곡 없음", color=discord.Color.dark_grey())

    status = []
    status.append("🔁 반복 ON" if loop_enabled else "🔁 반복 OFF")
    status.append("🤖 자동재생 ON" if autoplay_enabled else "🤖 자동재생 OFF")
    status.append(f"대기열 {len(song_queue)}곡")

    title_prefix = "🤖 " if current_song.get("autoplay") else ""
    embed = discord.Embed(
        title="🎵 지금 재생 중",
        description=f"**{title_prefix}{current_song['title']}**",
        color=discord.Color.blurple(),
    )
    if current_song.get("webpage_url"):
        embed.url = current_song["webpage_url"]
    embed.add_field(name="길이", value=duration_text(current_song.get("duration") or 0), inline=True)
    embed.add_field(name="상태", value=" · ".join(status), inline=True)
    footer = current_song.get("uploader") or ""
    if current_song.get("autoplay"):
        footer = (footer + " · " if footer else "") + "자동재생(유사곡)"
    if footer:
        embed.set_footer(text=footer)
    return embed


class PlayerControls(discord.ui.View):
    def __init__(self):
        super().__init__(timeout=None)

    async def _require_voice(self, interaction: discord.Interaction):
        if "노래봇" not in (interaction.channel.name or ""):
            await interaction.response.send_message("노래봇 채널에서만 사용할 수 있어요.", ephemeral=True)
            return None
        if not interaction.user.voice or not interaction.user.voice.channel:
            await interaction.response.send_message("먼저 음성 채널에 접속해 주세요!", ephemeral=True)
            return None
        vc = interaction.guild.voice_client
        if not vc:
            vc = await interaction.user.voice.channel.connect()
        return vc

    async def _refresh_panel(self, interaction: discord.Interaction):
        global control_message
        embed = make_status_embed()
        vc = interaction.guild.voice_client
        if vc and vc.is_paused():
            embed.add_field(name="재생", value="⏸️ 일시정지", inline=True)
        elif vc and vc.is_playing():
            embed.add_field(name="재생", value="▶️ 재생 중", inline=True)
        try:
            await interaction.message.edit(embed=embed, view=self)
            control_message = interaction.message
        except Exception:
            pass

    @discord.ui.button(label="다음", style=discord.ButtonStyle.primary, custom_id="nb:next", row=0)
    async def btn_next(self, interaction: discord.Interaction, button: discord.ui.Button):
        vc = await self._require_voice(interaction)
        if vc is None:
            return
        if not vc.is_playing() and not vc.is_paused():
            await interaction.response.send_message("재생 중인 곡이 없습니다.", ephemeral=True)
            return
        global skip_once
        skip_once = True
        vc.stop()
        await interaction.response.send_message("⏭️ 다음 곡으로!", ephemeral=True)

    @discord.ui.button(label="반복재생", style=discord.ButtonStyle.secondary, custom_id="nb:loop", row=0)
    async def btn_loop(self, interaction: discord.Interaction, button: discord.ui.Button):
        if await self._require_voice(interaction) is None:
            return
        global loop_enabled
        loop_enabled = not loop_enabled
        msg = "🔁 반복재생 **ON**" if loop_enabled else "반복재생 **OFF**"
        await interaction.response.send_message(msg, ephemeral=True)
        await self._refresh_panel(interaction)

    @discord.ui.button(label="일시정지", style=discord.ButtonStyle.secondary, custom_id="nb:pause", row=0)
    async def btn_pause(self, interaction: discord.Interaction, button: discord.ui.Button):
        vc = await self._require_voice(interaction)
        if vc is None:
            return
        if not vc.is_playing() and not vc.is_paused():
            await interaction.response.send_message("재생 중인 곡이 없습니다.", ephemeral=True)
            return
        if vc.is_paused():
            vc.resume()
            await interaction.response.send_message("▶️ 재생 재개", ephemeral=True)
        else:
            vc.pause()
            await interaction.response.send_message("⏸️ 일시정지", ephemeral=True)
        await self._refresh_panel(interaction)

    @discord.ui.button(label="자동재생", style=discord.ButtonStyle.secondary, custom_id="nb:autoplay", row=0)
    async def btn_autoplay(self, interaction: discord.Interaction, button: discord.ui.Button):
        vc = await self._require_voice(interaction)
        if vc is None:
            return
        global autoplay_enabled, control_channel, control_channel_id
        autoplay_enabled = not autoplay_enabled
        control_channel = interaction.channel
        control_channel_id = interaction.channel.id

        if not autoplay_enabled:
            await interaction.response.send_message("자동재생 **OFF**", ephemeral=True)
            await self._refresh_panel(interaction)
            return

        await interaction.response.defer(ephemeral=True)
        await self._refresh_panel(interaction)

        seed = current_song or last_seed_song
        if not seed:
            await interaction.followup.send(
                "🤖 자동재생 ON — 재생 중인 곡이 없어 예약은 못 했어요. 먼저 노래를 틀어 주세요.",
                ephemeral=True,
            )
            return

        titles = await enqueue_autoplay_songs(seed, limit=3)
        if not titles:
            await interaction.followup.send(
                "🤖 자동재생 ON — 지금은 유사곡을 못 찾았어요. 곡이 끝나면 다시 시도해요.",
                ephemeral=True,
            )
            return

        await interaction.followup.send(
            "🤖 자동재생 ON — 유사곡 예약:\n" + "\n".join(f"- {t}" for t in titles),
            ephemeral=True,
        )
        if not vc.is_playing() and not vc.is_paused():
            await play_next_async(vc, interaction.channel)

    @discord.ui.button(label="추천", style=discord.ButtonStyle.success, custom_id="nb:rec", row=1)
    async def btn_rec(self, interaction: discord.Interaction, button: discord.ui.Button):
        vc = await self._require_voice(interaction)
        if vc is None:
            return
        seed = None
        if current_song and current_song.get("title"):
            seed = current_song["title"]
        elif last_recommend_query:
            seed = last_recommend_query
        if not seed:
            await interaction.response.send_message(
                "추천 기준이 없어요. `!추천 아이유` 처럼 먼저 검색해 주세요.", ephemeral=True
            )
            return
        await interaction.response.defer(ephemeral=True)
        added = await add_music_recommendations(seed, limit=3)
        if not added:
            await interaction.followup.send(
                "노래 영상만 골라봤는데 결과가 없어요. (쇼츠/일반 영상 제외)", ephemeral=True
            )
            return
        await interaction.followup.send(
            "**✨ 추천 추가 (노래만):**\n" + "\n".join(f"- {t}" for t in added), ephemeral=True
        )
        if not vc.is_playing() and not vc.is_paused():
            await play_next_async(vc, interaction.channel)

    @discord.ui.button(label="고급추천", style=discord.ButtonStyle.success, custom_id="nb:adv", row=1)
    async def btn_adv(self, interaction: discord.Interaction, button: discord.ui.Button):
        vc = await self._require_voice(interaction)
        if vc is None:
            return
        seed = None
        if current_song and current_song.get("title"):
            seed = current_song["title"]
        elif last_recommend_query:
            seed = last_recommend_query
        if not seed:
            await interaction.response.send_message(
                "고급추천 기준이 없어요. 노래를 재생하거나 `!고급추천 키워드`를 써 주세요.",
                ephemeral=True,
            )
            return
        await interaction.response.defer(ephemeral=True)
        added = await add_advanced_recommendations(seed, limit=3)
        if not added:
            await interaction.followup.send("조건에 맞는 노래 영상을 못 찾았어요.", ephemeral=True)
            return
        await interaction.followup.send(
            "**🎯 고급추천 추가 (노래만):**\n" + "\n".join(f"- {t}" for t in added), ephemeral=True
        )
        if not vc.is_playing() and not vc.is_paused():
            await play_next_async(vc, interaction.channel)


async def resolve_control_channel(channel=None):
    """패널을 보낼 텍스트 채널 확보 (id로 재조회해 after 콜백에서도 안전하게)."""
    global control_channel, control_channel_id
    if channel is not None:
        control_channel = channel
        control_channel_id = getattr(channel, "id", None)
        return channel

    if control_channel_id:
        ch = bot.get_channel(control_channel_id)
        if ch is None:
            try:
                ch = await bot.fetch_channel(control_channel_id)
            except Exception as e:
                print(f"[패널] 채널 fetch 실패: {e}")
                ch = None
        if ch is not None:
            control_channel = ch
            return ch

    return control_channel


async def refresh_panel_embed():
    """기존 패널 embed만 갱신 (버튼 유지). 실패 시 새 패널 전송."""
    global control_message
    ch = await resolve_control_channel()
    if control_message is None:
        if ch is not None and current_song:
            await send_control_panel(ch)
        return
    embed = make_status_embed()
    try:
        await control_message.edit(
            content=f"🎵 **지금 재생 중:** {current_song['title']}" if current_song else "재생 중인 곡 없음",
            embed=embed,
            view=PlayerControls(),
        )
    except Exception as e:
        print(f"[패널 갱신 실패] {e}")
        control_message = None
        if ch is not None and current_song:
            await send_control_panel(ch)


async def send_control_panel(channel):
    """버튼 패널 메시지를 반드시 새로 전송."""
    global control_message, control_channel, control_channel_id
    ch = await resolve_control_channel(channel)
    if ch is None:
        print("[패널] channel 이 None 이라 전송 불가")
        return
    control_channel = ch
    control_channel_id = ch.id

    title = current_song["title"] if current_song else "재생 중인 곡 없음"
    embed = make_status_embed()
    view = PlayerControls()
    old = control_message

    try:
        control_message = await ch.send(
            content=f"🎵 **지금 재생 중:** {title}",
            embed=embed,
            view=view,
        )
        print(f"[패널] 전송 성공 message_id={control_message.id}")
    except Exception as e:
        print(f"[패널] embed 전송 실패: {e!r} → 간단 메시지로 재시도")
        try:
            control_message = await ch.send(
                content=(
                    f"🎵 **지금 재생 중:** {title}\n"
                    f"대기열 {len(song_queue)}곡 | "
                    f"{'반복 ON' if loop_enabled else '반복 OFF'} | "
                    f"{'자동재생 ON' if autoplay_enabled else '자동재생 OFF'}\n"
                    f"(다음 / 반복재생 / 일시정지 / 자동재생 / 추천 / 고급추천)"
                ),
                view=PlayerControls(),
            )
            print(f"[패널] 간단 전송 성공 message_id={control_message.id}")
        except Exception as e2:
            print(f"[패널] 전송 완전 실패: {e2!r}")
            control_message = None
            return

    if old is not None:
        try:
            await old.edit(view=None)
        except Exception:
            try:
                await old.delete()
            except Exception:
                pass


async def ensure_fresh_stream(song: dict) -> dict:
    """대기열에 넣어둔 스트림 URL은 금방 만료되므로 재생 직전 다시 뽑는다."""
    webpage = song.get("webpage_url")
    if not webpage and song.get("id"):
        webpage = f"https://www.youtube.com/watch?v={song['id']}"
        song["webpage_url"] = webpage
    if not webpage:
        return song

    clients_to_try = [
        ["android", "ios", "tv_embedded", "web"],
        ["android"],
        ["ios"],
        ["web"],
    ]
    last_err = None
    for clients in clients_to_try:
        opts = dict(ydl_opts)
        opts["extractor_args"] = {"youtube": {"player_client": clients}}
        try:
            with yt_dlp.YoutubeDL(opts) as ydl:
                info = await asyncio.to_thread(ydl.extract_info, webpage, download=False)
            if info and info.get("url"):
                song["url"] = info["url"]
                song["duration"] = song.get("duration") or info.get("duration") or 0
                song["title"] = song.get("title") or info.get("title") or "제목 없음"
                song["id"] = song.get("id") or info.get("id") or ""
                if info.get("thumbnail"):
                    song["thumbnail"] = info.get("thumbnail")
                if info.get("uploader") or info.get("channel"):
                    song["uploader"] = song.get("uploader") or info.get("uploader") or info.get("channel")
                return song
        except Exception as e:
            last_err = e
            continue
    print(f"[스트림 갱신 실패] {last_err}")
    song["url"] = None
    return song


async def play_next_async(vc, channel=None):
    """다음 곡 재생 + 패널 전송 (명령어/버튼/after 공용)."""
    global current_song, skip_once, last_seed_song, stream_fail_streak

    ch = await resolve_control_channel(channel)
    if not vc or not vc.is_connected():
        return

    do_loop = loop_enabled and not skip_once
    skip_once = False

    if do_loop and current_song:
        next_song = current_song
    elif len(song_queue) > 0:
        next_song = song_queue.pop(0)
        current_song = next_song
    else:
        # 대기열 없음 → 자동재생이면 이전 곡 분석해서 유사곡 큐에 넣기
        seed = current_song or last_seed_song
        if autoplay_enabled and seed and stream_fail_streak < 5:
            if ch is not None:
                try:
                    await ch.send(
                        f"🤖 자동재생: **{seed.get('title', '이전 곡')}** 과(와) 비슷한 노래를 찾는 중..."
                    )
                except Exception:
                    pass
            titles = await enqueue_autoplay_songs(seed, limit=3)
            if titles:
                next_song = song_queue.pop(0)
                current_song = next_song
                if ch is not None:
                    try:
                        await ch.send(
                            f"🤖 **자동재생** (기준: {seed.get('title')})\n"
                            + "\n".join(f"- {t}" for t in titles)
                        )
                    except Exception:
                        pass
            else:
                current_song = None
                if ch is not None:
                    try:
                        await ch.send("🤖 자동재생: 비슷한 노래 영상을 찾지 못했어요.")
                    except Exception:
                        pass
                if control_message is not None:
                    try:
                        await control_message.edit(content="큐가 비었습니다.", embed=None, view=None)
                    except Exception:
                        pass
                return
        else:
            current_song = None
            if control_message is not None:
                try:
                    await control_message.edit(content="큐가 비었습니다.", embed=None, view=None)
                except Exception:
                    pass
            return

    # 매 곡 재생 직전 스트림 URL 재발급 (2곡째부터 만료로 실패하던 문제 수정)
    next_song = await ensure_fresh_stream(next_song)
    remember_played(next_song)
    if not next_song.get("url"):
        stream_fail_streak += 1
        print(f"[재생] 스트림 URL 없음, 스킵: {next_song.get('title')} (streak={stream_fail_streak})")
        if ch is not None:
            try:
                await ch.send(
                    f"⚠️ `{next_song.get('title')}` 재생 URL을 못 받아 건너뜁니다.\n"
                    f"계속 실패하면 유튜브 봇 차단일 수 있어요. `cookies.txt` 설정을 확인하세요."
                )
            except Exception:
                pass
        if stream_fail_streak >= 5:
            if ch is not None:
                try:
                    await ch.send("⛔ 스트림을 연속으로 못 받아 자동재생을 잠시 멈춥니다.")
                except Exception:
                    pass
            current_song = None
            return
        retry_seed = next_song if (next_song.get("id") or next_song.get("title")) else last_seed_song
        current_song = None
        if autoplay_enabled and retry_seed and len(song_queue) == 0:
            await enqueue_autoplay_songs(retry_seed, limit=3)
        await play_next_async(vc, ch)
        return

    stream_fail_streak = 0
    current_song = next_song
    last_seed_song = dict(next_song)

    def _after(err):
        if err:
            print(f"[재생 after 에러] {err}")
        try:
            asyncio.run_coroutine_threadsafe(play_next_async(vc), bot.loop)
        except Exception as e:
            print(f"[after 스케줄 실패] {e}")

    try:
        vc.play(discord.FFmpegPCMAudio(next_song["url"], **ffmpeg_options), after=_after)
    except Exception as e:
        print(f"[vc.play 에러] {e}")
        await asyncio.sleep(0.2)
        # 실패 곡은 버리고 다음 곡
        current_song = None
        await play_next_async(vc, ch)
        return

    # 곡이 바뀌면 패널 항상 새로 전송
    await send_control_panel(ch)


async def extract_and_queue(video_url: str):
    with yt_dlp.YoutubeDL(ydl_opts) as ydl:
        info = await asyncio.to_thread(ydl.extract_info, video_url, download=False)
    if not info or not info.get("url"):
        return None
    song = song_dict_from_info(info)
    song_queue.append(song)
    return song


async def add_music_recommendations(search_query: str, limit: int = 5):
    """추천: 쇼츠/일반 영상 거르고 노래만 대기열에 추가. 추가된 제목 리스트 반환."""
    global last_recommend_query
    last_recommend_query = search_query

    advanced_query = f"{search_query} -shorts -쇼츠 -브이로그 -gameplay"
    search_results = await asyncio.to_thread(fast_youtube_search, advanced_query, music_bias=True)
    if not search_results:
        search_results = await asyncio.to_thread(fast_youtube_search, advanced_query)

    unique_results = list(dict.fromkeys(search_results))[:12]
    added_songs = []

    with yt_dlp.YoutubeDL(ydl_opts) as ydl:
        for video_id in unique_results:
            if len(added_songs) >= limit:
                break
            video_url = f"https://www.youtube.com/watch?v={video_id}"
            info = await asyncio.to_thread(ydl.extract_info, video_url, download=False)
            if not info:
                continue
            if not looks_like_music(info):
                continue
            stream_url = info.get("url")
            if not stream_url:
                continue
            song = song_dict_from_info(info)
            song_queue.append(song)
            added_songs.append(song["title"])

    return added_songs


async def add_advanced_recommendations(search_query: str, limit: int = 5):
    """고급추천: YouTube Music 우선, Music 카테고리·쇼츠 필터."""
    global last_recommend_query
    last_recommend_query = search_query

    search_opts = {"extract_flat": True, "quiet": True, "no_warnings": True}
    search_info = None

    with yt_dlp.YoutubeDL(search_opts) as search_ydl:
        try:
            search_info = await asyncio.to_thread(
                search_ydl.extract_info, f"ytmsearch30:{search_query}", download=False
            )
        except Exception:
            pass

        if not search_info or "entries" not in search_info:
            print(f"⚠️ 유튜브 뮤직 검색 실패 → 일반 검색: {search_query}")
            fallback_query = (
                f"ytsearch20:{search_query} official audio -모음 -1시간 "
                f"-playlist -shorts -쇼츠 -브이로그 -gameplay"
            )
            search_info = await asyncio.to_thread(
                search_ydl.extract_info, fallback_query, download=False
            )

    if not search_info or "entries" not in search_info:
        return []

    entries = [e for e in search_info["entries"] if e]
    random.shuffle(entries)
    added_songs = []

    with yt_dlp.YoutubeDL(ydl_opts) as ydl:
        for entry in entries:
            if len(added_songs) >= limit:
                break
            video_id = entry.get("id")
            if not video_id:
                continue
            video_url = f"https://www.youtube.com/watch?v={video_id}"
            info = await asyncio.to_thread(ydl.extract_info, video_url, download=False)
            if not info:
                continue
            if not looks_like_music(info):
                continue
            # 고급추천은 Music 카테고리 또는 Topic/Official 힌트 필수에 가깝게
            categories = info.get("categories") or []
            channel = (info.get("channel") or info.get("uploader") or "").lower()
            title = info.get("title") or ""
            strong = (
                "Music" in categories
                or "음악" in categories
                or any(k in channel for k in ("topic", "vevo", "official"))
                or bool(MUSIC_TITLE_HINT.search(title))
            )
            if not strong:
                continue
            if not info.get("url"):
                continue
            song = song_dict_from_info(info)
            song_queue.append(song)
            uploader = song.get("uploader") or ""
            added_songs.append(f"{song['title']}" + (f" ({uploader})" if uploader else ""))

    return added_songs


def _score_related_track(track: dict, analysis: dict) -> int:
    """유사도 점수: YouTube Music 라디오 후보 정렬용 (거의 탈락시키지 않음)."""
    score = 20
    title = track.get("title") or ""
    if AUTOPLAY_HARD_EXCLUDE.search(title):
        return -1

    vtype = (track.get("videoType") or "").upper()
    if "ATV" in vtype:
        score += 40
    elif "OMV" in vtype:
        score += 30
    elif "UGC" in vtype:
        score += 8
    else:
        score += 15  # 타입 없어도 YTMusic이면 기본 가산

    artists = [a.get("name", "") for a in (track.get("artists") or []) if a.get("name")]
    artist_blob = " ".join(artists).lower()
    seed_artist = (analysis.get("artist") or "").lower()
    if seed_artist and seed_artist in artist_blob:
        score += 35  # 같은 아티스트 강력 가산

    clean = (analysis.get("clean_title") or "").lower()
    title_l = title.lower()
    if clean and clean in title_l:
        score -= 15  # 같은 곡 재추천만 살짝 억제 (탈락 X)

    duration = _parse_ytm_length(track.get("length") or track.get("duration"))
    seed_dur = analysis.get("duration") or 0
    if duration:
        if 45 <= duration <= 60 * 15:
            score += 10
        if seed_dur and abs(duration - seed_dur) <= 120:
            score += 12
    return score


def _parse_ytm_length(length) -> int:
    """'3:45' / 225 → 초."""
    if length is None:
        return 0
    if isinstance(length, (int, float)):
        return int(length)
    s = str(length).strip()
    if not s:
        return 0
    if s.isdigit():
        return int(s)
    try:
        parts = [int(x) for x in s.split(":")]
        if len(parts) == 2:
            return parts[0] * 60 + parts[1]
        if len(parts) == 3:
            return parts[0] * 3600 + parts[1] * 60 + parts[2]
    except Exception:
        return 0
    return 0


def _song_from_ytmusic_track(track: dict, *, analysis: dict, source: str) -> Optional[dict]:
    """YTMusic 트랙 → 대기열용 song (자동재생은 거의 통과)."""
    vid = track.get("videoId") or track.get("id")
    title = track.get("title") or ""
    if not vid or not title:
        return None
    # 확실한 잡영상만 제외
    if AUTOPLAY_HARD_EXCLUDE.search(title):
        return None
    artists = [a.get("name", "") for a in (track.get("artists") or []) if a.get("name")]
    uploader = ", ".join(artists) if artists else (track.get("uploader") or "")
    duration = _parse_ytm_length(
        track.get("length") or track.get("duration") or track.get("duration_seconds")
    )
    # 극단적인 길이만 제외 (30초 미만 / 20분 초과)
    if duration and (duration < 30 or duration > 60 * 20):
        return None
    return {
        "title": title,
        "url": None,
        "webpage_url": f"https://www.youtube.com/watch?v={vid}",
        "duration": duration,
        "uploader": uploader,
        "id": vid,
        "autoplay": True,
        "autoplay_from": analysis.get("title"),
        "autoplay_source": source,
    }


def _add_candidate(candidates: List[dict], seen: Set[str], track: dict, *, score: int, source: str):
    vid = track.get("videoId") or track.get("id")
    if not vid or vid in seen:
        return
    if score < 0:
        return
    seen.add(vid)
    item = dict(track)
    item["videoId"] = vid
    item["_score"] = score
    item["_source"] = source
    candidates.append(item)


async def _resolve_seed_video_id(analysis: dict) -> Optional[str]:
    """재생 곡에 videoId가 없으면 제목/아티스트로 YTMusic에서 찾는다."""
    if analysis.get("video_id"):
        return analysis["video_id"]
    queries = []
    if analysis.get("artist") and analysis.get("clean_title"):
        queries.append(f"{analysis['artist']} {analysis['clean_title']}")
    if analysis.get("clean_title"):
        queries.append(analysis["clean_title"])
    if analysis.get("title"):
        queries.append(analysis["title"])
    for q in queries:
        try:
            results = await asyncio.to_thread(ytmusic.search, q, filter="songs", limit=5)
            for r in results or []:
                vid = r.get("videoId")
                if vid:
                    # analysis 보강
                    analysis["video_id"] = vid
                    if not analysis.get("artist"):
                        artists = r.get("artists") or []
                        if artists:
                            analysis["artist"] = artists[0].get("name") or analysis.get("artist")
                    return vid
        except Exception as e:
            print(f"[자동재생] seed 해석 실패({q}): {e}")
    return None


async def find_autoplay_tracks(seed_song: dict, limit: int = 3) -> List[dict]:
    """
    이전 곡과 비슷한 노래를 최대한 많이/확실하게 찾는다.
    - YTMusic 라디오 + 일반 watch + 아티스트 곡 + 여러 검색어
    - 필터는 쇼츠/잡영상 정도만 (성공률 우선)
    """
    analysis = analyze_song(seed_song)
    await _resolve_seed_video_id(analysis)

    avoid = avoided_ids()
    if analysis.get("video_id"):
        avoid.add(analysis["video_id"])

    candidates: List[dict] = []
    seen: Set[str] = set(avoid)

    # 1) Radio
    if analysis.get("video_id"):
        for radio_flag, tag in ((True, "radio"), (False, "watch")):
            try:
                data = await asyncio.to_thread(
                    ytmusic.get_watch_playlist,
                    videoId=analysis["video_id"],
                    limit=50,
                    radio=radio_flag,
                )
                for t in data.get("tracks") or []:
                    score = _score_related_track(t, analysis)
                    _add_candidate(candidates, seen, t, score=score, source=f"ytmusic-{tag}")
            except Exception as e:
                print(f"[자동재생] YTMusic {tag} 실패: {e}")

    # 2) 같은 아티스트 곡 / 관련 아티스트
    artist = analysis.get("artist") or ""
    if artist and not _is_generic_artist(artist):
        try:
            # artist id 찾기
            asearch = await asyncio.to_thread(ytmusic.search, artist, filter="artists", limit=1)
            artist_id = None
            if asearch:
                artist_id = asearch[0].get("browseId") or asearch[0].get("channelId")
            if artist_id:
                info = await asyncio.to_thread(ytmusic.get_artist, artist_id)
                for t in ((info.get("songs") or {}).get("results") or []):
                    score = _score_related_track(t, analysis) + 25
                    _add_candidate(candidates, seen, t, score=score, source="artist-songs")
                # related artists → 그들의 곡
                related_list = ((info.get("related") or {}).get("results") or [])[:4]
                for rel in related_list:
                    rid = rel.get("browseId")
                    if not rid:
                        continue
                    try:
                        rinfo = await asyncio.to_thread(ytmusic.get_artist, rid)
                        for t in ((rinfo.get("songs") or {}).get("results") or [])[:8]:
                            score = _score_related_track(t, analysis) + 10
                            _add_candidate(candidates, seen, t, score=score, source="related-artist")
                    except Exception:
                        continue
        except Exception as e:
            print(f"[자동재생] artist catalog 실패: {e}")

    # 3) 여러 검색어 (항상 실행 — 후보 부족할 때만 하던 방식 폐기)
    search_queries = []
    if artist and analysis.get("clean_title"):
        search_queries += [
            f"{artist}",
            f"{artist} 노래",
            f"{artist} best songs",
            f"{artist} popular",
            f"{analysis['clean_title']} like songs",
        ]
    elif analysis.get("clean_title"):
        search_queries += [
            analysis["clean_title"],
            f"{analysis['clean_title']} similar songs",
            f"{analysis['clean_title']} playlist",
        ]
    if analysis.get("title"):
        search_queries.append(analysis["title"])

    # 중복 제거, 순서 유지
    uniq_q = []
    for q in search_queries:
        q = (q or "").strip()
        if q and q not in uniq_q:
            uniq_q.append(q)

    for q in uniq_q[:6]:
        try:
            results = await asyncio.to_thread(ytmusic.search, q, filter="songs", limit=25)
            for t in results or []:
                score = _score_related_track(t, analysis)
                if artist:
                    artists = [a.get("name", "") for a in (t.get("artists") or [])]
                    if any(artist.lower() in (a or "").lower() for a in artists):
                        score += 20
                _add_candidate(candidates, seen, t, score=max(score, 8), source="ytmusic-search")
        except Exception as e:
            print(f"[자동재생] search '{q}' 실패: {e}")

    # 4) 최후: yt-dlp flat
    if len(candidates) < 8:
        q = artist or analysis.get("clean_title") or analysis.get("title") or "popular songs"
        search_opts = {"extract_flat": True, "quiet": True, "no_warnings": True}
        try:
            with yt_dlp.YoutubeDL(search_opts) as ydl:
                info = await asyncio.to_thread(
                    ydl.extract_info, f"ytmsearch20:{q}", download=False
                )
            for e in (info or {}).get("entries") or []:
                if not e:
                    continue
                _add_candidate(
                    candidates,
                    seen,
                    {
                        "videoId": e.get("id"),
                        "title": e.get("title") or "",
                        "artists": [{"name": e.get("uploader") or ""}],
                        "length": e.get("duration"),
                    },
                    score=12,
                    source="ytdlp-flat",
                )
        except Exception as e:
            print(f"[자동재생] flat 검색 실패: {e}")

    candidates.sort(key=lambda c: c.get("_score", 0), reverse=True)
    print(
        f"[자동재생] 총 후보 {len(candidates)}개 "
        f"(seed={analysis.get('video_id')}, artist={analysis.get('artist')})"
    )
    if not candidates:
        return []

    # 상위권에서 다양하게 뽑기 (같은 곡만 반복 방지)
    top = candidates[:40]
    random.shuffle(top)
    # 점수 높은 것 일부는 고정 포함
    ordered = candidates[:15] + top
    picked: List[dict] = []
    picked_ids: Set[str] = set()
    for pick in ordered:
        if len(picked) >= limit:
            break
        song = _song_from_ytmusic_track(
            pick, analysis=analysis, source=pick.get("_source") or "unknown"
        )
        if not song or song["id"] in picked_ids:
            continue
        picked.append(song)
        picked_ids.add(song["id"])

    # 그래도 부족하면 점수순으로 강제 채우기 (하드 제외만 적용)
    if len(picked) < limit:
        for pick in candidates:
            if len(picked) >= limit:
                break
            song = _song_from_ytmusic_track(
                pick, analysis=analysis, source=pick.get("_source") or "unknown"
            )
            if not song or song["id"] in picked_ids:
                continue
            picked.append(song)
            picked_ids.add(song["id"])

    for s in picked:
        print(f"[자동재생] 선택: {s['title']} ({s['id']}) via {s['autoplay_source']}")
    return picked


async def find_autoplay_track(seed_song: dict) -> Optional[dict]:
    tracks = await find_autoplay_tracks(seed_song, limit=1)
    return tracks[0] if tracks else None


async def enqueue_autoplay_songs(seed_song: dict, limit: int = 3) -> List[str]:
    """유사곡을 찾아 대기열에 넣고 제목 목록 반환."""
    tracks = await find_autoplay_tracks(seed_song, limit=limit)
    titles = []
    for t in tracks:
        song_queue.append(t)
        titles.append(t["title"])
    return titles


# ---------------------------------------------------------------------------
# 이벤트 / 명령어
# ---------------------------------------------------------------------------
@bot.event
async def on_ready():
    # persistent view: 재시작 후에도 버튼 클릭 수신
    try:
        bot.add_view(PlayerControls())
    except Exception as e:
        print(f"[add_view] {e}")
    print(f"{bot.user.name} 봇이 온라인입니다!")


@bot.event
async def on_voice_state_update(member, before, after):
    global current_song, control_message
    voice_client = discord.utils.get(bot.voice_clients, guild=member.guild)
    if voice_client is None:
        return
    if before.channel is not None and before.channel.id == voice_client.channel.id:
        if len(voice_client.channel.members) == 1:
            song_queue.clear()
            current_song = None
            control_message = None
            await voice_client.disconnect()
            print(f"{before.channel.name} 채널에 아무도 없어서 퇴장했습니다.")


@bot.command()
async def 재생(ctx, *, query):
    global control_channel, control_channel_id
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return await ctx.send("❌ 먼저 음성 채널에 접속해 주세요!")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()

    control_channel = ctx.channel
    control_channel_id = ctx.channel.id
    await ctx.send(f"🔎 '{query}' 음악을 가져오는 중입니다...")

    try:
        if "youtube.com/watch" in query or "youtu.be/" in query or "youtube.com/shorts/" in query:
            video_url = query
        else:
            search_results = await asyncio.to_thread(fast_youtube_search, query)
            if not search_results:
                return await ctx.send("❌ 검색 결과를 찾을 수 없습니다.")
            video_url = f"https://www.youtube.com/watch?v={search_results[0]}"

        song = await extract_and_queue(video_url)
        if not song:
            return await ctx.send("❌ 영상 정보를 가져올 수 없습니다.")

        await ctx.send(f"▶️ **{song['title']}** 곡이 대기열에 추가되었습니다!")
        if not vc.is_playing() and not vc.is_paused():
            # 첫 재생 / 멈춘 상태 → 재생 시작 + 패널
            await play_next_async(vc, ctx.channel)
        else:
            # 이미 재생 중 → 대기열만 추가된 경우에도 패널 대기열 숫자 갱신
            await refresh_panel_embed()

    except Exception as e:
        await ctx.send("⚠️ 곡을 추가하는 중 오류가 발생했습니다.")
        print(f"[재생 에러] {e}")


@bot.command()
async def 대기열(ctx):
    if len(song_queue) == 0 and not current_song:
        return await ctx.send("텅 비어있네요! 노래를 먼저 추가해 주세요. 🎧")

    lines = []
    if current_song:
        loop_mark = " 🔁" if loop_enabled else ""
        lines.append(f"**재생중{loop_mark}:** {current_song['title']}")
    lines.append(f"\n**🎵 대기열 (총 {len(song_queue)}곡)**\n")
    for index, song in enumerate(song_queue):
        lines.append(f"**{index + 1}.** {song['title']}")
    queue_text = "\n".join(lines)
    if len(queue_text) > 1900:
        queue_text = queue_text[:1900] + "\n... (목록이 너무 길어 아래는 생략되었습니다!)"
    await ctx.send(queue_text)


@bot.command()
async def 다음(ctx):
    global skip_once
    if ctx.voice_client and (ctx.voice_client.is_playing() or ctx.voice_client.is_paused()):
        skip_once = True
        ctx.voice_client.stop()
        await ctx.send("현재 곡을 건너뜁니다.")
    else:
        await ctx.send("재생 중인 곡이 없습니다.")


@bot.command()
async def 나가(ctx):
    global current_song, control_message, control_channel, control_channel_id, loop_enabled
    if not in_song_channel(ctx):
        return
    vc = ctx.voice_client
    if not vc:
        return await ctx.send("❌ 봇이 현재 음성 채널에 접속해 있지 않습니다.")
    await vc.disconnect()
    song_queue.clear()
    current_song = None
    control_message = None
    control_channel = None
    control_channel_id = None
    loop_enabled = False
    await ctx.send("👋 안녕히계세요")


@bot.command()
async def 클리어(ctx):
    song_queue.clear()
    if ctx.voice_client and (ctx.voice_client.is_playing() or ctx.voice_client.is_paused()):
        ctx.voice_client.stop()
    await ctx.send("대기열을 비웠습니다!")


@bot.command()
async def 반복재생(ctx):
    """한번: 반복 ON / 다시: OFF"""
    global loop_enabled
    if not in_song_channel(ctx):
        return
    loop_enabled = not loop_enabled
    if loop_enabled:
        await ctx.send("🔁 반복재생 **활성화** — 다시 입력하면 해제됩니다.")
    else:
        await ctx.send("반복재생 **비활성화**")
    await refresh_panel_embed()


@bot.command()
async def 자동재생(ctx):
    """한번: 자동재생 ON / 다시: OFF — 대기열이 비면 이전 곡과 비슷한 노래 재생"""
    global autoplay_enabled, control_channel, control_channel_id
    if not in_song_channel(ctx):
        return
    autoplay_enabled = not autoplay_enabled
    control_channel = ctx.channel
    control_channel_id = ctx.channel.id

    if not autoplay_enabled:
        await ctx.send("자동재생 **비활성화**")
        await refresh_panel_embed()
        return

    await ctx.send(
        "🤖 자동재생 **활성화**\n"
        "대기열이 비면 직전 곡과 비슷한 노래를 이어서 틀어요.\n"
        "지금 재생 중이면 유사곡을 **3곡** 미리 예약합니다."
    )
    await refresh_panel_embed()

    seed = current_song or last_seed_song
    if not seed:
        await ctx.send("재생 중인 곡이 없어요. `!재생` 후 다시 `!자동재생` 하세요.")
        return

    async with ctx.typing():
        titles = await enqueue_autoplay_songs(seed, limit=3)
    if not titles:
        await ctx.send("❌ 비슷한 곡을 못 찾았어요. 곡이 끝날 때 다시 시도합니다.")
        return

    await ctx.send(
        f"✅ 자동재생 예약 (기준: {seed.get('title')}):\n"
        + "\n".join(f"- {t}" for t in titles)
    )
    await refresh_panel_embed()

    vc = ctx.voice_client
    if vc and not vc.is_playing() and not vc.is_paused():
        await play_next_async(vc, ctx.channel)


@bot.command()
async def 일시정지(ctx):
    """한번: 일시정지 / 다시: 재개"""
    if not in_song_channel(ctx):
        return
    vc = ctx.voice_client
    if not vc or (not vc.is_playing() and not vc.is_paused()):
        return await ctx.send("재생 중인 곡이 없습니다.")
    if vc.is_paused():
        vc.resume()
        await ctx.send("▶️ 재생 재개 — 다시 입력하면 일시정지됩니다.")
    else:
        vc.pause()
        await ctx.send("⏸️ 일시정지 — 다시 입력하면 재생됩니다.")
    await refresh_panel_embed()


@bot.command()
async def 추천(ctx, *, search_query):
    global control_channel, control_channel_id
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return

    await ctx.send(f"⚡ '{search_query}' 관련 **노래 영상만** 검색 중입니다! (쇼츠/일반 영상 제외)")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()
    control_channel = ctx.channel
    control_channel_id = ctx.channel.id

    try:
        added_songs = await add_music_recommendations(search_query, limit=5)
        if not added_songs:
            return await ctx.send(
                "❌ 노래 영상을 찾지 못했어요. 쇼츠·브이로그·게임 영상은 자동으로 걸러집니다."
            )
        await ctx.send("**🎵 대기열에 추가된 노래들:**\n" + "\n".join(f"- {t}" for t in added_songs))
        if not vc.is_playing() and not vc.is_paused():
            await play_next_async(vc, ctx.channel)
        else:
            await refresh_panel_embed()
    except Exception as e:
        await ctx.send("⚠️ 음악을 불러오는 중 오류가 발생했습니다.")
        print(f"[검색 에러] {e}")


@bot.command()
async def 고급추천(ctx, *, search_query):
    global control_channel, control_channel_id
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return

    await ctx.send(f"🎧 '{search_query}' 관련 **노래만** 찾는 중입니다... 🎵")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()
    control_channel = ctx.channel
    control_channel_id = ctx.channel.id

    try:
        added_songs = await add_advanced_recommendations(search_query, limit=5)
        if not added_songs:
            return await ctx.send("❌ 조건에 맞는 노래 영상을 찾지 못했습니다.")
        await ctx.send("**🎵 대기열에 추가된 추천 음원들:**\n" + "\n".join(f"- {t}" for t in added_songs))
        if not vc.is_playing() and not vc.is_paused():
            await play_next_async(vc, ctx.channel)
        else:
            await refresh_panel_embed()
    except Exception as e:
        await ctx.send("⚠️ 음악을 불러오는 중 심각한 오류가 발생했습니다.")
        print(f"[검색 에러] {e}")


@bot.command()
async def 패널(ctx):
    """버튼 패널을 다시 보냅니다."""
    global control_channel
    if not in_song_channel(ctx):
        return
    control_channel = ctx.channel
    if not current_song:
        return await ctx.send("재생 중인 곡이 없어요. 먼저 `!재생` 하세요.")
    await send_control_panel(ctx.channel)
    await ctx.send("패널을 다시 보냈어요!", delete_after=3)


@bot.command()
async def 명령어(ctx):
    text = """
**🎵 노래봇 명령어**
`!재생 제목/링크` — 재생 / 대기열 추가
`!다음` — 다음 곡
`!반복재생` — 반복 ON/OFF (토글)
`!자동재생` — 유사곡 자동재생 ON/OFF (토글)
`!일시정지` — 일시정지/재개 (토글)
`!추천 키워드` — 노래 영상만 추천 (쇼츠·일반 영상 제외)
`!고급추천 키워드` — 유튜브뮤직 위주 노래만 추천
`!패널` — 버튼 패널 다시 보내기
`!대기열` — 대기열 보기
`!클리어` — 대기열 비우기
`!나가` — 음성채널 퇴장

재생이 시작되면 **다음 · 반복재생 · 일시정지 · 자동재생 · 추천 · 고급추천** 버튼이 나타납니다.
`!자동재생` ON이면 대기열이 끝날 때 이전 곡과 비슷한 노래를 이어서 재생합니다.
""".strip()
    await ctx.send(text)


def main():
    token = TOKEN
    if not token:
        # 로컬 실행용 토큰 (GitHub에 올리지 말 것!)
        token = ""
    if not token:
        raise SystemExit(
            "DISCORD_TOKEN이 없습니다.\n"
            "Windows: set DISCORD_TOKEN=토큰\n"
            "또는 bot.py의 main() 안 token 값에 넣으세요."
        )
    bot.run(token)


if __name__ == "__main__":
    main()
