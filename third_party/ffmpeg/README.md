# Bundled ffmpeg binaries

The video export feature spawns `ffmpeg` to encode WebM/VP9 (default) and MP4/H.264 (when `libx264` is available). The app prefers a binary bundled here over one on the user's `PATH`.

These binaries are **not** committed to git — drop them in before publishing a release.

## Required layout

```
third_party/ffmpeg/
├── win-x64/ffmpeg.exe
├── osx-arm64/ffmpeg
└── linux-x64/ffmpeg
```

The csproj copies the matching binary next to the published exe (sibling, not embedded). When a binary is absent, the build still succeeds and the app falls back to a system ffmpeg on `PATH` (or the PNG-sequence encoder).

## Licensing

Default codec is **VP9/WebM** so the bundle stays LGPL-clean. Build ffmpeg with `--disable-gpl --enable-libvpx` to ship an LGPL-compatible binary. MP4/H.264 (libx264) is GPL — only enabled at runtime if the user already has a libx264-capable ffmpeg on `PATH`.

## Sourcing

| Platform | Source | Notes |
| --- | --- | --- |
| `win-x64` | https://ffmpeg.org/download.html → Windows builds (LGPL) | Or build from source. |
| `osx-arm64` | Build from source: `./configure --disable-gpl --enable-libvpx --enable-libopus` | No prebuilt LGPL Apple Silicon binaries are widely distributed. |
| `linux-x64` | Build from source with the same flags. | The popular `johnvansickle.com` static builds are GPL. |

After dropping the binary in place on Unix:

```sh
chmod +x third_party/ffmpeg/osx-arm64/ffmpeg
chmod +x third_party/ffmpeg/linux-x64/ffmpeg
```

Record the version + sha256 + source URL of each binary you bundle.

## macOS signing

The `signing/Publish-And-Sign-macOS.zsh` script relocates the bundled binary from `Contents/MacOS/ffmpeg` to `Contents/Resources/ffmpeg` and signs it with the same Developer ID as the main app. The app's `FfmpegEncoder.LocateBinary()` looks in `Contents/Resources` when running from inside an `.app` bundle.
