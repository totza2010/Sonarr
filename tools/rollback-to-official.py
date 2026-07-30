#!/usr/bin/env python3
"""Check whether a database can go back to official Sonarr, and put it back if it can.

    python tools/rollback-to-official.py <path to sonarr.db>            # report only
    python tools/rollback-to-official.py <path to sonarr.db> --apply    # make the changes

Stop Sonarr before running this. Nothing is written without --apply, and --apply copies the database
first.

What has to be undone is not guessed from the schema: rollback-plan.json says what each migration
this fork added did, and only the migrations a given database recorded in VersionInfo are acted on.
A database that never ran the editions migration is never asked about editions.

Most of what this fork adds is inert to official Sonarr - the extra columns are all NOT NULL with a
default, so its own inserts fill them without knowing they exist, and the VersionInfo rows numbered
from 9000 name migrations it has never heard of and are skipped. The plan marks those, and this
leaves them alone: they are also what makes coming back here later a matter of starting the other
build.
"""

import argparse
import json
import os
import shutil
import sqlite3
import sys
from datetime import datetime

PLAN_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "rollback-plan.json")


def connect(path, writable):
    uri = "file:" + path.replace("\\", "/") + ("" if writable else "?mode=ro")
    return sqlite3.connect(uri, uri=True)


def applied_fork_migrations(db, plan):
    """The fork migrations this database actually ran, in order."""
    recorded = {str(row[0]) for row in db.execute("SELECT Version FROM VersionInfo")}

    return [(version, plan[version]) for version in sorted(plan, key=int) if version in recorded]


def rows_for(db, query):
    try:
        return list(db.execute(query))
    except sqlite3.OperationalError as error:
        # A column the query names is missing, which means the migration did not leave what the plan
        # says it did. Saying so is better than reporting nothing found.
        raise SystemExit(f"the plan does not match this database: {error}")


def report(db, migrations):
    blockers = []
    warnings = []

    print("=== what this database ran ===\n")

    if not migrations:
        print("  none of this fork's migrations - nothing to undo, switch whenever you like\n")
        return True

    for version, entry in migrations:
        mark = "inert" if entry.get("inert") else "needs work"
        print(f"  {version}  {entry['name']}  [{mark}]")

    print("\n=== what stands in the way ===\n")

    for version, entry in migrations:
        for kind, bucket in (("blocks", blockers), ("warns", warnings)):
            spec = entry.get(kind)

            if not spec:
                continue

            found = rows_for(db, spec["query"])

            if found:
                bucket.append((version, entry, spec, found))

    if not blockers and not warnings:
        print("  nothing - the features were never used\n")
        return True

    for version, entry, spec, found in blockers + warnings:
        heading = "MUST BE CLEARED FIRST" if spec is entry.get("blocks") else "removed by --apply"
        print(f"{spec['label']}: {len(found)} - {heading}   (migration {version} {entry['name']})")

        for row in found:
            print("    " + "  ".join(str(column) for column in row))

        print(f"\n    {spec['reason']}")
        print(f"    {spec['resolution']}\n")

    return not blockers


def undo_statements(migrations):
    for version, entry in migrations:
        for statement in entry.get("undo", []):
            yield version, statement


def apply(path, migrations):
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    backup = f"{path}.before-rollback-{stamp}"

    shutil.copy2(path, backup)
    print(f"copied the database to {backup}\n")

    db = connect(path, writable=True)

    try:
        with db:
            for version, statement in undo_statements(migrations):
                changed = db.execute(statement).rowcount
                suffix = f" ({changed} row(s))" if changed and changed > 0 else ""
                print(f"  {version}: {statement.splitlines()[0][:90]}{suffix}")
    except sqlite3.IntegrityError as error:
        print(f"\nFAILED: {error}")
        print("Nothing was kept. Two series still share a TVDB id, so an edition was missed.")
        print(f"{backup} is the database as it was.")
        return 1
    finally:
        db.close()

    print("\nThe columns are left in place: they are inert to official Sonarr, dropping one in SQLite")
    print("rebuilds the whole table, and keeping them is what makes coming back here cost nothing.")

    return 0


def main():
    parser = argparse.ArgumentParser(description="Prepare a database for official Sonarr.")
    parser.add_argument("database", help="path to sonarr.db")
    parser.add_argument("--apply", action="store_true", help="make the changes")
    args = parser.parse_args()

    with open(PLAN_PATH, encoding="utf-8") as handle:
        plan = json.load(handle)["migrations"]

    try:
        db = connect(args.database, writable=False)
        migrations = applied_fork_migrations(db, plan)
        ready = report(db, migrations)
        db.close()
    except sqlite3.Error as error:
        print(f"cannot read {args.database}: {error}")
        return 2

    if not args.apply:
        print("Nothing was written. Add --apply when the list above is clear.")
        return 0 if ready else 1

    if not ready:
        print("Refusing to change anything while the list above is not clear.")
        return 1

    return apply(args.database, migrations)


if __name__ == "__main__":
    sys.exit(main())
