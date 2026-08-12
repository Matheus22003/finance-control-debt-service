using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class SocialConnectionService(DebtDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var friendships = await dbContext.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted &&
                                 (friendship.RequesterUserId == userId ||
                                  friendship.AddresseeUserId == userId))
            .OrderBy(friendship => friendship.UpdatedAt)
            .ToListAsync(cancellationToken);

        return friendships.Select(friendship => ToFriendResponse(friendship, userId)).ToList();
    }

    public Task<List<FriendshipResponse>> GetIncomingRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        GetRequestsAsync(friendship => friendship.AddresseeUserId == userId, cancellationToken);

    public Task<List<FriendshipResponse>> GetOutgoingRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        GetRequestsAsync(friendship => friendship.RequesterUserId == userId, cancellationToken);

    public async Task<FriendshipResponse> CreateRequestAsync(
        Guid userId,
        CreateFriendRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(userId, request);
        var pairKey = Friendship.CreatePairKey(userId, request.TargetUserId);
        var existing = await dbContext.Friendships
            .SingleOrDefaultAsync(friendship => friendship.PairKey == pairKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status != FriendshipStatus.Rejected)
            {
                throw new InvalidOperationException(
                    existing.Status == FriendshipStatus.Accepted
                        ? "These users are already friends."
                        : "A friendship request already exists between these users.");
            }

            dbContext.Friendships.Remove(existing);
        }

        var friendship = new Friendship(
            userId,
            request.RequesterDisplayName.Trim(),
            request.RequesterEmail.Trim().ToLowerInvariant(),
            request.TargetUserId,
            request.TargetDisplayName.Trim(),
            request.TargetEmail.Trim().ToLowerInvariant(),
            timeProvider.GetUtcNow());
        dbContext.Friendships.Add(friendship);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(friendship);
    }

    public async Task<FriendshipResponse> AcceptAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        var friendship = await FindIncomingPendingAsync(userId, friendshipId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        friendship.Accept(userId, now);
        await EnsureCurrentPersonAsync(
            friendship.RequesterUserId,
            friendship.RequesterDisplayName,
            friendship.RequesterEmail,
            now,
            cancellationToken);
        await EnsureCurrentPersonAsync(
            friendship.AddresseeUserId,
            friendship.AddresseeDisplayName,
            friendship.AddresseeEmail,
            now,
            cancellationToken);
        await EnsureLinkedPersonAsync(
            friendship.RequesterUserId,
            friendship.AddresseeUserId,
            friendship.AddresseeDisplayName,
            friendship.AddresseeEmail,
            now,
            cancellationToken);
        await EnsureLinkedPersonAsync(
            friendship.AddresseeUserId,
            friendship.RequesterUserId,
            friendship.RequesterDisplayName,
            friendship.RequesterEmail,
            now,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(friendship);
    }

    public async Task<FriendshipResponse> RejectAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        var friendship = await FindIncomingPendingAsync(userId, friendshipId, cancellationToken);
        friendship.Reject(userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(friendship);
    }

    public async Task RemoveFriendAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken)
    {
        var pairKey = Friendship.CreatePairKey(userId, friendUserId);
        var friendship = await dbContext.Friendships.SingleOrDefaultAsync(
            candidate => candidate.PairKey == pairKey && candidate.Status == FriendshipStatus.Accepted,
            cancellationToken)
            ?? throw new ResourceNotFoundException("The friendship was not found.");
        dbContext.Friendships.Remove(friendship);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> AreFriendsAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        var pairKey = Friendship.CreatePairKey(firstUserId, secondUserId);
        return dbContext.Friendships.AnyAsync(
            friendship => friendship.PairKey == pairKey &&
                          friendship.Status == FriendshipStatus.Accepted,
            cancellationToken);
    }

    public async Task EnsureLinkedPersonAsync(
        Guid ownerUserId,
        Guid linkedUserId,
        string displayName,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.People.AnyAsync(
            person => person.OwnerUserId == ownerUserId && person.LinkedUserId == linkedUserId,
            cancellationToken);
        if (!exists)
        {
            dbContext.People.Add(new Person(
                ownerUserId,
                linkedUserId,
                displayName.Trim(),
                email.Trim().ToLowerInvariant(),
                isCurrentUser: false,
                now));
        }
    }

    private async Task EnsureCurrentPersonAsync(
        Guid userId,
        string displayName,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.People.AnyAsync(
            person => person.OwnerUserId == userId && person.LinkedUserId == userId,
            cancellationToken);
        if (!exists)
        {
            dbContext.People.Add(new Person(
                userId,
                userId,
                displayName.Trim(),
                email.Trim().ToLowerInvariant(),
                isCurrentUser: true,
                now));
        }
    }

    private async Task<Friendship> FindIncomingPendingAsync(
        Guid userId,
        Guid friendshipId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Friendships.SingleOrDefaultAsync(
            friendship => friendship.Id == friendshipId &&
                          friendship.AddresseeUserId == userId &&
                          friendship.Status == FriendshipStatus.Pending,
            cancellationToken)
            ?? throw new ResourceNotFoundException("The pending friendship request was not found.");
    }

    private Task<List<FriendshipResponse>> GetRequestsAsync(
        System.Linq.Expressions.Expression<Func<Friendship, bool>> ownershipPredicate,
        CancellationToken cancellationToken) =>
        dbContext.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.Status == FriendshipStatus.Pending)
            .Where(ownershipPredicate)
            .OrderByDescending(friendship => friendship.CreatedAt)
            .Select(friendship => ToResponse(friendship))
            .ToListAsync(cancellationToken);

    private static void ValidateRequest(Guid userId, CreateFriendRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.TargetUserId == Guid.Empty || request.TargetUserId == userId)
        {
            errors["targetUserId"] = ["A different target user is required."];
        }

        if (string.IsNullOrWhiteSpace(request.RequesterDisplayName) ||
            string.IsNullOrWhiteSpace(request.TargetDisplayName))
        {
            errors["displayName"] = ["Both user display names are required."];
        }

        if (string.IsNullOrWhiteSpace(request.RequesterEmail) ||
            string.IsNullOrWhiteSpace(request.TargetEmail))
        {
            errors["email"] = ["Both user emails are required."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }

    private static FriendshipResponse ToResponse(Friendship friendship) => new(
        friendship.Id,
        friendship.RequesterUserId,
        friendship.RequesterDisplayName,
        friendship.RequesterEmail,
        friendship.AddresseeUserId,
        friendship.AddresseeDisplayName,
        friendship.AddresseeEmail,
        friendship.Status,
        friendship.CreatedAt,
        friendship.UpdatedAt);

    private static FriendResponse ToFriendResponse(Friendship friendship, Guid userId) =>
        friendship.RequesterUserId == userId
            ? new FriendResponse(
                friendship.Id,
                friendship.AddresseeUserId,
                friendship.AddresseeDisplayName,
                friendship.AddresseeEmail,
                friendship.UpdatedAt)
            : new FriendResponse(
                friendship.Id,
                friendship.RequesterUserId,
                friendship.RequesterDisplayName,
                friendship.RequesterEmail,
                friendship.UpdatedAt);
}
