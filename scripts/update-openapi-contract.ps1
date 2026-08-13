[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionRoot = Join-Path $repositoryRoot 'FinanceControl.DebtService'
$contractPath = Join-Path $repositoryRoot 'openapi\openapi-v1.json'
$previousUpdatePath = $env:OPENAPI_CONTRACT_UPDATE_PATH

try {
    $env:OPENAPI_CONTRACT_UPDATE_PATH = $contractPath
    dotnet test `
        (Join-Path $solutionRoot 'tests\FinanceControl.DebtService.Tests\FinanceControl.DebtService.Tests.csproj') `
        --configuration Release `
        --filter 'FullyQualifiedName~OpenApiContractTests.RuntimeOpenApi_MatchesVersionedContract'

    if ($LASTEXITCODE -ne 0) {
        throw "OpenAPI contract generation failed with exit code $LASTEXITCODE."
    }

    Write-Host "OpenAPI contract updated at $contractPath"
}
finally {
    $env:OPENAPI_CONTRACT_UPDATE_PATH = $previousUpdatePath
}
