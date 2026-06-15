#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

solution_path="AtomUICity.slnx"
tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/atomuicity-project-inventory.XXXXXX")"
trap 'rm -rf "$tmp_dir"' EXIT

find_repository_projects() {
  find src tests -path 'src/AtomUI.City.Templates/templates' -prune -o -name '*.csproj' -print
}

find_source_projects() {
  find src -path 'src/AtomUI.City.Templates/templates' -prune -o -name '*.csproj' -print
}

find_test_projects() {
  find tests -name '*.csproj' -print
}

has_source_project_implementation() {
  local project_path="$1"
  local project_dir

  project_dir="$(dirname "$project_path")"

  [[ -n "$(find "$project_dir" \
    \( -path "$project_dir/bin" -o -path "$project_dir/obj" \) -prune -o \
    \( -name '*.cs' -o -name '*.props' -o -name '*.targets' -o -path '*/.template.config/template.json' \) \
    -type f -print -quit)" ]]
}

grep -Eo 'Path="[^"]+\.csproj"' "$solution_path" \
  | sed -E 's/^Path="//; s/"$//' \
  | sort > "$tmp_dir/solution-projects.txt"

find_repository_projects \
  | sed -E 's#^\./##' \
  | sort > "$tmp_dir/repository-projects.txt"

failure_count=0

report_lines() {
  local prefix="$1"
  local file="$2"

  while IFS= read -r line; do
    if [[ -n "$line" ]]; then
      printf '%s: %s\n' "$prefix" "$line" >&2
      failure_count=$((failure_count + 1))
    fi
  done < "$file"
}

comm -23 "$tmp_dir/repository-projects.txt" "$tmp_dir/solution-projects.txt" > "$tmp_dir/missing-from-solution.txt"
comm -13 "$tmp_dir/repository-projects.txt" "$tmp_dir/solution-projects.txt" > "$tmp_dir/unknown-in-solution.txt"

report_lines "project missing from solution" "$tmp_dir/missing-from-solution.txt"
report_lines "solution references unknown project" "$tmp_dir/unknown-in-solution.txt"

find_source_projects \
  | sed -E 's#^src/[^/]+/##; s#\.csproj$##' \
  | sort > "$tmp_dir/source-project-names.txt"

find_test_projects \
  | sed -E 's#^tests/[^/]+/##; s#\.csproj$##' \
  | sort > "$tmp_dir/test-project-names.txt"

: > "$tmp_dir/expected-test-project-names.txt"
while IFS= read -r source_project_name; do
  if [[ "$source_project_name" == "AtomUI.City.Templates" ]]; then
    printf '%s\n' "AtomUI.City.TemplateSmokeTests"
  else
    printf '%s.Tests\n' "$source_project_name"
  fi
done < "$tmp_dir/source-project-names.txt" | sort > "$tmp_dir/expected-test-project-names.txt"

comm -23 "$tmp_dir/expected-test-project-names.txt" "$tmp_dir/test-project-names.txt" > "$tmp_dir/source-without-tests.txt"
comm -13 "$tmp_dir/expected-test-project-names.txt" "$tmp_dir/test-project-names.txt" > "$tmp_dir/orphan-tests.txt"

report_lines "source project without test project" "$tmp_dir/source-without-tests.txt"
report_lines "test project without source project" "$tmp_dir/orphan-tests.txt"

: > "$tmp_dir/source-placeholder-projects.txt"
while IFS= read -r source_project; do
  if ! has_source_project_implementation "$source_project"; then
    printf '%s\n' "$source_project" >> "$tmp_dir/source-placeholder-projects.txt"
  fi
done < <(find_source_projects | sort)

report_lines "source project without implementation files" "$tmp_dir/source-placeholder-projects.txt"

if [[ "$failure_count" -gt 0 ]]; then
  printf 'Project inventory validation failed with %s error(s).\n' "$failure_count" >&2
  exit 1
fi

printf 'Project inventory validated against %s.\n' "$solution_path"
