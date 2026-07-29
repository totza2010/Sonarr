# Built the way the linuxserver image is: an Alpine base with the native libraries Sonarr binds to,
# and the published package extracted into it. Sonarr publishes self-contained, so there is no .NET
# runtime to install — but that also means the musl build has to be used here, not the glibc one.
#
# The package comes from outside: build.sh writes _artifacts/<runtime>/<framework>/Sonarr, and CI
# feeds this the artifact the normal Build workflow already produces.
FROM alpine:3.21

ARG RUNTIME=linux-musl-x64
ARG FRAMEWORK=net6.0
ARG BRANCH=main
ARG REPOSITORY=totza2010/Sonarr
ARG VERSION=0.0.0

# icu-libs backs libSystem.Globalization.Native.so and sqlite-libs is the database driver the package
# binds to rather than carrying; libintl is what hotio installs alongside them. su-exec is for the
# entrypoint to drop privileges, standing in for the s6-overlay their base image brings. tzdata is
# what makes TZ mean anything: Alpine carries no zone database, so without it a compose file setting
# TZ is quietly ignored and everything stays on UTC. ffprobe is bundled
# (Openur.FFprobeStatic) and v4 no longer shells out to mediainfo — migration 163 moved media
# analysis to ffprobe — so neither is installed.
RUN apk add --no-cache icu-libs libintl sqlite-libs su-exec tzdata

COPY _artifacts/${RUNTIME}/${FRAMEWORK}/Sonarr /app/bin

# The built-in updater is kept out by .dockerignore rather than deleted here: a file deleted in a
# later layer is still carried by the one that brought it in, so the image would be no smaller and
# the 30MB would still be downloaded. package_info sits next to the bin directory and tells Sonarr to
# report Docker as the update method, so the UI explains how to update instead of offering to do it.
RUN printf 'PackageVersion=%s\nPackageAuthor=[%s](https://github.com/%s)\nUpdateMethod=Docker\nBranch=%s\n' \
        "${VERSION}" "${REPOSITORY}" "${REPOSITORY}" "${BRANCH}" > /app/package_info \
    && chmod -R u=rwX,go=rX /app \
    && chmod +x /app/bin/Sonarr /app/bin/ffprobe

# The recursive chmod above leaves the app readable by any uid, so `user:` on the container works
# without rebuilding. Holds config.xml, the database and the logs. Media and downloads are mounted by whoever runs this,
# at the same paths the download client and Plex use.
VOLUME /config

EXPOSE 8989

COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Sonarr binds its options from the environment as well as config.xml, so an instance can be defined
# entirely in a compose file: Sonarr__Server__Port, Sonarr__App__InstanceName, Sonarr__Auth__ApiKey,
# Sonarr__Postgres__Host, Sonarr__Log__Level, ...
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["/app/bin/Sonarr", "-nobrowser", "-data=/config"]
