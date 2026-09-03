param(
    [string[]]$Frameworks = @("net10.0", "net8.0"),
    [string]$RuntimeIdentifier = "win-x64",
    [string]$DotnetExecutable = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "fixtures/AtomUI.City.Core.Mvp/AtomUI.City.Core.MvpCli/AtomUI.City.Core.MvpCli.csproj"

foreach ($framework in $Frameworks) {
    Write-Host "Restoring Core MVP NativeAOT: $framework/$RuntimeIdentifier"
    & $DotnetExecutable restore $project `
        -r $RuntimeIdentifier `
        -p:Configuration=Release `
        -p:AtomUICityCoreMvpPublishAot=true
    if ($LASTEXITCODE -ne 0) {
        throw "Core MVP NativeAOT restore failed for $framework/$RuntimeIdentifier."
    }

    Write-Host "Publishing Core MVP NativeAOT: $framework/$RuntimeIdentifier"
    $publishOutput = & $DotnetExecutable publish $project `
        -c Release `
        -f $framework `
        -r $RuntimeIdentifier `
        -p:AtomUICityCoreMvpPublishAot=true `
        --no-restore 2>&1
    $publishOutput | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Core MVP NativeAOT publish failed for $framework/$RuntimeIdentifier."
    }

    $aotWarnings = $publishOutput | Select-String -Pattern '\bIL[23]\d{3}\b|will always throw'
    if ($aotWarnings) {
        throw "Core MVP NativeAOT emitted trimming/AOT warnings for $framework/$RuntimeIdentifier."
    }

    $executable = Join-Path $repositoryRoot "output/bin/Release/AtomUI.City.Core.MvpCli/$framework/$RuntimeIdentifier/publish/AtomUI.City.Core.MvpCli.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Core MVP NativeAOT executable was not produced: $executable"
    }

    Write-Host "Running Core MVP NativeAOT matrix: $framework/$RuntimeIdentifier"
    $runOutput = & $executable verify --scenario all 2>&1
    $runOutput | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Core MVP NativeAOT process failed for $framework/$RuntimeIdentifier."
    }

    $jsonLine = $runOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1
    $result = $jsonLine | ConvertFrom-Json
    if (-not $result.success -or
        $result.selectedModuleCount -ne 6 -or
        $result.selectedServiceCount -ne 27 -or
        $result.permutationCount -ne 120 -or
        $result.combinationCount -ne 32 -or
        $result.concurrentScopeCount -ne 64) {
        throw "Core MVP NativeAOT result did not satisfy the industrial matrix for $framework/$RuntimeIdentifier."
    }
}

Write-Host "Core MVP NativeAOT gates passed for: $($Frameworks -join ', ')."
