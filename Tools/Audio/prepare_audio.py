from __future__ import annotations

import shutil
import sys
import wave
from array import array
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "VoiceTest"
DESTINATION = ROOT / "Assets" / "_Game" / "Audio" / "Resources" / "Audio"

SFX = (
    "sfx_coffee_drink.wav",
    "sfx_coffee_drop.wav",
    "sfx_drop_convert_xp.wav",
    "sfx_drop_pickup.wav",
    "sfx_enemy_bug_split.wav",
    "sfx_enemy_email_death.wav",
    "sfx_flow_dayend.wav",
    "sfx_growth_card_appear.wav",
    "sfx_growth_levelup.wav",
    "sfx_player_death.wav",
    "sfx_player_hurt.wav",
    "sfx_slam.wav",
    "sfx_ui_clockin.wav",
    "sfx_weapon_stapler_fire.wav",
    "sfx_weapon_stapler_hit.wav",
)

DROP = (
    "sfx_drop_green.wav",
    "sfx_drop_blue.wav",
    "sfx_drop_purple.wav",
    "sfx_drop_orange.wav",
)

LOOPS = ("sfx_player_lowsan_loop.wav",)

BGM = (
    "bgm_battle.ogg",
    "bgm_boss.ogg",
    "bgm_login.ogg",
    "bgm_result.ogg",
)

EXPECTED = set(SFX + DROP + LOOPS + BGM)
AUDIO_SUFFIXES = {".wav", ".ogg", ".mp3", ".mp4"}


def trim_email_death(source: Path, destination: Path) -> None:
    with wave.open(str(source), "rb") as reader:
        channels = reader.getnchannels()
        sample_width = reader.getsampwidth()
        sample_rate = reader.getframerate()
        compression = reader.getcomptype()
        frames = reader.readframes(reader.getnframes())

    if channels != 1 or sample_width != 2 or sample_rate != 44100 or compression != "NONE":
        raise RuntimeError(
            "email death must be mono 16-bit PCM at 44.1 kHz before trimming; "
            f"got channels={channels}, width={sample_width}, rate={sample_rate}, compression={compression}"
        )

    samples = array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()

    target_frames = round(sample_rate * 0.30)
    fade_frames = round(sample_rate * 0.05)
    if len(samples) < target_frames:
        raise RuntimeError("email death is shorter than the requested 0.30 second derived clip")

    trimmed = samples[:target_frames]
    fade_start = target_frames - fade_frames
    for index in range(fade_frames):
        gain = 1.0 - (index + 1) / fade_frames
        trimmed[fade_start + index] = round(trimmed[fade_start + index] * gain)

    if sys.byteorder != "little":
        trimmed.byteswap()

    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), "wb") as writer:
        writer.setnchannels(channels)
        writer.setsampwidth(sample_width)
        writer.setframerate(sample_rate)
        writer.writeframes(trimmed.tobytes())


def copy_group(names: tuple[str, ...], folder: str) -> None:
    target = DESTINATION / folder
    target.mkdir(parents=True, exist_ok=True)
    for name in names:
        source = SOURCE / name
        destination = target / name
        if name == "sfx_enemy_email_death.wav":
            trim_email_death(source, destination)
        else:
            shutil.copy2(source, destination)


def remove_stale_outputs(names: tuple[str, ...], folder: str) -> None:
    target = DESTINATION / folder
    if not target.is_dir():
        return

    expected = set(names)
    for path in target.iterdir():
        if path.is_file() and path.suffix.lower() in AUDIO_SUFFIXES and path.name not in expected:
            path.unlink()
            meta = Path(str(path) + ".meta")
            if meta.is_file():
                meta.unlink()


def main() -> None:
    if not SOURCE.is_dir():
        raise RuntimeError(f"audio source directory is missing: {SOURCE}")

    actual = {
        path.name
        for path in SOURCE.iterdir()
        if path.is_file() and path.suffix.lower() in AUDIO_SUFFIXES
    }
    missing = sorted(EXPECTED - actual)
    if missing:
        raise RuntimeError("missing converted audio inputs: " + ", ".join(missing))

    unexpected = sorted(actual - EXPECTED)
    if unexpected:
        print("warning: unmapped VoiceTest files: " + ", ".join(unexpected))

    groups = ((SFX, "SFX"), (DROP, "Drop"), (LOOPS, "Loop"), (BGM, "BGM"))
    for names, folder in groups:
        remove_stale_outputs(names, folder)
        copy_group(names, folder)

    produced = []
    for names, folder in groups:
        produced.extend(
            path.name
            for path in (DESTINATION / folder).iterdir()
            if path.is_file() and path.suffix.lower() in AUDIO_SUFFIXES
        )

    missing_output = sorted(EXPECTED - set(produced))
    if missing_output:
        raise RuntimeError("audio preparation did not produce: " + ", ".join(missing_output))

    print(f"prepared {len(EXPECTED)} audio files in {DESTINATION}")
    print("trimmed sfx_enemy_email_death.wav to 0.30s with a 0.05s fade")


if __name__ == "__main__":
    main()
