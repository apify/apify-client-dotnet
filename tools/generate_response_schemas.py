#!/usr/bin/env python3
"""Generates src/Apify.Client/Internal/ResponseSchemas.json from the Apify OpenAPI specification.

The client validates every successful JSON response against the schema the specification declares for the
operation (see ResponseSchemas.cs), so this file has to be regenerated whenever the client is moved to a new
specification version:

    python3 tools/generate_response_schemas.py [SPEC_URL_OR_PATH]

The default source is https://docs.apify.com/api/openapi.json. Only what validation needs is kept: types,
required properties, nested properties and items, composition keywords, references, and the value
constraints (format, pattern, minimum, maximum, minLength, maxLength). Documentation, examples and
keywords the client deliberately does not enforce (enum, const) are dropped, which also keeps the embedded
resource small. Enum values stay open on purpose so that a value the API adds later does not break
existing clients; the reference client's generated schemas do the same.
"""
import json
import pathlib
import sys
import urllib.request

DEFAULT_SPEC_URL = "https://docs.apify.com/api/openapi.json"
OUTPUT = pathlib.Path(__file__).resolve().parent.parent / "src" / "Apify.Client" / "Internal" / "ResponseSchemas.json"
HTTP_METHODS = ("get", "post", "put", "delete", "patch")
SCHEMA_REF_PREFIX = "#/components/schemas/"
# Keywords that carry structure the validator evaluates. Everything else is dropped.
SCHEMA_LIST_KEYWORDS = ("allOf", "anyOf", "oneOf")
# Scalar constraints copied as they are.
CONSTRAINT_KEYWORDS = ("format", "pattern", "minimum", "maximum", "minLength", "maxLength")
# Properties the API returns as `null` although the specification declares them non-nullable. The generator
# widens each of them to accept `null`, and fails once the specification itself allows `null` there, so a
# stale entry is removed instead of lingering. Mirrors the reference client's `ScheduleSpecNarrowings`.
NULLABLE_OVERRIDES = {
    # `uiState` is `null` once an API update has changed the schedule's `cronExpression` or `timezone` and the
    # Console editor state was dropped.
    "Schedule": ("uiState",),
}


def load_spec(source):
    if source.startswith(("http://", "https://")):
        with urllib.request.urlopen(source) as response:
            return json.load(response)
    with open(source, encoding="utf-8") as handle:
        return json.load(handle)


def slim(schema):
    """Reduces a schema to the keywords the client validates."""
    if not isinstance(schema, dict):
        return schema
    result = {}
    if "$ref" in schema:
        result["$ref"] = schema["$ref"]
    if "type" in schema:
        result["type"] = schema["type"]
    for keyword in CONSTRAINT_KEYWORDS:
        if keyword in schema:
            result[keyword] = schema[keyword]
    if "required" in schema:
        result["required"] = schema["required"]
    if "properties" in schema:
        result["properties"] = {name: slim(value) for name, value in schema["properties"].items()}
    if "items" in schema:
        result["items"] = slim(schema["items"])
    for keyword in SCHEMA_LIST_KEYWORDS:
        if keyword in schema:
            result[keyword] = [slim(value) for value in schema[keyword]]
    return result


def find_property(schema, name):
    """Returns the (properties dict) declaring `name` in a schema or in its allOf/anyOf/oneOf parts, or None."""
    if not isinstance(schema, dict):
        return None
    if name in schema.get("properties", {}):
        return schema["properties"]
    for keyword in SCHEMA_LIST_KEYWORDS:
        for part in schema.get(keyword, []):
            found = find_property(part, name)
            if found is not None:
                return found
    return None


def allows_null(schema):
    if not isinstance(schema, dict):
        return False
    declared_type = schema.get("type")
    if declared_type == "null" or (isinstance(declared_type, list) and "null" in declared_type):
        return True
    return any(allows_null(part) for keyword in ("anyOf", "oneOf") for part in schema.get(keyword, []))


def apply_nullable_overrides(schemas):
    for schema_name, property_names in NULLABLE_OVERRIDES.items():
        for property_name in property_names:
            properties = find_property(schemas.get(schema_name), property_name)
            if properties is None:
                raise SystemExit("nullable override targets a missing property: %s.%s" % (schema_name, property_name))
            if allows_null(properties[property_name]):
                raise SystemExit(
                    "stale nullable override: the specification already allows null in %s.%s" % (schema_name, property_name))
            properties[property_name] = {"anyOf": [properties[property_name], {"type": "null"}]}


def collect_refs(node, found):
    if isinstance(node, dict):
        ref = node.get("$ref")
        if isinstance(ref, str):
            if not ref.startswith(SCHEMA_REF_PREFIX):
                raise SystemExit("unsupported reference: " + ref)
            found.add(ref[len(SCHEMA_REF_PREFIX):])
        for value in node.values():
            collect_refs(value, found)
    elif isinstance(node, list):
        for value in node:
            collect_refs(value, found)


def success_json_schema(spec, operation):
    for status, response in operation.get("responses", {}).items():
        if not status.startswith("2"):
            continue
        if "$ref" in response:
            response = spec["components"]["responses"][response["$ref"].split("/")[-1]]
        content = response.get("content", {}).get("application/json")
        if content and "schema" in content:
            return content["schema"]
    return None


def main():
    spec = load_spec(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SPEC_URL)
    operations = {}
    for path, item in spec["paths"].items():
        for method in HTTP_METHODS:
            operation = item.get(method)
            schema = success_json_schema(spec, operation) if operation else None
            if schema is not None:
                operations[method.upper() + " " + path] = slim(schema)

    schemas = {}
    pending = set()
    collect_refs(operations, pending)
    while pending:
        name = pending.pop()
        if name in schemas:
            continue
        schemas[name] = slim(spec["components"]["schemas"][name])
        collect_refs(schemas[name], pending)

    apply_nullable_overrides(schemas)
    document = {"specVersion": spec["info"]["version"], "operations": operations, "schemas": dict(sorted(schemas.items()))}
    OUTPUT.write_text(json.dumps(document, separators=(",", ":"), sort_keys=True) + "\n", encoding="utf-8")
    print("wrote", OUTPUT, "(%d operations, %d schemas)" % (len(operations), len(schemas)))


if __name__ == "__main__":
    main()
