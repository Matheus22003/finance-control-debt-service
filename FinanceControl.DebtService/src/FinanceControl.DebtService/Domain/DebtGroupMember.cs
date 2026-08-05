namespace FinanceControl.DebtService.Domain;

public sealed class DebtGroupMember
{
    private DebtGroupMember()
    {
    }

    internal DebtGroupMember(
        Guid userId,
        string displayName,
        string email,
        GroupRole role,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DisplayName = displayName;
        Email = email;
        Role = role;
        JoinedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid DebtGroupId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public GroupRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public void UpdateSnapshot(string displayName, string email)
    {
        DisplayName = displayName;
        Email = email;
    }
}
