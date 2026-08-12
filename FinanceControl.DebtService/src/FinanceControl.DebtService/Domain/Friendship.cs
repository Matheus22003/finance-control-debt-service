namespace FinanceControl.DebtService.Domain;

public sealed class Friendship
{
    private Friendship()
    {
    }

    public Friendship(
        Guid requesterUserId,
        string requesterDisplayName,
        string requesterEmail,
        Guid addresseeUserId,
        string addresseeDisplayName,
        string addresseeEmail,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        RequesterUserId = requesterUserId;
        RequesterDisplayName = requesterDisplayName;
        RequesterEmail = requesterEmail;
        AddresseeUserId = addresseeUserId;
        AddresseeDisplayName = addresseeDisplayName;
        AddresseeEmail = addresseeEmail;
        PairKey = CreatePairKey(requesterUserId, addresseeUserId);
        Status = FriendshipStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public string RequesterDisplayName { get; private set; } = string.Empty;
    public string RequesterEmail { get; private set; } = string.Empty;
    public Guid AddresseeUserId { get; private set; }
    public string AddresseeDisplayName { get; private set; } = string.Empty;
    public string AddresseeEmail { get; private set; } = string.Empty;
    public string PairKey { get; private set; } = string.Empty;
    public FriendshipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        EnsureAddressee(userId);
        if (Status != FriendshipStatus.Pending)
        {
            throw new InvalidOperationException("Only pending friendship requests can be accepted.");
        }

        Status = FriendshipStatus.Accepted;
        UpdatedAt = now;
    }

    public void Reject(Guid userId, DateTimeOffset now)
    {
        EnsureAddressee(userId);
        if (Status != FriendshipStatus.Pending)
        {
            throw new InvalidOperationException("Only pending friendship requests can be rejected.");
        }

        Status = FriendshipStatus.Rejected;
        UpdatedAt = now;
    }

    public Guid OtherUserId(Guid userId) =>
        RequesterUserId == userId ? AddresseeUserId : RequesterUserId;

    public void UpdateUserSnapshot(Guid userId, string displayName, string email, DateTimeOffset now)
    {
        if (RequesterUserId == userId)
        {
            RequesterDisplayName = displayName;
            RequesterEmail = email;
        }
        else if (AddresseeUserId == userId)
        {
            AddresseeDisplayName = displayName;
            AddresseeEmail = email;
        }
        else
        {
            throw new InvalidOperationException("The user does not participate in this friendship.");
        }

        UpdatedAt = now;
    }

    public static string CreatePairKey(Guid firstUserId, Guid secondUserId)
    {
        var first = firstUserId.ToString("N");
        var second = secondUserId.ToString("N");
        return string.CompareOrdinal(first, second) < 0
            ? $"{first}:{second}"
            : $"{second}:{first}";
    }

    private void EnsureAddressee(Guid userId)
    {
        if (AddresseeUserId != userId)
        {
            throw new InvalidOperationException("Only the recipient can answer this friendship request.");
        }
    }
}
