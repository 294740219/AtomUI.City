param(
    [switch]$SkipNativeAot,
    [int]$StressRounds = 20
)

$ErrorActionPreference = 'Stop'
if ($StressRounds -le 0) {
    throw 'StressRounds must be greater than zero.'
}

$project = Join-Path $PSScriptRoot 'AtomUI.City.CoreEventBus.DogfoodApp.csproj'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$expected = '{"status":"passed","modules":10,"services":31,"contracts":12,"handlers":20,"scenarios":6}'

dotnet build $project -c Release -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) {
    throw 'Dogfood Release build failed.'
}

for ($round = 1; $round -le $StressRounds; $round++) {
    $output = @(dotnet run --project $project -c Release --no-build -- verify-all 2>&1)
    if ($LASTEXITCODE -ne 0 -or $output[-1] -ne $expected) {
        $output | ForEach-Object { Write-Host $_ }
        throw "Dogfood verification round $round failed."
    }
}
Write-Host "DOGFOOD_STRESS rounds=$StressRounds status=passed"

if (-not $SkipNativeAot) {
    if (-not $IsWindows) {
        throw 'The current dogfood NativeAOT gate requires Windows and win-x64.'
    }

    $publishOutput = Join-Path $repositoryRoot 'output\core-eventbus-dogfood-aot'
    $intermediateOutput = Join-Path $repositoryRoot 'output\core-eventbus-dogfood-aot-obj'
    dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:AtomUICityDevelopTargetFramework=net8.0 `
        -p:AtomUICityCoreEventBusDogfoodPublishAot=true `
        -p:AtomUICityIsolatedIntermediateRoot=$intermediateOutput `
        -p:TreatWarningsAsErrors=true `
        -o $publishOutput
    if ($LASTEXITCODE -ne 0) {
        throw 'Dogfood NativeAOT publish failed.'
    }

    $executable = Join-Path $publishOutput 'AtomUI.City.CoreEventBus.DogfoodApp.exe'
    $nativeOutput = @(& $executable verify-all 2>&1)
    if ($LASTEXITCODE -ne 0 -or $nativeOutput[-1] -ne $expected) {
        $nativeOutput | ForEach-Object { Write-Host $_ }
        throw 'Dogfood NativeAOT execution failed.'
    }
    Write-Host 'DOGFOOD_NATIVEAOT tfm=net8.0 rid=win-x64 status=passed'
}
