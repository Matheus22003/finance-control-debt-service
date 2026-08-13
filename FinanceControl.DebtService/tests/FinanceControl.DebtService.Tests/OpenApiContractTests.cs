using System.Text.Json;
using System.Text.Json.Nodes;

namespace FinanceControl.DebtService.Tests;

public sealed class OpenApiContractTests(DebtServiceApplicationFactory factory)
    : IClassFixture<DebtServiceApplicationFactory>
{
    private const string ContractFileName = "openapi-v1.json";

    [Fact]
    public async Task RuntimeOpenApi_MatchesVersionedContract()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("The runtime OpenAPI document was empty.");

        var updatePath = Environment.GetEnvironmentVariable("OPENAPI_CONTRACT_UPDATE_PATH");
        if (!string.IsNullOrWhiteSpace(updatePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(updatePath)
                ?? throw new InvalidOperationException("The contract update path has no directory."));
            await File.WriteAllTextAsync(
                updatePath,
                actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) +
                Environment.NewLine);
            return;
        }

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "Contracts", ContractFileName);
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))
            ?? throw new InvalidOperationException("The versioned OpenAPI contract was empty.");

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "The runtime OpenAPI document changed. Run scripts/update-openapi-contract.ps1, " +
            "review the diff and commit the updated contract.");
    }
}
