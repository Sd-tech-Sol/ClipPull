# Pause/resume investigation for v0.6.0

Pause/resume is intentionally deferred from ClipPull v0.6.0.

## What was tested

On 2026-09-11, yt-dlp was run against the small public YouTube media at
`https://www.youtube.com/watch?v=jNQXAC9IVRw` using separate MP4 video and M4A
audio streams, FFmpeg merging, and a 20 KiB/s test rate limit. The process was
interrupted during the audio stream. yt-dlp preserved the completed video and
a 15,360-byte `.part` audio file. Re-running the identical command resumed the
audio at byte 15,360 and completed the MP4 merge successfully.

This confirms that yt-dlp's default `--continue` and `.part` behavior can work
for a controlled interruption during a known download phase.

## Why the UI feature is deferred

ClipPull's current process boundary covers extraction, one or more downloads,
playlist iteration, FFmpeg merging, post-processing, and archive updates. A
generic process stop cannot atomically prove that it occurred during a safe
download phase. In particular:

- a click can race with the transition from the final stream to FFmpeg merge;
- interrupted post-processing can leave output that is not a resumable
  `.part` file;
- playlists can be between entries when stopped;
- local archive behavior differs depending on which entries have completed;
- cancellation and pause need distinct queue-control lifetimes.

A reliable implementation needs explicit phase signals and per-item process
control, plus broader playlist, archive-on/archive-off, and post-processing
tests. A pause button is therefore not shipped in v0.6.0.
