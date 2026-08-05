namespace FinanceControl.DebtService.Domain;

public sealed class Person
{
    private Person()
    {
    }

    public Person(
        Guid ownerUserId,
        Guid? linkedUserId,
        string name,
        string? email,
        bool isCurrentUser,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        LinkedUserId = linkedUserId;
        Name = name;
        Email = email;
        IsCurrentUser = isCurrentUser;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? LinkedUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public bool IsCurrentUser { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? email,
        bool isCurrentUser,
        Guid? linkedUserId,
        DateTimeOffset now)
    {
        Name = name;
        Email = email;
        IsCurrentUser = isCurrentUser;
        LinkedUserId = linkedUserId;
        UpdatedAt = now;
    }

    public void SetCurrentUser(bool isCurrentUser, DateTimeOffset now)
    {
        IsCurrentUser = isCurrentUser;
        if (!isCurrentUser)
        {
            LinkedUserId = null;
        }
        UpdatedAt = now;
    }

    public void UpdateLinkedUserSnapshot(Guid userId, string displayName, string email, DateTimeOffset now)
    {
        if (LinkedUserId != userId)
        {
            throw new InvalidOperationException("The person is not linked to the supplied user.");
        }

        Name = displayName;
        Email = email;
        UpdatedAt = now;
    }

    public void AnonymizeDeletedUser(Guid userId, DateTimeOffset now)
    {
        if (OwnerUserId == userId)
        {
            OwnerUserId = Guid.Empty;
            IsCurrentUser = false;
        }

        if (LinkedUserId == userId)
        {
            LinkedUserId = null;
            Name = "Usuário removido";
            Email = null;
            IsCurrentUser = false;
        }

        UpdatedAt = now;
    }
}
