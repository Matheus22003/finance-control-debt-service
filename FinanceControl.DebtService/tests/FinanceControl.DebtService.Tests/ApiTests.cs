using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Contracts.People;
using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Persistence;
using FinanceControl.DebtService.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceControl.DebtService.Tests;

public sealed class ApiTests(DebtServiceApplicationFactory factory) : IClassFixture<DebtServiceApplicationFactory>
{
    private static readonly Guid DemoUserId =
        Guid.Parse("7f805b46-0b56-4a5d-86eb-d4f53c92db93");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    private readonly HttpClient _client = CreateClient(factory);

    private static HttpClient CreateClient(DebtServiceApplicationFactory applicationFactory)
    {
        var client = applicationFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Finance-Control-User-Id", DemoUserId.ToString());
        return client;
    }

    [Fact]
    public async Task Health_ReturnsServiceStatus()
    {
        await factory.ResetDatabaseAsync();

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal("debt-service", payload.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task PersonCrud_PersistsAndKeepsOnlyOneCurrentUser()
    {
        await factory.ResetDatabaseAsync();
        var first = await CreatePersonAsync("Me", "me@example.com", true);
        var second = await CreatePersonAsync("Ana", "ana@example.com", true);

        var people = await ReadAsync<IReadOnlyList<PersonResponse>>(
            await _client.GetAsync("/api/v1/people"));
        Assert.Equal(2, people.Count);
        Assert.False(people.Single(person => person.Id == first.Id).IsCurrentUser);
        Assert.True(people.Single(person => person.Id == second.Id).IsCurrentUser);

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/people/{first.Id}", new
        {
            name = "Current user",
            email = "current@example.com",
            isCurrentUser = true
        });
        var updated = await ReadAsync<PersonResponse>(updateResponse);
        Assert.Equal("Current user", updated.Name);
        Assert.True(updated.IsCurrentUser);

        var deleteResponse = await _client.DeleteAsync($"/api/v1/people/{second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/people/{second.Id}")).StatusCode);
    }

    [Fact]
    public async Task PeopleAreIsolatedByUser()
    {
        await factory.ResetDatabaseAsync();
        await CreatePersonAsync("Me", "me@example.com", true);

        using var anotherUserClient = factory.CreateClient();
        anotherUserClient.DefaultRequestHeaders.Add(
            "X-Finance-Control-User-Id",
            "8750c27d-a3ff-4c8f-997b-c6f230005040");
        var people = await ReadAsync<IReadOnlyList<PersonResponse>>(
            await anotherUserClient.GetAsync("/api/v1/people"));

        Assert.Empty(people);
    }

    [Fact]
    public async Task UserSnapshotUpdate_PropagatesToPeopleFriendshipsAndGroups()
    {
        await factory.ResetDatabaseAsync();
        var friendUserId = Guid.Parse("8750c27d-a3ff-4c8f-997b-c6f230005040");
        var request = await ReadAsync<FriendshipResponse>(await _client.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new
            {
                targetUserId = friendUserId,
                requesterDisplayName = "Old Name",
                requesterEmail = "old@example.com",
                targetDisplayName = "Friend User",
                targetEmail = "friend@example.com"
            }));
        using var friendClient = factory.CreateClient();
        friendClient.DefaultRequestHeaders.Add("X-Finance-Control-User-Id", friendUserId.ToString());
        (await friendClient.PostAsync($"/api/v1/friends/requests/{request.Id}/accept", null))
            .EnsureSuccessStatusCode();
        var group = await ReadAsync<GroupResponse>(await _client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Profile sync group",
            description = (string?)null,
            owner = new
            {
                userId = DemoUserId,
                displayName = "Old Name",
                email = "old@example.com"
            },
            members = new[]
            {
                new
                {
                    userId = friendUserId,
                    displayName = "Friend User",
                    email = "friend@example.com"
                }
            }
        }));

        var update = await _client.PutAsJsonAsync(
            $"/api/v1/internal/user-snapshots/{DemoUserId}",
            new
            {
                userId = DemoUserId,
                displayName = "New Name",
                email = "new@example.com"
            });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var friendView = await ReadAsync<IReadOnlyList<FriendResponse>>(
            await friendClient.GetAsync("/api/v1/friends"));
        var groupView = await ReadAsync<GroupResponse>(
            await friendClient.GetAsync($"/api/v1/groups/{group.Id}"));
        var people = await ReadAsync<IReadOnlyList<PersonResponse>>(
            await friendClient.GetAsync("/api/v1/people"));
        Assert.Contains(friendView, friend =>
            friend.UserId == DemoUserId && friend.DisplayName == "New Name" && friend.Email == "new@example.com");
        Assert.Contains(groupView.Members, member =>
            member.UserId == DemoUserId && member.DisplayName == "New Name" && member.Email == "new@example.com");
        Assert.Contains(people, person =>
            person.Name == "New Name" && person.Email == "new@example.com");
    }

    [Fact]
    public async Task AccountDeletion_BlocksOpenDebtAndAnonymizesCompletedHistory()
    {
        await factory.ResetDatabaseAsync();
        var payer = await CreatePersonAsync("Deleted user", "deleted@example.com", true);
        var participant = await CreatePersonAsync("Ana", "ana@example.com", false);
        var debt = await CreateDebtAsync(
            "Shared dinner",
            100m,
            payer.Id,
            "FOOD",
            [(participant.Id, 100m)]);

        var blocked = await ReadAsync<FinanceControl.DebtService.Contracts.Users.AccountDeletionEligibilityResponse>(
            await _client.GetAsync("/api/v1/internal/account-data/deletion-eligibility"));
        Assert.False(blocked.CanDelete);
        Assert.Equal(1, blocked.OpenDebtsCount);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync("/api/v1/internal/account-data")).StatusCode);

        var share = Assert.Single(debt.Shares);
        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/v1/debts/{debt.Id}/shares/{share.Id}/payments",
            new { amount = 100m, paymentDate = "2026-08-03", note = "PIX" });
        Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);

        var ready = await ReadAsync<FinanceControl.DebtService.Contracts.Users.AccountDeletionEligibilityResponse>(
            await _client.GetAsync("/api/v1/internal/account-data/deletion-eligibility"));
        Assert.True(ready.CanDelete);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync("/api/v1/internal/account-data")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync("/api/v1/internal/account-data")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DebtDbContext>();
        var persistedDebt = await dbContext.Debts
            .Include(candidate => candidate.Payments)
            .SingleAsync(candidate => candidate.Id == debt.Id);
        var deletedUserPerson = await dbContext.People.SingleAsync(person => person.Id == payer.Id);
        var retainedParticipant = await dbContext.People.SingleAsync(person => person.Id == participant.Id);
        Assert.Equal(Guid.Empty, persistedDebt.CreatedByUserId);
        Assert.Equal(Guid.Empty, Assert.Single(persistedDebt.Payments).RecordedByUserId);
        Assert.Equal(Guid.Empty, deletedUserPerson.OwnerUserId);
        Assert.Null(deletedUserPerson.LinkedUserId);
        Assert.Equal("Usuário removido", deletedUserPerson.Name);
        Assert.Null(deletedUserPerson.Email);
        Assert.Equal(Guid.Empty, retainedParticipant.OwnerUserId);
        Assert.Equal("Ana", retainedParticipant.Name);
    }

    [Fact]
    public async Task FriendshipAcceptanceCreatesLinkedContactsAndAllowsGroups()
    {
        await factory.ResetDatabaseAsync();
        var friendUserId = Guid.Parse("8750c27d-a3ff-4c8f-997b-c6f230005040");
        var requestResponse = await _client.PostAsJsonAsync("/api/v1/friends/requests", new
        {
            targetUserId = friendUserId,
            requesterDisplayName = "Demo User",
            requesterEmail = "demo@example.com",
            targetDisplayName = "Friend User",
            targetEmail = "friend@example.com"
        });
        var friendship = await ReadAsync<FriendshipResponse>(requestResponse);

        using var friendClient = factory.CreateClient();
        friendClient.DefaultRequestHeaders.Add(
            "X-Finance-Control-User-Id",
            friendUserId.ToString());
        var acceptResponse = await friendClient.PostAsync(
            $"/api/v1/friends/requests/{friendship.Id}/accept",
            content: null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var demoFriends = await ReadAsync<IReadOnlyList<FriendResponse>>(
            await _client.GetAsync("/api/v1/friends"));
        var friendPeople = await ReadAsync<IReadOnlyList<PersonResponse>>(
            await friendClient.GetAsync("/api/v1/people"));
        Assert.Contains(demoFriends, friend => friend.UserId == friendUserId);
        Assert.Contains(friendPeople, person => person.Name == "Demo User" && !person.IsCurrentUser);

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Trip",
            description = "Shared trip expenses",
            owner = new
            {
                userId = DemoUserId,
                displayName = "Demo User",
                email = "demo@example.com"
            },
            members = new[]
            {
                new
                {
                    userId = friendUserId,
                    displayName = "Friend User",
                    email = "friend@example.com"
                }
            }
        });
        var group = await ReadAsync<GroupResponse>(groupResponse);
        var friendGroups = await ReadAsync<IReadOnlyList<GroupResponse>>(
            await friendClient.GetAsync("/api/v1/groups"));

        Assert.Equal(2, group.Members.Count);
        Assert.Contains(friendGroups, candidate => candidate.Id == group.Id);

        var demoPeople = await ReadAsync<IReadOnlyList<PersonResponse>>(
            await _client.GetAsync("/api/v1/people"));
        var demoPerson = demoPeople.Single(person => person.IsCurrentUser);
        var friendPerson = demoPeople.Single(person => person.Email == "friend@example.com");
        var debt = await CreateDebtAsync(
            "Shared hotel",
            100m,
            demoPerson.Id,
            "TRAVEL",
            [(demoPerson.Id, 50m), (friendPerson.Id, 50m)],
            group.Id);
        var friendCurrentPerson = friendPeople.Single(person => person.IsCurrentUser);
        var demoContactForFriend = friendPeople.Single(person => person.Email == "demo@example.com");
        var reciprocalDebt = await ReadAsync<DebtResponse>(await friendClient.PostAsJsonAsync("/api/v1/debts", new
        {
            description = "Transport paid by friend",
            totalAmount = 20m,
            paidByPersonId = friendCurrentPerson.Id,
            groupId = group.Id,
            category = "TRANSPORT",
            dueDate = (string?)null,
            shares = new[]
            {
                new { personId = demoContactForFriend.Id, amount = 20m }
            }
        }));
        var ungroupedSettlement = await ReadAsync<SimplifiedSettlementResponse>(
            await _client.GetAsync("/api/v1/debts/settlements/simplified"));
        var groupSettlement = await ReadAsync<SimplifiedSettlementResponse>(
            await _client.GetAsync($"/api/v1/debts/settlements/simplified?groupId={group.Id}"));
        var friendGroupSettlement = await ReadAsync<SimplifiedSettlementResponse>(
            await friendClient.GetAsync($"/api/v1/debts/settlements/simplified?groupId={group.Id}"));

        Assert.Empty(ungroupedSettlement.Transfers);
        Assert.Equal(2, groupSettlement.OriginalTransferCount);
        Assert.Equal(1, groupSettlement.SimplifiedTransferCount);
        Assert.Equal(30m, groupSettlement.Transfers.Single().Amount);
        Assert.Equal(
            groupSettlement.SimplifiedTransferCount,
            friendGroupSettlement.SimplifiedTransferCount);

        using var unrelatedClient = factory.CreateClient();
        unrelatedClient.DefaultRequestHeaders.Add(
            "X-Finance-Control-User-Id",
            "e213f309-a284-4bd8-9678-d80135a68887");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await unrelatedClient.GetAsync(
                $"/api/v1/debts/settlements/simplified?groupId={group.Id}")).StatusCode);

        var friendShare = debt.Shares.Single(share => share.Person.Id == friendPerson.Id);
        var pendingPayment = await ReadAsync<PaymentResponse>(
            await friendClient.PostAsJsonAsync(
                $"/api/v1/debts/{debt.Id}/shares/{friendShare.Id}/payments",
                new { amount = 50m, paymentDate = "2026-08-01", note = "PIX" }));
        var beforeConfirmation = await ReadAsync<DebtResponse>(
            await _client.GetAsync($"/api/v1/debts/{debt.Id}"));
        var demoPendingConfirmations = await ReadAsync<IReadOnlyList<PaymentResponse>>(
            await _client.GetAsync("/api/v1/debts/payments/pending-confirmation"));
        var friendPendingConfirmations = await ReadAsync<IReadOnlyList<PaymentResponse>>(
            await friendClient.GetAsync("/api/v1/debts/payments/pending-confirmation"));

        Assert.Equal(PaymentStatus.Pending, pendingPayment.Status);
        Assert.Equal(50m, beforeConfirmation.Shares.Single(share => share.Id == friendShare.Id).RemainingAmount);
        Assert.Contains(demoPendingConfirmations, payment => payment.Id == pendingPayment.Id);
        Assert.DoesNotContain(friendPendingConfirmations, payment => payment.Id == pendingPayment.Id);

        var confirmedPayment = await ReadAsync<PaymentResponse>(
            await _client.PostAsync(
                $"/api/v1/debts/{debt.Id}/payments/{pendingPayment.Id}/confirm",
                content: null));
        var friendSummary = await ReadAsync<DebtSummaryResponse>(
            await friendClient.GetAsync("/api/v1/debts/summary"));

        Assert.Equal(PaymentStatus.Confirmed, confirmedPayment.Status);
        Assert.Equal(0m, friendSummary.TotalOwed);

        var history = await ReadAsync<IReadOnlyList<DebtHistoryResponse>>(
            await friendClient.GetAsync($"/api/v1/debts/{debt.Id}/history"));
        Assert.Equal(DebtHistoryType.PaymentConfirmed, history[^2].Type);
        Assert.Equal(DebtHistoryType.Paid, history[^1].Type);

        var finalDebt = await CreateDebtAsync(
            "Final group expense",
            50m,
            demoPerson.Id,
            "FOOD",
            [(friendPerson.Id, 50m)],
            group.Id);
        friendGroupSettlement = await ReadAsync<SimplifiedSettlementResponse>(
            await friendClient.GetAsync($"/api/v1/debts/settlements/simplified?groupId={group.Id}"));
        var suggestedTransfer = Assert.Single(friendGroupSettlement.Transfers);
        Assert.True(suggestedTransfer.FromPerson.IsCurrentUser);
        Assert.Equal(30m, suggestedTransfer.Amount);

        var recordedSettlement = await ReadAsync<SettlementTransferResponse>(
            await friendClient.PostAsJsonAsync(
                "/api/v1/debts/settlements/simplified/transfers",
                new
                {
                    groupId = group.Id,
                    fromPersonId = suggestedTransfer.FromPerson.Id,
                    toPersonId = suggestedTransfer.ToPerson.Id,
                    amount = suggestedTransfer.Amount,
                    paymentDate = "2026-08-02",
                    note = "Simplified PIX"
                }));
        var pendingSettlements = await ReadAsync<IReadOnlyList<SettlementTransferResponse>>(
            await _client.GetAsync(
                "/api/v1/debts/settlements/simplified/transfers/pending-confirmation"));

        Assert.Equal(SettlementTransferStatus.Pending, recordedSettlement.Status);
        Assert.Contains(pendingSettlements, transfer => transfer.Id == recordedSettlement.Id);

        var confirmedSettlement = await ReadAsync<SettlementTransferResponse>(
            await _client.PostAsync(
                $"/api/v1/debts/settlements/simplified/transfers/{recordedSettlement.Id}/confirm",
                content: null));
        var settledReciprocalDebt = await ReadAsync<DebtResponse>(
            await _client.GetAsync($"/api/v1/debts/{reciprocalDebt.Id}"));
        var settledFinalDebt = await ReadAsync<DebtResponse>(
            await _client.GetAsync($"/api/v1/debts/{finalDebt.Id}"));
        var activeTransfers = await ReadAsync<IReadOnlyList<SettlementTransferResponse>>(
            await _client.GetAsync(
                $"/api/v1/debts/settlements/simplified/transfers?groupId={group.Id}"));

        Assert.Equal(SettlementTransferStatus.Confirmed, confirmedSettlement.Status);
        Assert.Equal(DebtStatus.Paid, settledReciprocalDebt.Status);
        Assert.Equal(DebtStatus.Paid, settledFinalDebt.Status);
        Assert.Empty(activeTransfers);
    }

    [Fact]
    public async Task DebtPayments_UpdateStatusAndHistory()
    {
        await factory.ResetDatabaseAsync();
        var me = await CreatePersonAsync("Me", "me@example.com", true);
        var ana = await CreatePersonAsync("Ana", "ana@example.com", false);
        var debt = await CreateDebtAsync(
            "Shared dinner",
            200m,
            me.Id,
            "FOOD",
            [(me.Id, 100m), (ana.Id, 100m)]);
        var anaShare = debt.Shares.Single(share => share.Person.Id == ana.Id);
        Assert.Equal(DebtStatus.Open, debt.Status);
        Assert.Equal(100m, anaShare.RemainingAmount);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/v1/debts/{debt.Id}/shares/{anaShare.Id}/payments",
            new { amount = 100m, paymentDate = "2026-07-31", note = "PIX" });
        var payment = await ReadAsync<PaymentResponse>(paymentResponse);
        Assert.Equal(ana.Id, payment.FromPerson.Id);
        Assert.Equal(me.Id, payment.ToPerson.Id);
        Assert.Equal(100m, payment.Amount);

        var paidDebt = await GetDebtAsync(debt.Id);
        Assert.Equal(DebtStatus.Paid, paidDebt.Status);

        var history = await ReadAsync<IReadOnlyList<DebtHistoryResponse>>(
            await _client.GetAsync($"/api/v1/debts/{debt.Id}/history"));
        Assert.Contains(history, item => item.Type == DebtHistoryType.Created);
        Assert.Contains(history, item => item.Type == DebtHistoryType.PaymentConfirmed);
        Assert.Contains(history, item => item.Type == DebtHistoryType.Paid);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/debts/{debt.Id}/payments/{payment.Id}")).StatusCode);
        var reopenedDebt = await GetDebtAsync(debt.Id);
        Assert.Equal(DebtStatus.Open, reopenedDebt.Status);
        Assert.Equal(100m, reopenedDebt.Shares.Single(share => share.Id == anaShare.Id).RemainingAmount);

        history = await ReadAsync<IReadOnlyList<DebtHistoryResponse>>(
            await _client.GetAsync($"/api/v1/debts/{debt.Id}/history"));
        Assert.Contains(history, item => item.Type == DebtHistoryType.PaymentDeleted);
        Assert.Contains(history, item => item.Type == DebtHistoryType.Reopened);
    }

    [Fact]
    public async Task SummaryAndSimplification_AreCalculatedAcrossSharedExpenses()
    {
        await factory.ResetDatabaseAsync();
        var me = await CreatePersonAsync("Me", "me@example.com", true);
        var ana = await CreatePersonAsync("Ana", "ana@example.com", false);
        var bruno = await CreatePersonAsync("Bruno", "bruno@example.com", false);

        await CreateDebtAsync(
            "Paid by me",
            300m,
            me.Id,
            "FOOD",
            [(me.Id, 100m), (ana.Id, 100m), (bruno.Id, 100m)]);
        await CreateDebtAsync(
            "Paid by Ana for me",
            100m,
            ana.Id,
            "TRANSPORT",
            [(me.Id, 100m)]);
        await CreateDebtAsync(
            "Paid by Bruno for Ana",
            50m,
            bruno.Id,
            "OTHER",
            [(ana.Id, 50m)]);

        var summary = await ReadAsync<DebtSummaryResponse>(
            await _client.GetAsync("/api/v1/debts/summary"));
        Assert.Equal(100m, summary.TotalOwed);
        Assert.Equal(200m, summary.TotalToReceive);
        Assert.Equal(2, summary.OpenDebtsCount);

        var simplified = await ReadAsync<SimplifiedSettlementResponse>(
            await _client.GetAsync("/api/v1/debts/settlements/simplified"));
        Assert.Equal(350m, simplified.TotalOpenAmount);
        Assert.Equal(4, simplified.OriginalTransferCount);
        Assert.Equal(2, simplified.SimplifiedTransferCount);
        Assert.All(simplified.Transfers, transfer => Assert.Equal(me.Id, transfer.ToPerson.Id));
        Assert.Equal(100m, simplified.Transfers.Sum(transfer => transfer.Amount));
    }

    [Fact]
    public async Task Simplification_FindsTheMinimumInsteadOfOnlyUsingAGreedyMatch()
    {
        await factory.ResetDatabaseAsync();
        var debtors = new[]
        {
            await CreatePersonAsync("Debtor 6", null, false),
            await CreatePersonAsync("Debtor 4 A", null, false),
            await CreatePersonAsync("Debtor 4 B", null, false),
            await CreatePersonAsync("Debtor 4 C", null, false),
            await CreatePersonAsync("Debtor 2", null, false)
        };
        var creditors = new[]
        {
            await CreatePersonAsync("Creditor 8", null, false),
            await CreatePersonAsync("Creditor 6 A", null, false),
            await CreatePersonAsync("Creditor 6 B", null, false)
        };

        await CreateDebtAsync(
            "First creditor",
            8m,
            creditors[0].Id,
            "OTHER",
            [(debtors[0].Id, 6m), (debtors[1].Id, 2m)]);
        await CreateDebtAsync(
            "Second creditor",
            6m,
            creditors[1].Id,
            "OTHER",
            [(debtors[1].Id, 2m), (debtors[2].Id, 4m)]);
        await CreateDebtAsync(
            "Third creditor",
            6m,
            creditors[2].Id,
            "OTHER",
            [(debtors[3].Id, 4m), (debtors[4].Id, 2m)]);

        var simplified = await ReadAsync<SimplifiedSettlementResponse>(
            await _client.GetAsync("/api/v1/debts/settlements/simplified"));

        Assert.Equal(6, simplified.OriginalTransferCount);
        Assert.Equal(5, simplified.SimplifiedTransferCount);
        Assert.Equal(20m, simplified.Transfers.Sum(transfer => transfer.Amount));
    }

    [Fact]
    public async Task DebtCrud_UpdatesDetailsAndDeletesAggregate()
    {
        await factory.ResetDatabaseAsync();
        var payer = await CreatePersonAsync("Payer", null, true);
        var participant = await CreatePersonAsync("Participant", null, false);
        var addedParticipant = await CreatePersonAsync("Added participant", null, false);
        var debt = await CreateDebtAsync(
            "Trip",
            150m,
            payer.Id,
            "TRAVEL",
            [(participant.Id, 150m)]);

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/debts/{debt.Id}", new
        {
            description = "Updated trip",
            paidByPersonId = payer.Id,
            category = "OTHER",
            dueDate = "2026-08-15",
            shares = new[]
            {
                new { personId = addedParticipant.Id, amount = 150m }
            }
        });
        var updated = await ReadAsync<DebtResponse>(updateResponse);
        Assert.Equal("Updated trip", updated.Description);
        Assert.Equal(DebtCategory.Other, updated.Category);
        Assert.Equal(new DateOnly(2026, 8, 15), updated.DueDate);
        Assert.Single(updated.Shares);
        Assert.Equal(150m, updated.Shares.Single().Amount);
        Assert.Equal(addedParticipant.Id, updated.Shares.Single().Person.Id);

        var list = await ReadAsync<IReadOnlyList<DebtResponse>>(await _client.GetAsync("/api/v1/debts"));
        Assert.Contains(list, item => item.Id == debt.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/debts/{debt.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/debts/{debt.Id}")).StatusCode);
    }

    [Fact]
    public async Task DebtUpdate_ProtectsParticipantsWithPaymentHistory()
    {
        await factory.ResetDatabaseAsync();
        var payer = await CreatePersonAsync("Payer", null, true);
        var participant = await CreatePersonAsync("Participant", null, false);
        var replacement = await CreatePersonAsync("Replacement", null, false);
        var debt = await CreateDebtAsync(
            "Protected split",
            100m,
            payer.Id,
            "OTHER",
            [(participant.Id, 100m)]);
        var share = debt.Shares.Single();
        await ReadAsync<PaymentResponse>(await _client.PostAsJsonAsync(
            $"/api/v1/debts/{debt.Id}/shares/{share.Id}/payments",
            new { amount = 25m, paymentDate = "2026-08-01", note = "PIX" }));

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/debts/{debt.Id}", new
        {
            description = debt.Description,
            paidByPersonId = payer.Id,
            category = "OTHER",
            dueDate = (string?)null,
            shares = new[] { new { personId = replacement.Id, amount = 100m } }
        });

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Equal(100m, (await GetDebtAsync(debt.Id)).Shares.Single().Amount);
    }

    [Fact]
    public async Task InvalidSplit_ReturnsValidationProblemDetails()
    {
        await factory.ResetDatabaseAsync();
        var payer = await CreatePersonAsync("Payer", null, true);
        var participant = await CreatePersonAsync("Participant", null, false);

        var response = await _client.PostAsJsonAsync("/api/v1/debts", new
        {
            description = "Invalid split",
            totalAmount = 100m,
            paidByPersonId = payer.Id,
            category = "FOOD",
            dueDate = (string?)null,
            shares = new[] { new { personId = participant.Id, amount = 90m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Validation failed", payload.RootElement.GetProperty("title").GetString());
        Assert.True(payload.RootElement.GetProperty("errors").TryGetProperty("shares", out _));
    }

    [Fact]
    public async Task OpenApi_ContainsCrudAndSimplificationEndpoints()
    {
        await factory.ResetDatabaseAsync();
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"openapi\": \"3.1.1\"", document);
        Assert.Contains("/api/v1/people", document);
        Assert.Contains("/api/v1/debts", document);
        Assert.Contains("/api/v1/debts/settlements/simplified", document);
        Assert.Contains("/api/v1/debts/analysis-context", document);
        Assert.Contains("/payments", document);
        Assert.Contains("/history", document);
    }

    [Fact]
    public async Task DebtAnalysisContext_CalculatesUserPositionAndOverdueCategories()
    {
        await factory.ResetDatabaseAsync();
        var currentUser = await CreatePersonAsync("Me", "me@example.com", true);
        var payer = await CreatePersonAsync("Friend", "friend@example.com", false);
        var createResponse = await _client.PostAsJsonAsync("/api/v1/debts", new
        {
            description = "Private dinner description",
            totalAmount = 125m,
            paidByPersonId = payer.Id,
            groupId = (Guid?)null,
            category = "FOOD",
            dueDate = "2026-01-01",
            shares = new[] { new { personId = currentUser.Id, amount = 125m } }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var context = await ReadAsync<DebtAnalysisContextResponse>(
            await _client.GetAsync("/api/v1/debts/analysis-context"));

        Assert.Equal(125m, context.TotalOwed);
        Assert.Equal(0m, context.TotalToReceive);
        Assert.Equal(1, context.OpenDebtsCount);
        Assert.Equal(1, context.OverdueDebtsCount);
        var category = Assert.Single(context.Categories);
        Assert.Equal("FOOD", category.Category);
        Assert.Equal(125m, category.TotalOwed);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        await factory.ResetDatabaseAsync();
        var correlationId = Guid.Parse("0979386f-e630-4a29-adf3-ea62be3d7ed4").ToString("D");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/debts/unknown");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(correlationId, payload.RootElement.GetProperty("correlationId").GetString());
    }

    private async Task<PersonResponse> CreatePersonAsync(string name, string? email, bool isCurrentUser)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/people", new
        {
            name,
            email,
            isCurrentUser
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<PersonResponse>(response);
    }

    private async Task<DebtResponse> CreateDebtAsync(
        string description,
        decimal totalAmount,
        Guid paidByPersonId,
        string category,
        IReadOnlyList<(Guid PersonId, decimal Amount)> shares,
        Guid? groupId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/debts", new
        {
            description,
            totalAmount,
            paidByPersonId,
            groupId,
            category,
            dueDate = (string?)null,
            shares = shares.Select(share => new
            {
                personId = share.PersonId,
                amount = share.Amount
            })
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<DebtResponse>(response);
    }

    private async Task<DebtResponse> GetDebtAsync(Guid id)
    {
        return await ReadAsync<DebtResponse>(await _client.GetAsync($"/api/v1/debts/{id}"));
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
            ?? throw new InvalidOperationException("Response body was empty.");
    }
}
