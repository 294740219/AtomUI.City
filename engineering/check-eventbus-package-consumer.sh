#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
version="$(sed -n 's:.*<AtomUICityVersion>\(.*\)</AtomUICityVersion>.*:\1:p' build/Version.props | head -n 1)"
repository_root="$(pwd -P)"
validation_root="$repository_root/output/eventbus-package-consumer"
local_feed="$validation_root/local-feed"
workspace="$validation_root/workspace"
packages="$validation_root/packages"
dotnet_home="$validation_root/dotnet-home"
template_root="$repository_root/engineering/package-consumers/eventbus"

if [[ -z "$version" ]]; then
  printf 'Unable to determine AtomUI.City version.\n' >&2
  exit 2
fi

if [[ ! -d "$template_root" ]]; then
  printf 'EventBus package consumer template is missing: %s\n' "$template_root" >&2
  exit 1
fi

mkdir -p "$local_feed" "$workspace" "$packages" "$dotnet_home"

case "$workspace|$packages" in
  "$repository_root/output/eventbus-package-consumer/workspace|$repository_root/output/eventbus-package-consumer/packages") ;;
  *)
    printf 'Refusing to clean unexpected consumer paths: workspace=%s packages=%s\n' "$workspace" "$packages" >&2
    exit 2
    ;;
esac

find "$local_feed" -maxdepth 1 -type f \( -name 'AtomUI.City.*.nupkg' -o -name 'AtomUI.City.*.snupkg' \) -exec rm -f {} +
find "$workspace" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
find "$packages" -mindepth 1 -maxdepth 1 -exec rm -rf {} +

dotnet pack src/AtomUI.City.Build/AtomUI.City.Build.csproj \
  --configuration "$configuration" \
  --output "$local_feed" \
  -p:TreatWarningsAsErrors=true
dotnet pack src/AtomUI.City.Core/AtomUI.City.Core.csproj \
  --configuration "$configuration" \
  --output "$local_feed" \
  -p:TreatWarningsAsErrors=true
dotnet pack src/AtomUI.City.EventBus/AtomUI.City.EventBus.csproj \
  --configuration "$configuration" \
  --output "$local_feed" \
  -p:TreatWarningsAsErrors=true

for package_name in AtomUI.City.Build AtomUI.City.Core AtomUI.City.EventBus; do
  package_path="$local_feed/$package_name.$version.nupkg"
  if [[ ! -f "$package_path" ]]; then
    printf 'Local EventBus candidate package is missing: %s\n' "$package_path" >&2
    exit 1
  fi
done

eventbus_package="$local_feed/AtomUI.City.EventBus.$version.nupkg"
eventbus_entries="$(unzip -Z1 "$eventbus_package")"
eventbus_nuspec="$(unzip -p "$eventbus_package" 'AtomUI.City.EventBus.nuspec')"
for required_entry in \
  lib/net8.0/AtomUI.City.EventBus.dll \
  lib/net8.0/AtomUI.City.EventBus.pdb \
  lib/net8.0/AtomUI.City.EventBus.xml \
  lib/net10.0/AtomUI.City.EventBus.dll \
  lib/net10.0/AtomUI.City.EventBus.pdb \
  lib/net10.0/AtomUI.City.EventBus.xml \
  LICENSE README.nuget.md RELEASE_NOTES.md; do
  if ! grep -Fxq "$required_entry" <<< "$eventbus_entries"; then
    printf 'EventBus package is missing required entry: %s\n' "$required_entry" >&2
    exit 1
  fi
done

if grep -Eq '(^|/)(tests|fixtures|benchmarks|analyzers)/|AtomUI\.City\.Generators' <<< "$eventbus_entries"; then
  printf 'EventBus package contains a forbidden test, fixture, benchmark, or analyzer asset.\n' >&2
  exit 1
fi

if ! grep -Fq "<dependency id=\"AtomUI.City.Core\" version=\"$version\"" <<< "$eventbus_nuspec"; then
  printf 'EventBus package does not depend on the matching Core candidate version.\n' >&2
  exit 1
fi

cp "$template_root/EventBus.PackageConsumer.csproj.template" "$workspace/EventBus.PackageConsumer.csproj"
cp "$template_root/NuGet.Config.template" "$workspace/NuGet.Config"
cp "$template_root/Program.cs.template" "$workspace/Program.cs"
cp "$template_root/Directory.Build.props.template" "$workspace/Directory.Build.props"
cp "$template_root/Directory.Build.targets.template" "$workspace/Directory.Build.targets"
cp "$template_root/Directory.Packages.props.template" "$workspace/Directory.Packages.props"

if grep -Rq '<ProjectReference' "$workspace"; then
  printf 'The isolated EventBus package consumer must not contain ProjectReference.\n' >&2
  exit 1
fi

export NUGET_PACKAGES="$packages"
export DOTNET_CLI_HOME="$dotnet_home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
dotnet restore "$workspace/EventBus.PackageConsumer.csproj" \
  --configfile "$workspace/NuGet.Config" \
  --no-http-cache \
  --runtime win-x64 \
  -p:AtomUICityConsumerVersion="$version"
dotnet publish "$workspace/EventBus.PackageConsumer.csproj" \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --no-restore \
  --output "$workspace/publish" \
  -p:AtomUICityConsumerVersion="$version" \
  -p:TreatWarningsAsErrors=true

assets_file="$workspace/obj/project.assets.json"
for package_identity in \
  "AtomUI.City.Build/$version" \
  "AtomUI.City.Core/$version" \
  "AtomUI.City.EventBus/$version"; do
  if ! grep -Fq "\"$package_identity\"" "$assets_file"; then
    printf 'The isolated consumer assets file is missing local package: %s\n' "$package_identity" >&2
    exit 1
  fi
done

consumer_output="$("$workspace/publish/EventBus.PackageConsumer.exe")"
if ! grep -Fq 'EVENTBUS_PACKAGE_CONSUMER_OK' <<< "$consumer_output"; then
  printf 'The isolated EventBus package consumer did not report success.\n%s\n' "$consumer_output" >&2
  exit 1
fi

printf '%s\n' "$consumer_output"
printf 'EventBus local package consumer gate passed with isolated package cache: %s\n' "$packages"
