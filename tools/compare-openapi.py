#!/usr/bin/env python3
"""Compare this fork's OpenAPI spec against the one upstream published, and say whether anything a
third-party client relies on has moved.

    python tools/compare-openapi.py <baseline.json> <ours.json>

The baseline is upstream's own spec. The last commit before this fork touched the API carries it:

    git show 4ff1b7800:src/Sonarr.Api.V3/openapi.json > baseline.json

Everything this fork does should come out as ADDED. Anything else is a client that stops working:
they read a field we removed, send a value we renamed, or parse a type we changed.
"""

import json
import sys
from collections import OrderedDict


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle, object_pairs_hook=OrderedDict)


def type_of(schema):
    """A short description of what a client would have to parse."""
    if schema is None:
        return "?"

    if "$ref" in schema:
        return schema["$ref"].rsplit("/", 1)[-1]

    kind = schema.get("type", "object")

    if kind == "array":
        return f"{type_of(schema.get('items'))}[]"

    fmt = schema.get("format")

    return f"{kind}/{fmt}" if fmt else kind


class Report:
    def __init__(self):
        self.added = []
        self.breaking = []
        self.review = []

    def add(self, what):
        self.added.append(what)

    def breaks(self, what):
        self.breaking.append(what)

    def check(self, what):
        self.review.append(what)


def compare_paths(base, ours, report):
    base_paths = base.get("paths", {})
    our_paths = ours.get("paths", {})

    for path, methods in base_paths.items():
        if path not in our_paths:
            report.breaks(f"endpoint removed: {path}")
            continue

        for method, operation in methods.items():
            if method not in our_paths[path]:
                report.breaks(f"method removed: {method.upper()} {path}")
                continue

            compare_parameters(path, method, operation, our_paths[path][method], report)

    for path, methods in our_paths.items():
        if path not in base_paths:
            report.add(f"endpoint added: {path}")
            continue

        for method in methods:
            if method not in base_paths[path]:
                report.add(f"method added: {method.upper()} {path}")


def compare_parameters(path, method, base_op, our_op, report):
    """A parameter a client still sends must still be accepted, and one it has never heard of must
    not become mandatory."""
    where = f"{method.upper()} {path}"

    base_params = {(p.get("name"), p.get("in")): p for p in base_op.get("parameters", []) or []}
    our_params = {(p.get("name"), p.get("in")): p for p in our_op.get("parameters", []) or []}

    for key, definition in base_params.items():
        name, location = key

        if key not in our_params:
            report.breaks(f"parameter removed: {where} {name} ({location})")
            continue

        was, now = type_of(definition.get("schema")), type_of(our_params[key].get("schema"))

        if was != now:
            report.breaks(f"parameter type changed: {where} {name}: {was} -> {now}")

    for key, definition in our_params.items():
        name, location = key

        if key not in base_params:
            if definition.get("required"):
                report.breaks(f"parameter added and required: {where} {name} ({location})")
            else:
                report.add(f"parameter added: {where} {name} ({location}, optional)")
        elif definition.get("required") and not base_params[key].get("required"):
            report.breaks(f"parameter now required: {where} {name} ({location})")


def compare_schemas(base, ours, report):
    base_schemas = base.get("components", {}).get("schemas", {})
    our_schemas = ours.get("components", {}).get("schemas", {})

    for name, schema in base_schemas.items():
        if name not in our_schemas:
            report.breaks(f"schema removed: {name}")
            continue

        base_props = schema.get("properties", {})
        our_props = our_schemas[name].get("properties", {})

        for prop, definition in base_props.items():
            if prop not in our_props:
                report.breaks(f"field removed: {name}.{prop}")
                continue

            was, now = type_of(definition), type_of(our_props[prop])

            if was != now:
                report.breaks(f"type changed: {name}.{prop}: {was} -> {now}")

            # A field that used to always be there and can now be null is a client dereferencing
            # nothing, so it is worth a look even though the field is still present.
            if definition.get("nullable") != our_props[prop].get("nullable"):
                report.check(
                    f"nullable changed: {name}.{prop}: "
                    f"{bool(definition.get('nullable'))} -> {bool(our_props[prop].get('nullable'))}"
                )

        was_required = set(schema.get("required", []))
        now_required = set(our_schemas[name].get("required", []))

        for prop in sorted(now_required - was_required):
            report.breaks(f"newly required: {name}.{prop}")

        for prop in sorted(was_required - now_required):
            report.check(f"no longer required: {name}.{prop}")

        for prop in our_props:
            if prop not in base_props:
                report.add(f"field added: {name}.{prop} ({type_of(our_props[prop])})")

        # Removing a value from an enum breaks a client that still sends it.
        was_enum = schema.get("enum")
        now_enum = our_schemas[name].get("enum")

        if was_enum and now_enum:
            for value in was_enum:
                if value not in now_enum:
                    report.breaks(f"enum value removed: {name}.{value}")

            for value in now_enum:
                if value not in was_enum:
                    report.add(f"enum value added: {name}.{value}")

    for name in our_schemas:
        if name not in base_schemas:
            report.add(f"schema added: {name}")


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2

    base, ours = load(sys.argv[1]), load(sys.argv[2])

    report = Report()
    compare_paths(base, ours, report)
    compare_schemas(base, ours, report)

    for title, items in (
        ("BREAKING", report.breaking),
        ("REVIEW", report.review),
        ("ADDED", report.added),
    ):
        print(f"\n=== {title} ({len(items)}) ===")

        for item in sorted(items):
            print(" ", item)

    print()

    if report.breaking:
        print(f"FAIL: {len(report.breaking)} change(s) a third-party client would notice")
        return 1

    print("OK: additions only")
    return 0


if __name__ == "__main__":
    sys.exit(main())
