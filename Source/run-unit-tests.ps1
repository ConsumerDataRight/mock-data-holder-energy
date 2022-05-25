#Requires -PSEdition Core

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Output "***********************************************************"
Write-Output "MockDataHolder-Energy unit tests"
Write-Output "***********************************************************"

# Build and run containers
 docker-compose -f docker-compose.UnitTests.yml up --build --abort-on-container-exit --exit-code-from mock-data-holder-energy-unit-tests
$_lastExitCode = $LASTEXITCODE

# Stop containers
docker-compose -f docker-compose.UnitTests.yml down

if ($_lastExitCode -eq 0) {
    Write-Output "***********************************************************"
    Write-Output "✔ SUCCESS: MockDataHolder-Energy unit tests passed"
    Write-Output "***********************************************************"
    exit 0
}
else {
    Write-Output "***********************************************************"
    Write-Output "❌ FAILURE: MockDataHolder-Energy unit tests failed"
    Write-Output "***********************************************************"
    exit 1
}
