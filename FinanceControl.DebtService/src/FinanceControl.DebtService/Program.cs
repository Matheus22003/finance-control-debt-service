using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// =======================
// JWT Authentication
// =======================
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key is missing in configuration.");
if (string.IsNullOrWhiteSpace(jwtIssuer))
    throw new InvalidOperationException("Jwt:Issuer is missing in configuration.");
if (string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Jwt:Audience is missing in configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// =======================
// OpenAPI (native .NET 10) + Bearer scheme
// =======================
builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Finance Control — Debt Service",
            Version = "v1",
            Description = "Microserviço responsável por dívidas entre pessoas."
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// =======================
// Dev-only docs (OpenAPI + Scalar + Swagger UI viewer)
// =======================
if (app.Environment.IsDevelopment())
{
    // OpenAPI JSON
    app.MapOpenApi("/openapi/{documentName}.json");

    // Scalar UI
    app.MapScalarApiReference();

    // Swagger UI viewer (requires Swashbuckle.AspNetCore)
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "FinanceControl.DebtService v1");
        c.RoutePrefix = "swagger";
    });
}

// =======================
// Endpoints
// =======================

// Health (public)
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "debt-service" }))
    .AllowAnonymous()
    .WithTags("Health")
    .WithOpenApi();

// Debts summary (protected)
var debts = app.MapGroup("/api/v1/debts")
    .RequireAuthorization()
    .WithTags("Debts");

debts.MapGet("/summary", () =>
    {
        // Mock por enquanto
        return Results.Ok(new
        {
            totalOwed = 420.00m,
            totalToReceive = 180.00m,
            openDebtsCount = 3
        });
    })
    .WithName("GetDebtsSummary")
    .WithOpenApi();

app.Run();

// =======================
// OpenAPI transformer: add Bearer security scheme
// =======================
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        var hasBearer = schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme || s.Name == "Bearer");
        if (!hasBearer) return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        // Upsert (não sobrescreve outros schemes)
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT",
            Description = "Enter: Bearer {your JWT token}"
        };

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
    }
}