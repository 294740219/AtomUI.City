#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
core_project="src/AtomUI.City.Core/AtomUI.City.Core.csproj"
shipped_api="src/AtomUI.City.Core/PublicAPI.Shipped.txt"
unshipped_api="src/AtomUI.City.Core/PublicAPI.Unshipped.txt"
validation_output="output/public-api/package-validation"

for required_file in "$core_project" "$shipped_api" "$unshipped_api"; do
  if [[ ! -f "$required_file" ]]; then
    printf 'Missing Core public API gate input: %s\n' "$required_file" >&2
    exit 1
  fi
done

shipped_signature_count="$(grep -cEv '^[[:space:]]*(#|$)' "$shipped_api" || true)"
if [[ "$shipped_signature_count" -eq 0 ]]; then
  printf 'Core shipped public API baseline is empty: %s\n' "$shipped_api" >&2
  exit 1
fi

if ! grep -q 'Microsoft.CodeAnalysis.PublicApiAnalyzers' "$core_project"; then
  printf 'Core must reference Microsoft.CodeAnalysis.PublicApiAnalyzers.\n' >&2
  exit 1
fi

if ! grep -q '<EnablePackageValidation>true</EnablePackageValidation>' "$core_project"; then
  printf 'Core must enable SDK package validation.\n' >&2
  exit 1
fi

dotnet restore "$core_project" -p:Configuration="$configuration"

dotnet build "$core_project" \
  --configuration "$configuration" \
  --no-restore \
  -p:TreatWarningsAsErrors=true

xml_document_count=0
xml_member_count=0
while IFS= read -r xml_document; do
  current_member_count="$(grep -c '<member name=' "$xml_document" || true)"
  if [[ "$current_member_count" -eq 0 ]]; then
    printf 'Core XML documentation has no API members: %s\n' "$xml_document" >&2
    exit 1
  fi

  xml_document_count=$((xml_document_count + 1))
  xml_member_count=$((xml_member_count + current_member_count))
done < <(find "output/bin/$configuration/AtomUI.City.Core" -name 'AtomUI.City.Core.xml' -type f | sort)

if [[ "$xml_document_count" -eq 0 ]]; then
  printf 'Core XML documentation was not produced for %s.\n' "$configuration" >&2
  exit 1
fi

sourcelink_document_count=0
while IFS= read -r sourcelink_document; do
  if ! grep -q 'https://raw.githubusercontent.com/' "$sourcelink_document"; then
    printf 'Core SourceLink document does not contain a canonical GitHub raw URL: %s\n' "$sourcelink_document" >&2
    exit 1
  fi

  sourcelink_document_count=$((sourcelink_document_count + 1))
done < <(find "output/AtomUI.City.Core/obj/$configuration" -name 'AtomUI.City.Core.sourcelink.json' -type f | sort)

if [[ "$sourcelink_document_count" -ne "$xml_document_count" ]]; then
  printf 'Core SourceLink count (%s) does not match built target framework count (%s).\n' \
    "$sourcelink_document_count" \
    "$xml_document_count" >&2
  exit 1
fi

mkdir -p "$validation_output"
dotnet pack "$core_project" \
  --configuration "$configuration" \
  --no-build \
  --no-restore \
  --output "$validation_output" \
  -p:TreatWarningsAsErrors=true

package_path="$(find "$validation_output" -maxdepth 1 -name 'AtomUI.City.Core.*.nupkg' ! -name '*.snupkg' -type f | sort | tail -n 1)"
if [[ -z "$package_path" ]]; then
  printf 'Core validation package was not produced.\n' >&2
  exit 1
fi

package_repository="$(unzip -p "$package_path" '*.nuspec' | grep -Eo '<repository[^>]+>' || true)"
head_revision="$(git rev-parse HEAD)"
if [[ "$package_repository" != *'url="https://github.com/'* ]] ||
   [[ "$package_repository" != *"commit=\"$head_revision\""* ]]; then
  printf 'Core package repository metadata is not a canonical GitHub URL at HEAD: %s\n' "$package_repository" >&2
  exit 1
fi

printf 'Core public API gate passed: %s frozen signatures, %s XML members and %s SourceLink document(s) across %s target framework(s).\n' \
  "$shipped_signature_count" \
  "$xml_member_count" \
  "$sourcelink_document_count" \
  "$xml_document_count"
