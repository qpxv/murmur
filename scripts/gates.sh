#!/bin/bash
# format check, lint, build and tests. all must pass before a change counts as done.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> swift format"
swift format lint --strict --recursive Package.swift Sources Tests

echo "==> swiftlint"
swiftlint lint --strict --quiet

echo "==> build (release)"
swift build -c release -Xswiftc -warnings-as-errors

echo "==> tests"
swift test

echo "==> all gates passed"
