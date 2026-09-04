#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

failure_count=0

report_failure() {
  local message="$1"
  local path="$2"
  local detail="$3"

  printf '%s: %s (%s)\n' "$message" "$path" "$detail" >&2
  failure_count=$((failure_count + 1))
}

expected_source_project_for_test_project() {
  local test_project_name="$1"

  if [[ "$test_project_name" == "AtomUI.City.TemplateSmokeTests" ]]; then
    printf '%s\n' "src/AtomUI.City.Templates/AtomUI.City.Templates.csproj"
    return
  fi

  if [[ "$test_project_name" == AtomUI.City.*.Tests ]]; then
    local source_project_name="${test_project_name%.Tests}"
    printf 'src/%s/%s.csproj\n' "$source_project_name" "$source_project_name"
    return
  fi

  printf '\n'
}

while IFS= read -r test_project_path; do
  test_project_directory="$(dirname "$test_project_path")"
  test_project_folder="$(basename "$test_project_directory")"
  test_project_name="$(basename "$test_project_path" .csproj)"

  if [[ "$test_project_folder" != "$test_project_name" ]]; then
    report_failure "test project folder does not match project file" "$test_project_path" "$test_project_folder"
  fi

  expected_source_project="$(expected_source_project_for_test_project "$test_project_name")"

  if [[ -z "$expected_source_project" ]] || [[ ! -f "$expected_source_project" ]]; then
    report_failure "test project without source module" "$test_project_path" "${expected_source_project:-unknown}"
  fi
done < <(find tests -name '*.csproj' -print | sort)

while IFS= read -r test_file; do
  test_file_name="$(basename "$test_file")"

  if [[ "$test_file_name" == "AssemblyInfo.cs" ]]; then
    continue
  fi

  if grep -q '\[CollectionDefinition' "$test_file" && ! grep -Eq '\[(Fact|Theory)\]' "$test_file"; then
    continue
  fi

  if grep -Eq '\[(Fact|Theory)\]' "$test_file" && [[ "$test_file_name" != *Tests.cs ]]; then
    report_failure "test file without Tests suffix" "$test_file" "$test_file_name"
  fi

  while IFS= read -r class_name; do
    [[ -n "$class_name" ]] || continue

    if [[ "$class_name" != *Tests ]]; then
      report_failure "public test class without Tests suffix" "$test_file" "$class_name"
    fi
  done < <(grep -Eo '^public[[:space:]]+(sealed[[:space:]]+|partial[[:space:]]+|sealed[[:space:]]+partial[[:space:]]+|partial[[:space:]]+sealed[[:space:]]+)?class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$test_file" \
    | awk '{print $NF}')
done < <(find tests -name '*.cs' -print | sort)

if [[ "$failure_count" -gt 0 ]]; then
  printf 'Test naming validation failed with %s error(s).\n' "$failure_count" >&2
  exit 1
fi

printf 'Test naming validated for test projects and files.\n'
