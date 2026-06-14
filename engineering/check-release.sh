#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"
restore=true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      configuration="$2"
      shift 2
      ;;
    --no-restore)
      restore=false
      shift
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      exit 2
      ;;
  esac
done

run_gate() {
  local gate_name="$1"
  shift

  printf 'Running release gate: %s\n' "$gate_name"
  if ! "$@"; then
    printf 'release gate failed: %s\n' "$gate_name" >&2
    exit 1
  fi
}

if [[ "$restore" == true ]]; then
  run_gate "restore" dotnet restore AtomUICity.slnx
fi

run_gate "format" dotnet format AtomUICity.slnx --verify-no-changes --no-restore
run_gate "build" dotnet build AtomUICity.slnx --no-restore
run_gate "docs" bash engineering/check-docs.sh
run_gate "license" bash engineering/check-license.sh
run_gate "project-inventory" bash engineering/check-project-inventory.sh
run_gate "dependency-boundaries" bash engineering/check-dependency-boundaries.sh
run_gate "test-naming" bash engineering/check-test-naming.sh
run_gate "public-api" bash engineering/check-public-api.sh
run_gate "test" bash engineering/test-ci.sh
run_gate "release-notes" bash engineering/generate-release-notes.sh
run_gate "pack" bash engineering/pack.sh --configuration "$configuration" --no-build
run_gate "package-validation" bash engineering/validate-packages.sh --configuration "$configuration"
run_gate "template-smoke" bash engineering/check-template-smoke.sh

printf 'Release gates completed for %s configuration.\n' "$configuration"
