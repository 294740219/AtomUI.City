#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
project="benchmarks/AtomUI.City.EventBus.Benchmarks/AtomUI.City.EventBus.Benchmarks.csproj"
artifacts="output/eventbus-benchmark-gate"

if [[ ! -f "$project" ]]; then
  printf 'Missing EventBus benchmark project: %s\n' "$project" >&2
  exit 1
fi

mkdir -p "$artifacts"

dotnet run \
  --project "$project" \
  --configuration "$configuration" \
  -- \
  --filter '*' \
  --job short \
  --inProcess \
  --artifacts "$artifacts"

report_count="$(find "$artifacts" -name '*-report.csv' -type f | wc -l | tr -d ' ')"
if [[ "$report_count" -eq 0 ]]; then
  printf 'EventBus benchmark gate produced no CSV reports.\n' >&2
  exit 1
fi

printf 'EventBus benchmark gate passed with %s report file(s).\n' "$report_count"
