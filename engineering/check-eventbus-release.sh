#!/usr/bin/env bash
set -euo pipefail

configuration="Release"
restore=true
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$repository_root"

while [[ $# -gt 0 ]]; do
  case "$1" in
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

export CONFIGURATION="$configuration"

candidate_code_paths=(
  src/AtomUI.City.EventBus
  src/AtomUI.City.Generators
  tests/AtomUI.City.EventBus.Tests
  tests/AtomUI.City.Generators.Tests
  tests/AtomUI.City.Build.Tests
  fixtures/AtomUI.City.EventBus.HeadlessApp
  benchmarks/AtomUI.City.EventBus.Benchmarks
)

run_gate() {
  local gate_name="$1"
  shift

  printf 'Running EventBus RC gate: %s\n' "$gate_name"
  if ! "$@"; then
    printf 'EventBus RC gate failed: %s\n' "$gate_name" >&2
    exit 1
  fi
}

restore_candidate() {
  local project
  for project in \
    tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj \
    tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj \
    tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj \
    benchmarks/AtomUI.City.EventBus.Benchmarks/AtomUI.City.EventBus.Benchmarks.csproj; do
    dotnet restore "$project" -p:Configuration="$configuration"
  done
}

verify_candidate_format() {
  dotnet format AtomUICity.slnx \
    --verify-no-changes \
    --no-restore \
    --include "${candidate_code_paths[@]}"
}

run_stress_matrix() {
  local iteration
  for iteration in $(seq 1 20); do
    printf 'EventBus stress iteration %s/20\n' "$iteration"
    timeout 300s dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj \
      --configuration "$configuration" \
      --no-build \
      --no-restore \
      --filter 'FullyQualifiedName~EventChannelRuntimeTests|FullyQualifiedName~EventDispatchAndFailurePolicyTests|FullyQualifiedName~EventSubscriptionTests|FullyQualifiedName~EventBusHostIntegrationTests|FullyQualifiedName~EventBusPluginContractTests' \
      --logger 'console;verbosity=minimal'
  done
}

print_candidate_identity() {
  local manifest
  local fingerprint
  local dirty_state
  local path
  local hash
  manifest="$(mktemp "${TMPDIR:-/tmp}/atomuicity-eventbus-rc.XXXXXX")"

  while IFS= read -r path; do
    [[ -n "$path" ]] || continue
    case "$path" in
      */bin/*|*/obj/*|docs/modules/eventbus/release-candidate-report.md)
        continue
        ;;
    esac

    hash="$(sha256sum "$path" | awk '{print $1}')"
    printf '%s\t%s\n' "$path" "$hash"
  done < <(
    find \
      "${candidate_code_paths[@]}" \
      docs/modules/eventbus \
      engineering/package-consumers/eventbus \
      build \
      -type f -print
    printf '%s\n' \
      Directory.Build.props \
      Directory.Build.targets \
      Directory.Packages.props \
      global.json \
      engineering/check-dependency-boundaries.sh \
      engineering/check-docs.sh \
      engineering/check-eventbus-benchmarks.sh \
      engineering/check-eventbus-package-consumer.sh \
      engineering/check-eventbus-release.sh \
      engineering/check-project-inventory.sh \
      engineering/check-public-api.sh \
      engineering/check-release.sh \
      engineering/check-test-naming.sh \
      engineering/test-ci.sh \
      engineering/validate-packages.sh
  ) | sort -u > "$manifest"

  fingerprint="$(sha256sum "$manifest" | awk '{print $1}')"
  if [[ -n "$(git status --porcelain --untracked-files=all)" ]]; then
    dirty_state="dirty"
  else
    dirty_state="clean"
  fi

  printf 'EVENTBUS_RC_IDENTITY head=%s worktree=%s fingerprint=%s files=%s\n' \
    "$(git rev-parse HEAD)" \
    "$dirty_state" \
    "$fingerprint" \
    "$(wc -l < "$manifest" | tr -d ' ')"
  rm -f "$manifest"
}

if [[ "$restore" == true ]]; then
  run_gate "restore" restore_candidate
fi

run_gate "format" verify_candidate_format
run_gate "build" dotnet build src/AtomUI.City.EventBus/AtomUI.City.EventBus.csproj --configuration "$configuration" --no-restore -p:TreatWarningsAsErrors=true
run_gate "docs" bash engineering/check-docs.sh
run_gate "project-inventory" bash engineering/check-project-inventory.sh
run_gate "dependency-boundaries" bash engineering/check-dependency-boundaries.sh
run_gate "test-naming" bash engineering/check-test-naming.sh
run_gate "build-engineering-tests" dotnet test tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj --configuration "$configuration" --no-restore -p:TreatWarningsAsErrors=true
run_gate "eventbus-tests" dotnet test tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj --configuration "$configuration" --no-restore -p:TreatWarningsAsErrors=true
run_gate "eventbus-generator-tests" dotnet test tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj --configuration "$configuration" --no-restore --filter 'FullyQualifiedName~EventBus' -p:TreatWarningsAsErrors=true
run_gate "stress-20-rounds" run_stress_matrix
run_gate "public-api" bash engineering/check-public-api.sh
run_gate "package-consumer" bash engineering/check-eventbus-package-consumer.sh
run_gate "benchmarks" bash engineering/check-eventbus-benchmarks.sh
run_gate "candidate-identity" print_candidate_identity

printf 'EventBus Release Candidate gates completed for %s.\n' "$configuration"
