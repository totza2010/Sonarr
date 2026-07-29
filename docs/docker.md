# Running this fork in a container

Sonarr does not ship an image. `docker/tests/` and `distribution/docker-build/` in this repo are
Mono-era leftovers — a test harness and a Debian package builder, both on Ubuntu releases that are
long out of support. The images people actually run (`linuxserver/sonarr`, `hotio/sonarr`) are built
by third parties from the release tarball. So this fork builds its own, following what those two do.

Both of them land on the same shape, and this follows it: Alpine, the `linux-musl-x64` build,
`icu-libs` and `sqlite-libs`, `Sonarr.Update` deleted, and a `package_info` declaring Docker as the
update method. From hotio specifically: `libintl` alongside the other two, `PackageVersion` in
`package_info`, and a recursive `chmod` so the app is readable by whatever uid the container runs
as. Their s6-overlay base image is not used, but `PUID`/`PGID` are: every Sonarr compose file already
sets them, so a small entrypoint does the same job with `su-exec`.

The image is published by a `docker` job in `.github/workflows/build.yml`, which reuses the
artifacts that workflow already produces rather than building a second time. It hangs off the
`backend` and `frontend` jobs alone: the integration tests reach external services and fail on a
fork, so gating on the whole workflow would mean no image ever gets published.

## Tags

A fork's version only means something against the upstream release it was built from, so that is
what the tags say. `VERSION` in the workflow is upstream's own number — their release commits change
that line and nothing else — so merging from them keeps these honest with nothing to remember.

| Tag | Points at | Moves |
| --- | --- | --- |
| `4.0.19.812` | one build | never — pin this |
| `4.0.19` | newest build of that upstream release | every build on `main` |
| `latest` | newest build on `main` | every build on `main` |
| `main`, `develop` | newest build of that branch | every build on it |

Only `main` and `develop` are built, the same two branches upstream builds. A feature branch is
tried by merging it into `main`, not by publishing an image of its own.

The commit is not a tag; it is in the image labels:

```
docker inspect --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}' IMAGE
```

## What the image is

Built the way the linuxserver image is: Alpine plus the native libraries Sonarr binds to, with the
published package extracted into it. Sonarr publishes self-contained, so there is no .NET runtime to
install — which also means the **musl** build belongs here, not the glibc one.

Installed on top: `icu-libs`, which backs `libSystem.Globalization.Native.so`, `sqlite-libs`,
because the package binds to the system SQLite rather than carrying it, and `libintl`. `ffprobe` is
bundled (`Openur.FFprobeStatic`), and v4 no longer shells out to `mediainfo` — migration 163 moved
media analysis to ffprobe — so neither is installed, and neither of the two reference images
installs them either.

`Sonarr.Update` is deleted, as linuxserver does: the built-in updater would replace the contents of
the image and the next pull would throw the result away. That removes a third of the size. A
`package_info` file next to the bin directory reports Docker as the update method, so the UI explains
how to update instead of offering to do it.

The data directory is `/config`, set on the entrypoint rather than left to the caller, so a plain
`docker run` lands somewhere sensible.

## Configuration without mounting config.xml

Sonarr binds its options from the environment as well as `config.xml`
([Bootstrap.cs](../src/NzbDrone.Host/Bootstrap.cs)), which is what makes running two instances from
one image comfortable:

| Variable | What it sets |
| --- | --- |
| `Sonarr__Server__Port` | listening port |
| `Sonarr__Server__UrlBase` | path prefix behind a reverse proxy |
| `Sonarr__Server__BindAddress` | interface to bind |
| `Sonarr__App__InstanceName` | name shown in the UI and in notifications |
| `Sonarr__Auth__Method` / `Sonarr__Auth__ApiKey` | authentication |
| `Sonarr__Postgres__Host` / `__User` / `__Password` | use Postgres instead of SQLite |
| `Sonarr__Log__Level` | log level |

Anything set here wins over `config.xml`, so the instance identity lives in the compose file and the
volume only carries state.

## Two instances

Each instance owns one root folder, which is one Plex library. Quality tier is the usual reason to
run more than one.

```yaml
services:
  sonarr-hd:
    image: ghcr.io/OWNER/sonarr:4.0.19
    container_name: sonarr-hd
    environment:
      - PUID=1000
      - PGID=1000
      - Sonarr__App__InstanceName=Sonarr HD
      - Sonarr__Server__Port=8989
      - TZ=Asia/Bangkok
    volumes:
      - ./hd-config:/config
      - /mnt/media/TV:/mnt/media/TV
      - /mnt/downloads:/mnt/downloads
    ports:
      - "8989:8989"
    restart: unless-stopped

  sonarr-4k:
    image: ghcr.io/OWNER/sonarr:4.0.19
    container_name: sonarr-4k
    environment:
      - PUID=1000
      - PGID=1000
      - Sonarr__App__InstanceName=Sonarr 4K
      - Sonarr__Server__Port=8990
      - TZ=Asia/Bangkok
    volumes:
      - ./4k-config:/config
      - /mnt/media/TV-4K:/mnt/media/TV-4K
      - /mnt/downloads:/mnt/downloads
    ports:
      - "8990:8990"
    restart: unless-stopped
```

On disk that gives:

```
/mnt/media/TV/
  Spider-Noir (2024)/
/mnt/media/TV-4K/
  Spider-Noir (2024)/
```

## Mount media at the same path inside and outside

The volumes above deliberately map `/mnt/media/TV` to `/mnt/media/TV` rather than to `/tv`. Sonarr
stores absolute paths in its database, so:

- an existing instance can be moved into a container without rewriting every series path,
- the path Sonarr reports matches the path the download client and Plex see, so hardlinks and
  atomic moves work instead of silently falling back to a copy,
- the folders Sonarr creates line up with what the Plex library scanner walks.

If media and downloads live on the same filesystem, mount them under one parent so a completed
download can be hardlinked into the library rather than copied.

## Permissions

`PUID` and `PGID` work as they do in the linuxserver and hotio images:

```yaml
    environment:
      - PUID=1000
      - PGID=1000
```

The entrypoint chowns `/config` to that uid and drops to it with `su-exec`, so a fresh volume needs
no preparation. Media mounts are deliberately left alone — a library can be large, and the same
paths are shared with other containers that have their own idea of who owns them.

Docker's own `--user` works too, and skips the entrypoint logic entirely since there is no root to
step down from. In that case `/config` has to be owned by the uid already, or Sonarr fails on
startup with `Access to the path '/config/asp' is denied`:

```
docker run --rm -v sonarr-hd-config:/config alpine chown -R 1000:1000 /config
```

## Building locally

```
./build.sh --backend --frontend --packages --runtime linux-musl-x64 --framework net6.0
docker build -t sonarr-fork .
docker run --rm -p 8989:8989 -v "$PWD/config:/config" sonarr-fork
```

The `COPY` expects `_artifacts/<runtime>/<framework>/Sonarr`, which is where `build.sh` puts it.
Building without `--packages` leaves that path empty and the image build fails.

## A local build does not currently produce a working package

Building on a machine that has both the .NET 6 and .NET 10 SDKs installed produces a package that
will not start:

```
EPIC FAIL: System.IO.FileLoadException: Could not load file or assembly
'System.Text.Encoding.CodePages, Version=8.0.0.0' ...
```

`Sonarr.Host` references the NuGet package at 8.0.0, but the self-contained publish ships the copy
from the runtime pack instead, which is 6.0.0.0. Comparing against an official release makes it
clear which one is wrong:

| | size | md5 |
| --- | --- | --- |
| official 4.0.19 tarball | 742,152 | `860da94c72bba49d09b88cd62369a59b` |
| NuGet `system.text.encoding.codepages/8.0.0` | 742,152 | `860da94c72bba49d09b88cd62369a59b` |
| local `build.sh` output | 873,760 | `d757d58df57b180abb955ffb9771d5fb` |

Sonarr's own build resolves this correctly, so the image recipe is fine and the local toolchain is
not. Until that is sorted out, build the image from the CI artifact rather than from a local
`build.sh` run.

## What has actually been verified

The recipe was proven by building it against the official 4.0.19 `linux-musl-x64` tarball, since a
local build cannot produce a working package yet:

- image builds, 373 MB against 805 MB for the same thing on Debian with `Sonarr.Update` kept
- container starts, runs its migrations and serves the UI (`GET /` → 200, `/ping` → OK)
- `package_info` takes effect: the API reports `packageUpdateMechanism: docker`, `isDocker: true`
  and the fork's branch
- `PUID=1000 PGID=1000` puts the process on that uid and leaves `/config` owned by it
- `--user 1000:1000` against a pre-chowned volume works as well, so the recursive chmod does its job

What that does **not** cover: the image has never been built from this fork's own code, so none of
this fork's features have been exercised in a container. The workflow has not run either.

## Not covered yet

- Only `linux-musl-x64` is published. The workflow notes what an arm64 matrix would look like.
- No healthcheck. `/ping` answers without authentication if one is wanted.
