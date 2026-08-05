using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Contracts.Social;

public sealed record UserSnapshotRequest(Guid UserId, string DisplayName, string Email);

public sealed record CreateGroupRequest(
    string Name,
    string? Description,
    UserSnapshotRequest Owner,
    IReadOnlyList<UserSnapshotRequest> Members);

public sealed record UpdateGroupRequest(string Name, string? Description);

public sealed record AddGroupMemberRequest(UserSnapshotRequest Member);

public sealed record GroupResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GroupMemberResponse> Members);

public sealed record GroupMemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    GroupRole Role,
    DateTimeOffset JoinedAt);
