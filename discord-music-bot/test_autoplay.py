"""자동재생 시드 분석 / 점수 단위 테스트."""

from bot import analyze_song, clean_title_for_seed, extract_video_id, _score_related_track


def test_extract_video_id():
    assert extract_video_id("dQw4w9WgXcQ") == "dQw4w9WgXcQ"
    assert extract_video_id("https://www.youtube.com/watch?v=dQw4w9WgXcQ") == "dQw4w9WgXcQ"
    assert extract_video_id("https://youtu.be/dQw4w9WgXcQ") == "dQw4w9WgXcQ"


def test_clean_title():
    assert "Blueming" in clean_title_for_seed("IU - Blueming (Official Audio)")
    assert "shorts" not in clean_title_for_seed("song").lower()


def test_analyze_song():
    a = analyze_song(
        {
            "title": "IU - Blueming (Official Audio)",
            "uploader": "IU - Topic",
            "webpage_url": "https://www.youtube.com/watch?v=abcABCabcAB",
            "duration": 217,
            "id": "abcABCabcAB",
        }
    )
    assert a["video_id"] == "abcABCabcAB"
    assert a["artist"]
    assert a["clean_title"]


def test_score_prefers_official_audio():
    analysis = {"artist": "IU", "clean_title": "Blueming", "duration": 217}
    atv = {
        "title": "Palette",
        "videoType": "MUSIC_VIDEO_TYPE_ATV",
        "artists": [{"name": "IU"}],
        "length": "3:37",
    }
    ugc = {
        "title": "random cover gameplay",
        "videoType": "MUSIC_VIDEO_TYPE_UGC",
        "artists": [{"name": "Someone"}],
        "length": "10:00",
    }
    assert _score_related_track(atv, analysis) > _score_related_track(ugc, analysis)


if __name__ == "__main__":
    test_extract_video_id()
    test_clean_title()
    test_analyze_song()
    test_score_prefers_official_audio()
    print("OK: autoplay tests passed")
