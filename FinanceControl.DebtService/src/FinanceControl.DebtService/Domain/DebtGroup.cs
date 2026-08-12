namespace FinanceControl.DebtService.Domain;

public sealed class DebtGroup
{
    private readonly List<DebtGroupMember> _members = [];

    private DebtGroup()
    {
    }

    public DebtGroup(
        string name,
        string? description,
        Guid ownerUserId,
        string ownerDisplayName,
        string ownerEmail,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        CreatedByUserId = ownerUserId;
        CreatedAt = now;
        UpdatedAt = now;
        _members.Add(new DebtGroupMember(
            ownerUserId,
            ownerDisplayName,
            ownerEmail,
            GroupRole.Owner,
            now));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<DebtGroupMember> Members => _members;

    public void Update(string name, string? description, Guid userId, DateTimeOffset now)
    {
        EnsureOwner(userId);
        Name = name;
        Description = description;
        UpdatedAt = now;
    }

    public void AddMember(
        Guid userId,
        string displayName,
        string email,
        Guid actorUserId,
        DateTimeOffset now)
    {
        EnsureOwner(actorUserId);
        if (_members.Any(member => member.UserId == userId))
        {
            throw new InvalidOperationException("The user is already a member of this group.");
        }

        _members.Add(new DebtGroupMember(userId, displayName, email, GroupRole.Member, now));
        UpdatedAt = now;
    }

    public void RemoveMember(Guid userId, Guid actorUserId, DateTimeOffset now)
    {
        EnsureOwner(actorUserId);
        var member = _members.SingleOrDefault(candidate => candidate.UserId == userId)
            ?? throw new InvalidOperationException("The user is not a member of this group.");
        if (member.Role == GroupRole.Owner)
        {
            throw new InvalidOperationException("The group owner cannot be removed.");
        }

        _members.Remove(member);
        UpdatedAt = now;
    }

    private void EnsureOwner(Guid userId)
    {
        if (CreatedByUserId != userId)
        {
            throw new InvalidOperationException("Only the group owner can perform this operation.");
        }
    }
}
