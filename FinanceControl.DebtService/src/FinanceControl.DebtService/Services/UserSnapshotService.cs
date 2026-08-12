using System.ComponentModel.DataAnnotations;
using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class UserSnapshotService(DebtDbContext dbContext, TimeProvider timeProvider)
{
    public async Task UpdateAsync(
        Guid userId,
        UserSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(userId, request);
        if (errors.Count > 0) throw new DomainValidationException(errors);

        var people = await dbContext.People
            .Where(person => person.LinkedUserId == userId)
            .ToListAsync(cancellationToken);
        var friendships = await dbContext.Friendships
            .Where(friendship => friendship.RequesterUserId == userId || friendship.AddresseeUserId == userId)
            .ToListAsync(cancellationToken);
        var groupMembers = await dbContext.DebtGroupMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var person in people)
            person.UpdateLinkedUserSnapshot(userId, request.DisplayName.Trim(), request.Email.Trim(), now);
        foreach (var friendship in friendships)
            friendship.UpdateUserSnapshot(userId, request.DisplayName.Trim(), request.Email.Trim(), now);
        foreach (var member in groupMembers)
            member.UpdateSnapshot(request.DisplayName.Trim(), request.Email.Trim());

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> Validate(Guid userId, UserSnapshotRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (userId == Guid.Empty || request.UserId != userId)
            errors["userId"] = ["User id must match the route."];
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors["displayName"] = ["Display name is required."];
        else if (request.DisplayName.Trim().Length > 120)
            errors["displayName"] = ["Display name must contain at most 120 characters."];
        if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email))
            errors["email"] = ["Email must be valid."];
        return errors;
    }
}
