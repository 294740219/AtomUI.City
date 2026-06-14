#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

readonly runtime_excluded_projects="|AtomUI.City.Build|AtomUI.City.Cli|AtomUI.City.Generators|AtomUI.City.Templates|AtomUI.City.Testing|"
readonly forbidden_runtime_projects="|AtomUI.City.Build|AtomUI.City.Cli|AtomUI.City.Generators|AtomUI.City.Templates|AtomUI.City.Testing|"
readonly forbidden_runtime_packages="|Microsoft.CodeAnalysis|Microsoft.CodeAnalysis.CSharp|Microsoft.NET.Test.Sdk|ReactiveUI|Spectre.Console|System.Reactive|coverlet.collector|xunit|xunit.runner.visualstudio|"

failure_count=0

project_name_from_path() {
  basename "$1" .csproj
}

is_runtime_project() {
  local project_name="$1"

  [[ "$runtime_excluded_projects" != *"|$project_name|"* ]]
}

report_failure() {
  local message="$1"
  local project="$2"
  local dependency="$3"

  printf '%s: %s -> %s\n' "$message" "$project" "$dependency" >&2
  failure_count=$((failure_count + 1))
}

while IFS= read -r project_path; do
  project_name="$(project_name_from_path "$project_path")"

  while IFS= read -r include_path; do
    [[ -n "$include_path" ]] || continue

    normalized_include="${include_path//\\//}"
    referenced_project="$(basename "$normalized_include" .csproj)"

    if [[ "$normalized_include" == *"/tests/"* ]] ||
       [[ "$referenced_project" == *.Tests ]] ||
       [[ "$referenced_project" == *SmokeTests ]]; then
      report_failure "source project references test project" "$project_path" "$include_path"
    fi

    if is_runtime_project "$project_name" &&
       [[ "$forbidden_runtime_projects" == *"|$referenced_project|"* ]]; then
      report_failure "runtime project references forbidden project" "$project_path" "$referenced_project"
    fi
  done < <(grep -Eo '<ProjectReference Include="[^"]+"' "$project_path" | sed -E 's/^<ProjectReference Include="//')

  if is_runtime_project "$project_name"; then
    while IFS= read -r package_id; do
      [[ -n "$package_id" ]] || continue

      if [[ "$forbidden_runtime_packages" == *"|$package_id|"* ]]; then
        report_failure "runtime project references forbidden package" "$project_path" "$package_id"
      fi
    done < <(grep -Eo '<PackageReference Include="[^"]+"' "$project_path" | sed -E 's/^<PackageReference Include="//')
  fi
done < <(find src -name '*.csproj' -print | sort)

if [[ "$failure_count" -gt 0 ]]; then
  printf 'Dependency boundary validation failed with %s error(s).\n' "$failure_count" >&2
  exit 1
fi

printf 'Dependency boundaries validated for source projects.\n'
