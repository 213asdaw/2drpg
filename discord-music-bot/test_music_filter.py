"""음악 영상 필터 단위 테스트 (Discord 토큰 불필요)."""

from bot import is_shorts_url, looks_like_music


def test_shorts_url():
    assert is_shorts_url("https://www.youtube.com/shorts/abcdefghijk")
    assert not is_shorts_url("https://www.youtube.com/watch?v=abcdefghijk")


def test_reject_short_duration():
    assert not looks_like_music(
        {
            "title": "funny clip",
            "duration": 25,
            "webpage_url": "https://www.youtube.com/watch?v=abc",
            "categories": ["Comedy"],
        }
    )


def test_reject_exclude_title():
    assert not looks_like_music(
        {
            "title": "오늘 브이로그 #shorts",
            "duration": 180,
            "webpage_url": "https://www.youtube.com/watch?v=abc",
        }
    )


def test_accept_official_audio():
    assert looks_like_music(
        {
            "title": "IU - Blueming (Official Audio)",
            "duration": 217,
            "webpage_url": "https://www.youtube.com/watch?v=abc",
            "categories": ["Music"],
            "channel": "IU - Topic",
        }
    )


def test_reject_vertical_short():
    assert not looks_like_music(
        {
            "title": "dance",
            "duration": 40,
            "width": 1080,
            "height": 1920,
            "webpage_url": "https://www.youtube.com/watch?v=abc",
        }
    )


if __name__ == "__main__":
    test_shorts_url()
    test_reject_short_duration()
    test_reject_exclude_title()
    test_accept_official_audio()
    test_reject_vertical_short()
    print("OK: all music filter tests passed")
