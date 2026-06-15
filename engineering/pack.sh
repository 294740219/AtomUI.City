#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"
no_build=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      configuration="$2"
      shift 2
      ;;
    --no-build)
      no_build=true
      shift
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      exit 2
      ;;
  esac
done

package_output="output/NuGet/$configuration"
log_output="output/logs/$configuration"
mkdir -p "$package_output" "$log_output"
find "$package_output" -maxdepth 1 -type f \( -name 'AtomUI.City.*.nupkg' -o -name 'AtomUI.City.*.snupkg' \) -exec rm -f {} +

while IFS= read -r project; do
  project_name="$(basename "$project" .csproj)"

  if [[ "$no_build" == true ]]; then
    dotnet restore "$project" -p:Configuration="$configuration"
    dotnet pack "$project" --configuration "$configuration" --output "$package_output" --no-build -p:TreatWarningsAsErrors=true -bl:$log_output/pack-$project_name.binlog
  else
    dotnet pack "$project" --configuration "$configuration" --output "$package_output" -p:TreatWarningsAsErrors=true -bl:$log_output/pack-$project_name.binlog
  fi
done < <(find src/AtomUI.City.* -name 'AtomUI.City.*.csproj' | sort)
