using FinanceControl.DebtService.Contracts.People;
using FinanceControl.DebtService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.DebtService.Endpoints;

public static class PersonEndpoints
{
    public static RouteGroupBuilder MapPersonEndpoints(this RouteGroupBuilder group)
    {
        var people = group.MapGroup("/people").WithTags("People");

        people.MapGet("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                PersonService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindAllAsync(userId, cancellationToken)))
            .WithName("GetPeople")
            .Produces<IReadOnlyList<PersonResponse>>();

        people.MapGet("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                PersonService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindByIdAsync(userId, id, cancellationToken)))
            .WithName("GetPersonById")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        people.MapPost("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                PersonRequest request,
                PersonService service,
                CancellationToken cancellationToken) =>
            {
                var person = await service.CreateAsync(userId, request, cancellationToken);
                return Results.Created($"/api/v1/people/{person.Id}", person);
            })
            .WithName("CreatePerson")
            .Produces<PersonResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        people.MapPut("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                PersonRequest request,
                PersonService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateAsync(userId, id, request, cancellationToken)))
            .WithName("UpdatePerson")
            .Produces<PersonResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        people.MapDelete("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                PersonService service,
                CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(userId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeletePerson")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
