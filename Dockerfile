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
# entrypoint to drop privileges, standing in for the s6-overlay their base image brings. ffprobe is bundled
# (Openur.FFprobeStatic) and v4 no longer shells out to mediainfo — migration 163 moved media
# analysis to ffprobe — so neither is installed.
RUN apk add --no-cache icu-libs libintl sqlite-libs su-exec

COPY _artifacts/${RUNTIME}/${FRAMEWORK}/Sonarr /app/bin

# The built-in updater would replace the contents of the image, which the next pull throws away.
# Removing it drops a third of the image and takes the option off the table rather than letting it
# half-work. package_info sits next to the bin directory and tells Sonarr to report Docker as the
# update method, so the UI explains how to update instead of offering to do it itself.
RUN rm -rf /app/bin/Sonarr.Update \
    && printf 'PackageVersion=%s\nPackageAuthor=[%s](https://github.com/%s)\nUpdateMethod=Docker\nBranch=%s\n' \
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
