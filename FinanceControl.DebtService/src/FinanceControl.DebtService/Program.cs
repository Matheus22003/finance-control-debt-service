using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceControl.DebtService.Endpoints;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using FinanceControl.DebtService.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<DebtServiceExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

var debtDatabaseConnectionString = builder.Configuration.GetConnectionString("DebtDatabase");
if (!builder.Environment.IsEnvironment("Testing"))
{
    if (string.IsNullOrWhiteSpace(debtDatabaseConnectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DebtDatabase must be configured.");
    }

    builder.Services.AddDbContext<DebtDbContext>(options =>
        options.UseNpgsql(debtDatabaseConnectionString));
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PersonService>();
builder.Services.AddScoped<DebtManagementService>();
builder.Services.AddScoped<DebtSummaryService>();
builder.Services.AddScoped<SettlementSimplificationService>();
builder.Services.AddScoped<SettlementTransferService>();
builder.Services.AddScoped<SocialConnectionService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<UserSnapshotService>();
builder.Services.AddScoped<AccountDeletionService>();

builder.Services.AddHealthChecks();

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Finance Control - Debt Service",
            Version = "v1",
            Description = "Debt, person, payment and debt history API."
        };

        return Task.CompletedTask;
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DebtDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Finance Control Debt Service v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

var apiV1 = app.MapGroup("/api/v1");
apiV1.MapPersonEndpoints();
apiV1.MapDebtEndpoints();
apiV1.MapSocialEndpoints();
apiV1.MapInternalUserEndpoints();

app.Run();

public partial class Program;
