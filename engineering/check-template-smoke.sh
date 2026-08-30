#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Debug}"
command_name="atomui city new app"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_source="$repository_root/output/NuGet/$configuration"
cli_path="$repository_root/output/bin/$configuration/AtomUI.City.Cli/net10.0/AtomUI.City.Cli.dll"
workspace="$(mktemp -d "${TMPDIR:-/tmp}/atomuicity-template-smoke.XXXXXX")"
export MSBUILDDISABLENODEREUSE=1
export NUGET_PACKAGES="$workspace/.nuget/packages"

package_source_for_dotnet="$package_source"
if command -v cygpath >/dev/null 2>&1; then
  package_source_for_dotnet="$(cygpath -w "$package_source")"
fi

cleanup() {
  if ! rm -rf "$workspace"; then
    sleep 1
    rm -rf "$workspace" || printf 'Could not fully remove smoke workspace: %s\n' "$workspace" >&2
  fi
}
trap cleanup EXIT

if [[ ! -d "$package_source" ]]; then
  printf 'Package source does not exist: %s\n' "$package_source" >&2
  exit 1
fi

if [[ ! -f "$cli_path" ]]; then
  printf 'CLI assembly does not exist: %s\n' "$cli_path" >&2
  exit 1
fi

cat > "$workspace/NuGet.Config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="AtomUICityLocal" value="$package_source_for_dotnet" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
CONFIG

printf 'Running %s smoke test in %s\n' "$command_name" "$workspace"

dotnet "$cli_path" city new app TemplateSmoke \
  --namespace Company.TemplateSmoke \
  --output "$workspace" \
  --json > "$workspace/new-app.json"

dotnet restore "$workspace/tests/TemplateSmoke.Tests/TemplateSmoke.Tests.csproj" \
  --configfile "$workspace/NuGet.Config"

dotnet build "$workspace/src/TemplateSmoke/TemplateSmoke.csproj" \
  --no-restore

dotnet test "$workspace/tests/TemplateSmoke.Tests/TemplateSmoke.Tests.csproj" \
  --no-restore

provider_analyzer_probe="$workspace/src/TemplateSmoke/ProviderAnalyzerViolation.cs"
provider_analyzer_log="$workspace/provider-analyzer-build.log"

cat > "$provider_analyzer_probe" <<'CS'
using Microsoft.Extensions.DependencyInjection;

namespace Company.TemplateSmoke;

internal static class ProviderAnalyzerViolation
{
    public static void CreateProvider(IServiceCollection services)
    {
        _ = new DefaultServiceProviderFactory().CreateServiceProvider(services);
    }
}
CS

if dotnet build "$workspace/src/TemplateSmoke/TemplateSmoke.csproj" \
  --no-restore > "$provider_analyzer_log" 2>&1; then
  printf 'Expected provider analyzer probe build to fail with AUCANL0001.\n' >&2
  cat "$provider_analyzer_log" >&2
  exit 1
fi

if ! grep -q "AUCANL0001" "$provider_analyzer_log"; then
  printf 'Provider analyzer probe build did not report AUCANL0001.\n' >&2
  cat "$provider_analyzer_log" >&2
  exit 1
fi

printf 'Provider analyzer package smoke reported AUCANL0001 as expected.\n'

rm -f "$provider_analyzer_probe"

host_analyzer_probe="$workspace/src/TemplateSmoke/HostAnalyzerViolation.cs"
host_analyzer_log="$workspace/host-analyzer-build.log"

cat > "$host_analyzer_probe" <<'CS'
using Microsoft.Extensions.Hosting;

namespace Company.TemplateSmoke;

internal static class HostAnalyzerViolation
{
    public static IHost CreateHost()
    {
        return Host.CreateApplicationBuilder().Build();
    }
}
CS

if dotnet build "$workspace/src/TemplateSmoke/TemplateSmoke.csproj" \
  --no-restore > "$host_analyzer_log" 2>&1; then
  printf 'Expected Generic Host analyzer probe build to fail with AUCANL0001.\n' >&2
  cat "$host_analyzer_log" >&2
  exit 1
fi

if ! grep -q "AUCANL0001" "$host_analyzer_log"; then
  printf 'Generic Host analyzer probe build did not report AUCANL0001.\n' >&2
  cat "$host_analyzer_log" >&2
  exit 1
fi

printf 'Generic Host analyzer package smoke reported AUCANL0001 as expected.\n'
