import asyncio
import os
import random
import re
import urllib.parse
import urllib.request

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
    "js_runtimes": {"node": {}},
    "remote_components": ["ejs:github"],
}

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
skip_once = False        # 반복 ON이어도 다음 곡으로 넘길 때 사용
control_channel = None   # 버튼/상태 메시지를 보낼 텍스트 채널
control_message = None
last_recommend_query = None

# 쇼츠·일반 영상 필터
MIN_MUSIC_SEC = 60
MAX_MUSIC_SEC = 60 * 12  # 12분 초과는 라이브/모음집 가능성 큼

EXCLUDE_TITLE = re.compile(
    r"(shorts?|쇼츠|릴스|reels|브이로그|vlog|리뷰|review|"
    r"게임|gameplay|플레이|예능|토크|인터뷰|interview|"
    r"하이라이트|highlights?|reaction|리액션|asmr|"
    r"trailer|예고편|tutorial|튜토리얼|강의|"
    r"모음|1시간|10시간|playlist|#shorts)",
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
    }


def duration_text(sec: int) -> str:
    if not sec:
        return "?:??"
    m, s = divmod(int(sec), 60)
    h, m = divmod(m, 60)
    return f"{h}:{m:02d}:{s:02d}" if h else f"{m}:{s:02d}"


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
    global current_song, loop_enabled
    if not current_song:
        return discord.Embed(title="재생 중인 곡 없음", color=discord.Color.dark_grey())

    vc = None
    # embed만 만들 때 voice 상태는 호출측에서 보강 가능
    status = []
    status.append("🔁 반복 ON" if loop_enabled else "🔁 반복 OFF")
    status.append(f"대기열 {len(song_queue)}곡")

    embed = discord.Embed(
        title="🎵 지금 재생 중",
        description=f"**{current_song['title']}**",
        color=discord.Color.blurple(),
    )
    if current_song.get("webpage_url"):
        embed.url = current_song["webpage_url"]
    embed.add_field(name="길이", value=duration_text(current_song.get("duration") or 0), inline=True)
    embed.add_field(name="상태", value=" · ".join(status), inline=True)
    if current_song.get("uploader"):
        embed.set_footer(text=current_song["uploader"])
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
            play_next(vc)

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
            play_next(vc)


def schedule_coro(coro):
    """봇 이벤트 루프에서 코루틴 실행 (같은 스레드/다른 스레드 모두 안전)."""
    try:
        loop = asyncio.get_running_loop()
    except RuntimeError:
        # ffmpeg after 콜백 등 다른 스레드
        return asyncio.run_coroutine_threadsafe(coro, bot.loop)
    # 이미 봇 루프 안이면 create_task (run_coroutine_threadsafe는 같은 스레드에서 불안정)
    return loop.create_task(coro)


async def refresh_panel_embed():
    """기존 패널 embed만 갱신 (버튼 유지)."""
    global control_message
    if control_message is None:
        return
    embed = make_status_embed()
    try:
        await control_message.edit(embed=embed, view=PlayerControls())
    except Exception:
        # 메시지가 없어졌으면 다음에 새로 보냄
        control_message = None


async def send_or_update_panel(channel):
    """곡이 바뀔 때마다 버튼 패널을 새로 보냄 (버튼 유실 방지)."""
    global control_message, control_channel
    if channel is None:
        return
    control_channel = channel
    embed = make_status_embed()
    view = PlayerControls()

    old = control_message
    try:
        control_message = await channel.send(embed=embed, view=view)
    except Exception as e:
        print(f"[패널 전송 실패] {e}")
        return

    # 이전 패널 버튼 제거 (실패해도 무시)
    if old is not None:
        try:
            await old.edit(view=None)
        except Exception:
            try:
                await old.delete()
            except Exception:
                pass


def play_next(vc):
    """다음 곡 재생. 반복재생 ON이면 현재 곡을 다시 재생."""
    global current_song, skip_once

    do_loop = loop_enabled and not skip_once
    skip_once = False

    if do_loop and current_song and current_song.get("url"):
        next_song = current_song
    elif len(song_queue) > 0:
        next_song = song_queue.pop(0)
        current_song = next_song
    else:
        current_song = None
        # 큐 끝났을 때 버튼만 남기지 않도록 이전 패널 정리
        if control_channel is not None:
            schedule_coro(_clear_panel_buttons())
        return

    def _after(err):
        if err:
            print(f"[재생 after 에러] {err}")
        # .result()로 기다리면 데드락/다음 곡 패널 미갱신 날 수 있음
        schedule_coro(_play_next_async(vc))

    try:
        vc.play(discord.FFmpegPCMAudio(next_song["url"], **ffmpeg_options), after=_after)
    except Exception as e:
        print(f"[vc.play 에러] {e}")
        if not do_loop:
            current_song = None
        schedule_coro(_play_next_async(vc))
        return

    if control_channel is not None:
        schedule_coro(send_or_update_panel(control_channel))


async def _clear_panel_buttons():
    global control_message
    if control_message is None:
        return
    try:
        await control_message.edit(view=None)
    except Exception:
        pass


async def _play_next_async(vc):
    """after 콜백용: 반복 중이면 스트림 URL 갱신 후 재생."""
    global current_song
    if not vc or not vc.is_connected():
        return

    # 반복이고 스킵이 아니면 만료된 스트림 URL 갱신
    if loop_enabled and not skip_once and current_song and current_song.get("webpage_url"):
        try:
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                info = await asyncio.to_thread(
                    ydl.extract_info, current_song["webpage_url"], download=False
                )
            if info and info.get("url"):
                current_song["url"] = info["url"]
        except Exception as e:
            print(f"[반복 스트림 갱신 실패] {e}")

    if not vc.is_playing() and not vc.is_paused():
        play_next(vc)


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


# ---------------------------------------------------------------------------
# 이벤트 / 명령어
# ---------------------------------------------------------------------------
@bot.event
async def on_ready():
    bot.add_view(PlayerControls())  # 재시작 후에도 버튼 동작
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
    global control_channel
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return await ctx.send("❌ 먼저 음성 채널에 접속해 주세요!")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()

    control_channel = ctx.channel
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
            play_next(vc)

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
    global current_song, control_message, loop_enabled
    if not in_song_channel(ctx):
        return
    vc = ctx.voice_client
    if not vc:
        return await ctx.send("❌ 봇이 현재 음성 채널에 접속해 있지 않습니다.")
    await vc.disconnect()
    song_queue.clear()
    current_song = None
    control_message = None
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
    global control_channel
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return

    await ctx.send(f"⚡ '{search_query}' 관련 **노래 영상만** 검색 중입니다! (쇼츠/일반 영상 제외)")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()
    control_channel = ctx.channel

    try:
        added_songs = await add_music_recommendations(search_query, limit=5)
        if not added_songs:
            return await ctx.send(
                "❌ 노래 영상을 찾지 못했어요. 쇼츠·브이로그·게임 영상은 자동으로 걸러집니다."
            )
        await ctx.send("**🎵 대기열에 추가된 노래들:**\n" + "\n".join(f"- {t}" for t in added_songs))
        if not vc.is_playing() and not vc.is_paused():
            play_next(vc)
    except Exception as e:
        await ctx.send("⚠️ 음악을 불러오는 중 오류가 발생했습니다.")
        print(f"[검색 에러] {e}")


@bot.command()
async def 고급추천(ctx, *, search_query):
    global control_channel
    if not in_song_channel(ctx):
        return
    if not ctx.author.voice or not ctx.author.voice.channel:
        return

    await ctx.send(f"🎧 '{search_query}' 관련 **노래만** 찾는 중입니다... 🎵")

    vc = ctx.voice_client
    if not vc:
        vc = await ctx.author.voice.channel.connect()
    control_channel = ctx.channel

    try:
        added_songs = await add_advanced_recommendations(search_query, limit=5)
        if not added_songs:
            return await ctx.send("❌ 조건에 맞는 노래 영상을 찾지 못했습니다.")
        await ctx.send("**🎵 대기열에 추가된 추천 음원들:**\n" + "\n".join(f"- {t}" for t in added_songs))
        if not vc.is_playing() and not vc.is_paused():
            play_next(vc)
    except Exception as e:
        await ctx.send("⚠️ 음악을 불러오는 중 심각한 오류가 발생했습니다.")
        print(f"[검색 에러] {e}")


@bot.command()
async def 명령어(ctx):
    text = """
**🎵 노래봇 명령어**
`!재생 제목/링크` — 재생 / 대기열 추가
`!다음` — 다음 곡
`!반복재생` — 반복 ON/OFF (토글)
`!일시정지` — 일시정지/재개 (토글)
`!추천 키워드` — 노래 영상만 추천 (쇼츠·일반 영상 제외)
`!고급추천 키워드` — 유튜브뮤직 위주 노래만 추천
`!대기열` — 대기열 보기
`!클리어` — 대기열 비우기
`!나가` — 음성채널 퇴장

재생이 시작되면 **다음 · 반복재생 · 일시정지 · 추천 · 고급추천** 버튼이 나타납니다.
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
