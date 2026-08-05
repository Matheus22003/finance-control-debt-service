using FinanceControl.DebtService.Contracts.People;
using FinanceControl.DebtService.Domain;
using FinanceControl.DebtService.Errors;
using FinanceControl.DebtService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Services;

public sealed class PersonService(DebtDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<PersonResponse>> FindAllAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var people = await dbContext.People
            .AsNoTracking()
            .Where(person => person.OwnerUserId == userId)
            .OrderBy(person => person.Name)
            .ThenBy(person => person.Id)
            .ToListAsync(cancellationToken);

        return people.Select(person => person.ToResponse(userId)).ToList();
    }

    public async Task<PersonResponse> FindByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return (await FindEntityAsync(userId, id, cancellationToken)).ToResponse(userId);
    }

    public async Task<PersonResponse> CreateAsync(
        Guid userId,
        PersonRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var now = timeProvider.GetUtcNow();
        await UnsetCurrentUserAsync(userId, request.IsCurrentUser, null, now, cancellationToken);
        var person = new Person(
            userId,
            request.IsCurrentUser ? userId : null,
            request.Name.Trim(),
            NormalizeEmail(request.Email),
            request.IsCurrentUser,
            now);
        dbContext.People.Add(person);
        await dbContext.SaveChangesAsync(cancellationToken);
        return person.ToResponse(userId);
    }

    public async Task<PersonResponse> UpdateAsync(
        Guid userId,
        Guid id,
        PersonRequest request,
        CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var person = await FindOwnedEntityAsync(userId, id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await UnsetCurrentUserAsync(userId, request.IsCurrentUser, id, now, cancellationToken);
        person.Update(
            request.Name.Trim(),
            NormalizeEmail(request.Email),
            request.IsCurrentUser,
            request.IsCurrentUser ? userId : person.LinkedUserId,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return person.ToResponse(userId);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        dbContext.People.Remove(await FindOwnedEntityAsync(userId, id, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Person> FindEntityAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.People.SingleOrDefaultAsync(
            person => person.Id == id && person.OwnerUserId == userId,
            cancellationToken)
            ?? throw new ResourceNotFoundException($"Person with id {id} was not found.");
    }

    private async Task<Person> FindOwnedEntityAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.People.SingleOrDefaultAsync(
            person => person.Id == id && person.OwnerUserId == userId,
            cancellationToken)
            ?? throw new ResourceNotFoundException($"Person with id {id} was not found.");
    }

    private async Task UnsetCurrentUserAsync(
        Guid userId,
        bool setCurrentUser,
        Guid? exceptId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!setCurrentUser)
        {
            return;
        }

        var currentPeople = await dbContext.People
            .Where(person => person.OwnerUserId == userId && person.IsCurrentUser && person.Id != exceptId)
            .ToListAsync(cancellationToken);
        foreach (var currentPerson in currentPeople)
        {
            currentPerson.SetCurrentUser(false, now);
        }
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
