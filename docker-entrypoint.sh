#!/bin/sh
set -e

# PUID/PGID are what every Sonarr compose file already sets, because the linuxserver and hotio
# images support them through s6-overlay. This gives the same behaviour without that base image:
# fix the ownership of the paths Sonarr writes to, then drop to the requested user.
#
# Running the container with Docker's own `--user` instead skips all of this — there is no root left
# to step down from, so the volumes have to be owned correctly already.
if [ "$(id -u)" = "0" ] && [ -n "${PUID}${PGID}" ]; then
    PUID="${PUID:-1000}"
    PGID="${PGID:-1000}"

    if ! getent group sonarr >/dev/null 2>&1; then
        addgroup -g "${PGID}" sonarr 2>/dev/null || true
    fi

    if ! getent passwd sonarr >/dev/null 2>&1; then
        adduser -D -H -u "${PUID}" -G sonarr sonarr 2>/dev/null || true
    fi

    # Only the config directory, never the media mounts: those can hold a large library and are
    # shared with other containers that have their own idea of who owns them.
    chown -R "${PUID}:${PGID}" /config

    exec su-exec "${PUID}:${PGID}" "$@"
fi

exec "$@"
