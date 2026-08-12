using FinanceControl.DebtService.Contracts.Social;
using FinanceControl.DebtService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.DebtService.Endpoints;

public static class SocialEndpoints
{
    public static RouteGroupBuilder MapSocialEndpoints(this RouteGroupBuilder group)
    {
        MapFriendEndpoints(group);
        MapGroupEndpoints(group);
        return group;
    }

    private static void MapFriendEndpoints(RouteGroupBuilder group)
    {
        var friends = group.MapGroup("/friends").WithTags("Friends");

        friends.MapGet("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetFriendsAsync(userId, cancellationToken)))
            .Produces<IReadOnlyList<FriendResponse>>();

        friends.MapGet("/requests/incoming", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetIncomingRequestsAsync(userId, cancellationToken)))
            .Produces<IReadOnlyList<FriendshipResponse>>();

        friends.MapGet("/requests/outgoing", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetOutgoingRequestsAsync(userId, cancellationToken)))
            .Produces<IReadOnlyList<FriendshipResponse>>();

        friends.MapPost("/requests", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                CreateFriendRequest request,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
            {
                var friendship = await service.CreateRequestAsync(userId, request, cancellationToken);
                return Results.Created($"/api/v1/friends/requests/{friendship.Id}", friendship);
            })
            .Produces<FriendshipResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        friends.MapPost("/requests/{friendshipId:guid}/accept", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid friendshipId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.AcceptAsync(userId, friendshipId, cancellationToken)))
            .Produces<FriendshipResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        friends.MapPost("/requests/{friendshipId:guid}/reject", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid friendshipId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.RejectAsync(userId, friendshipId, cancellationToken)))
            .Produces<FriendshipResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        friends.MapDelete("/{friendUserId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid friendUserId,
                SocialConnectionService service,
                CancellationToken cancellationToken) =>
            {
                await service.RemoveFriendAsync(userId, friendUserId, cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapGroupEndpoints(RouteGroupBuilder group)
    {
        var groups = group.MapGroup("/groups").WithTags("Groups");

        groups.MapGet("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                GroupService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAllAsync(userId, cancellationToken)))
            .Produces<IReadOnlyList<GroupResponse>>();

        groups.MapGet("/{groupId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid groupId,
                GroupService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetByIdAsync(userId, groupId, cancellationToken)))
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapPost("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                CreateGroupRequest request,
                GroupService service,
                CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(userId, request, cancellationToken);
                return Results.Created($"/api/v1/groups/{created.Id}", created);
            })
            .Produces<GroupResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        groups.MapPut("/{groupId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid groupId,
                UpdateGroupRequest request,
                GroupService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateAsync(userId, groupId, request, cancellationToken)))
            .Produces<GroupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapPost("/{groupId:guid}/members", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid groupId,
                AddGroupMemberRequest request,
                GroupService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.AddMemberAsync(userId, groupId, request, cancellationToken)))
            .Produces<GroupResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapDelete("/{groupId:guid}/members/{memberUserId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid groupId,
                Guid memberUserId,
                GroupService service,
                CancellationToken cancellationToken) =>
            {
                await service.RemoveMemberAsync(userId, groupId, memberUserId, cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        groups.MapDelete("/{groupId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid groupId,
                GroupService service,
                CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(userId, groupId, cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
