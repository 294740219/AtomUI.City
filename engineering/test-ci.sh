#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"
test_results="output/test-results/$configuration"

mkdir -p "$test_results"

dotnet test AtomUICity.slnx --configuration "$configuration" --no-build --filter "Category!=PlatformIntegration" --logger "trx;LogFilePrefix=ci-tests" --results-directory "$test_results"
