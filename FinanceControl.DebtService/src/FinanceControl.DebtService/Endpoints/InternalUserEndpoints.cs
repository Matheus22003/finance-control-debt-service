using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.DebtService.Endpoints;

public static class InternalUserEndpoints
{
    public static RouteGroupBuilder MapInternalUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/internal/account-data/deletion-eligibility", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                AccountDeletionService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.GetEligibilityAsync(userId, cancellationToken)))
            .WithTags("Internal")
            .Produces<Contracts.Users.AccountDeletionEligibilityResponse>();

        group.MapDelete("/internal/account-data", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                AccountDeletionService service,
                CancellationToken cancellationToken) =>
            {
                var eligibility = await service.DeleteAsync(userId, cancellationToken);
                return eligibility.CanDelete
                    ? Results.NoContent()
                    : Results.Conflict(eligibility);
            })
            .WithTags("Internal")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Contracts.Users.AccountDeletionEligibilityResponse>(StatusCodes.Status409Conflict);

        group.MapPut("/internal/user-snapshots/{userId:guid}", async (
                Guid userId,
                UserSnapshotRequest request,
                UserSnapshotService service,
                CancellationToken cancellationToken) =>
            {
                await service.UpdateAsync(userId, request, cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Internal")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        return group;
    }
}
