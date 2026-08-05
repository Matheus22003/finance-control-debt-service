using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Social;

public sealed record CreateFriendRequest(
    Guid TargetUserId,
    string RequesterDisplayName,
    string RequesterEmail,
    string TargetDisplayName,
    string TargetEmail);

public sealed record FriendshipResponse(
    Guid Id,
    Guid RequesterUserId,
    string RequesterDisplayName,
    string RequesterEmail,
    Guid AddresseeUserId,
    string AddresseeDisplayName,
    string AddresseeEmail,
    FriendshipStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FriendResponse(
    Guid FriendshipId,
    Guid UserId,
    string DisplayName,
    string Email,
    DateTimeOffset FriendsSince);
