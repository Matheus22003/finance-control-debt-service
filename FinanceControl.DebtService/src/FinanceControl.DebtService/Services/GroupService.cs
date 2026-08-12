using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class GroupService(
    DebtDbContext dbContext,
    SocialConnectionService socialConnectionService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<GroupResponse>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var groups = await GroupQuery(tracking: false)
            .Where(group => group.Members.Any(member => member.UserId == userId))
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(ToResponse).ToList();
    }

    public async Task<GroupResponse> GetByIdAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken) =>
        ToResponse(await FindVisibleAsync(userId, groupId, tracking: false, cancellationToken));

    public async Task<GroupResponse> CreateAsync(
        Guid userId,
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Description);
        if (request.Owner.UserId != userId)
        {
            throw DomainValidationException.For("owner", "The authenticated user must own the group.");
        }

        var members = request.Members
            .Where(member => member.UserId != userId)
            .DistinctBy(member => member.UserId)
            .ToList();
        foreach (var member in members)
        {
            await EnsureFriendAsync(userId, member.UserId, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var group = new DebtGroup(
            request.Name.Trim(),
            NormalizeDescription(request.Description),
            userId,
            request.Owner.DisplayName.Trim(),
            request.Owner.Email.Trim().ToLowerInvariant(),
            now);
        foreach (var member in members)
        {
            group.AddMember(
                member.UserId,
                member.DisplayName.Trim(),
                member.Email.Trim().ToLowerInvariant(),
                userId,
                now);
        }

        dbContext.DebtGroups.Add(group);
        await EnsureContactsAsync(group.Members, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(group);
    }

    public async Task<GroupResponse> UpdateAsync(
        Guid userId,
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Description);
        var group = await FindOwnedAsync(userId, groupId, tracking: true, cancellationToken);
        group.Update(
            request.Name.Trim(),
            NormalizeDescription(request.Description),
            userId,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(group);
    }

    public async Task<GroupResponse> AddMemberAsync(
        Guid userId,
        Guid groupId,
        AddGroupMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = request.Member;
        await EnsureFriendAsync(userId, member.UserId, cancellationToken);
        var group = await FindOwnedAsync(userId, groupId, tracking: true, cancellationToken);
        var now = timeProvider.GetUtcNow();
        group.AddMember(
            member.UserId,
            member.DisplayName.Trim(),
            member.Email.Trim().ToLowerInvariant(),
            userId,
            now);
        await EnsureContactsAsync(group.Members, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(group);
    }

    public async Task RemoveMemberAsync(
        Guid userId,
        Guid groupId,
        Guid memberUserId,
        CancellationToken cancellationToken)
    {
        var group = await FindOwnedAsync(userId, groupId, tracking: true, cancellationToken);
        group.RemoveMember(memberUserId, userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await FindOwnedAsync(userId, groupId, tracking: true, cancellationToken);
        dbContext.DebtGroups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> UsersShareGroupAsync(
        Guid groupId,
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken) =>
        dbContext.DebtGroups.AnyAsync(
            group => group.Id == groupId &&
                     group.Members.Any(member => member.UserId == firstUserId) &&
                     group.Members.Any(member => member.UserId == secondUserId),
            cancellationToken);

    private async Task EnsureFriendAsync(
        Guid userId,
        Guid memberUserId,
        CancellationToken cancellationToken)
    {
        if (memberUserId == Guid.Empty ||
            !await socialConnectionService.AreFriendsAsync(userId, memberUserId, cancellationToken))
        {
            throw DomainValidationException.For(
                "members",
                "Only accepted friends can be added to a group.");
        }
    }

    private async Task EnsureContactsAsync(
        IReadOnlyCollection<DebtGroupMember> members,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var owner in members)
        {
            foreach (var linked in members.Where(member => member.UserId != owner.UserId))
            {
                await socialConnectionService.EnsureLinkedPersonAsync(
                    owner.UserId,
                    linked.UserId,
                    linked.DisplayName,
                    linked.Email,
                    now,
                    cancellationToken);
            }
        }
    }

    private IQueryable<DebtGroup> GroupQuery(bool tracking)
    {
        var query = dbContext.DebtGroups.Include(group => group.Members);
        return tracking ? query : query.AsNoTracking();
    }

    private async Task<DebtGroup> FindVisibleAsync(
        Guid userId,
        Guid groupId,
        bool tracking,
        CancellationToken cancellationToken) =>
        await GroupQuery(tracking).SingleOrDefaultAsync(
            group => group.Id == groupId && group.Members.Any(member => member.UserId == userId),
            cancellationToken)
        ?? throw new ResourceNotFoundException("The group was not found.");

    private async Task<DebtGroup> FindOwnedAsync(
        Guid userId,
        Guid groupId,
        bool tracking,
        CancellationToken cancellationToken) =>
        await GroupQuery(tracking).SingleOrDefaultAsync(
            group => group.Id == groupId && group.CreatedByUserId == userId,
            cancellationToken)
        ?? throw new ResourceNotFoundException("The group was not found.");

    private static void Validate(string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            errors["name"] = ["Name is required and must contain at most 120 characters."];
        }

        if (description?.Trim().Length > 500)
        {
            errors["description"] = ["Description must contain at most 500 characters."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static GroupResponse ToResponse(DebtGroup group) => new(
        group.Id,
        group.Name,
        group.Description,
        group.CreatedByUserId,
        group.CreatedAt,
        group.UpdatedAt,
        group.Members
            .OrderBy(member => member.Role)
            .ThenBy(member => member.DisplayName)
            .Select(member => new GroupMemberResponse(
                member.UserId,
                member.DisplayName,
                member.Email,
                member.Role,
                member.JoinedAt))
            .ToList());
}
