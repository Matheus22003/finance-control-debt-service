using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceControl.DebtService.Endpoints;

public static class DebtEndpoints
{
    public static RouteGroupBuilder MapDebtEndpoints(this RouteGroupBuilder group)
    {
        var debts = group.MapGroup("/debts").WithTags("Debts");

        debts.MapGet("/summary", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                DebtSummaryService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetSummaryAsync(userId, cancellationToken)))
            .WithName("GetDebtSummary")
            .Produces<DebtSummaryResponse>();

        debts.MapGet("/analysis-context", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                DebtSummaryService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAnalysisContextAsync(userId, cancellationToken)))
            .WithName("GetDebtAnalysisContext")
            .Produces<DebtAnalysisContextResponse>();

        debts.MapGet("/settlements/simplified", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                [FromQuery] Guid? groupId,
                SettlementSimplificationService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.CalculateAsync(userId, groupId, cancellationToken)))
            .WithName("GetSimplifiedSettlements")
            .Produces<SimplifiedSettlementResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapGet("/settlements/simplified/transfers", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                [FromQuery] Guid? groupId,
                SettlementTransferService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindActiveAsync(userId, groupId, cancellationToken)))
            .WithName("GetActiveSettlementTransfers")
            .Produces<IReadOnlyList<SettlementTransferResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapGet("/settlements/simplified/transfers/pending-confirmation", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                SettlementTransferService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindPendingConfirmationsAsync(userId, cancellationToken)))
            .WithName("GetPendingSettlementTransferConfirmations")
            .Produces<IReadOnlyList<SettlementTransferResponse>>();

        debts.MapPost("/settlements/simplified/transfers", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                RecordSettlementTransferRequest request,
                SettlementTransferService service,
                CancellationToken cancellationToken) =>
            {
                var transfer = await service.RecordAsync(userId, request, cancellationToken);
                return Results.Created(
                    $"/api/v1/debts/settlements/simplified/transfers/{transfer.Id}",
                    transfer);
            })
            .WithName("RecordSettlementTransfer")
            .Produces<SettlementTransferResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        debts.MapPost("/settlements/simplified/transfers/{transferId:guid}/confirm", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid transferId,
                SettlementTransferService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.ConfirmAsync(userId, transferId, cancellationToken)))
            .WithName("ConfirmSettlementTransfer")
            .Produces<SettlementTransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        debts.MapPost("/settlements/simplified/transfers/{transferId:guid}/reject", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid transferId,
                SettlementTransferService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.RejectAsync(userId, transferId, cancellationToken)))
            .WithName("RejectSettlementTransfer")
            .Produces<SettlementTransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        debts.MapGet("/payments/pending-confirmation", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindPendingConfirmationsAsync(userId, cancellationToken)))
            .WithName("GetPendingPaymentConfirmations")
            .Produces<IReadOnlyList<PaymentResponse>>();

        debts.MapGet("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindAllAsync(userId, cancellationToken)))
            .WithName("GetDebts")
            .Produces<IReadOnlyList<DebtResponse>>();

        debts.MapGet("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindByIdAsync(userId, id, cancellationToken)))
            .WithName("GetDebtById")
            .Produces<DebtResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapPost("/", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                CreateDebtRequest request,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
            {
                var debt = await service.CreateAsync(userId, request, cancellationToken);
                return Results.Created($"/api/v1/debts/{debt.Id}", debt);
            })
            .WithName("CreateDebt")
            .Produces<DebtResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        debts.MapPut("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                UpdateDebtRequest request,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateAsync(userId, id, request, cancellationToken)))
            .WithName("UpdateDebt")
            .Produces<DebtResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapDelete("/{id:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid id,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(userId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteDebt")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapGet("/{debtId:guid}/payments", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindPaymentsAsync(userId, debtId, cancellationToken)))
            .WithName("GetDebtPayments")
            .Produces<IReadOnlyList<PaymentResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapPost("/{debtId:guid}/shares/{shareId:guid}/payments", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                Guid shareId,
                PaymentRequest request,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
            {
                var payment = await service.AddPaymentAsync(
                    userId,
                    debtId,
                    shareId,
                    request,
                    cancellationToken);
                return Results.Created($"/api/v1/debts/{debtId}/payments/{payment.Id}", payment);
            })
            .WithName("CreateDebtPayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapPut("/{debtId:guid}/payments/{paymentId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                Guid paymentId,
                PaymentRequest request,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdatePaymentAsync(
                    userId,
                    debtId,
                    paymentId,
                    request,
                    cancellationToken)))
            .WithName("UpdateDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapDelete("/{debtId:guid}/payments/{paymentId:guid}", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                Guid paymentId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
            {
                await service.DeletePaymentAsync(userId, debtId, paymentId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteDebtPayment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        debts.MapPost("/{debtId:guid}/payments/{paymentId:guid}/confirm", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                Guid paymentId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.ConfirmPaymentAsync(
                    userId,
                    debtId,
                    paymentId,
                    cancellationToken)))
            .WithName("ConfirmDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        debts.MapPost("/{debtId:guid}/payments/{paymentId:guid}/reject", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                Guid paymentId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.RejectPaymentAsync(
                    userId,
                    debtId,
                    paymentId,
                    cancellationToken)))
            .WithName("RejectDebtPayment")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        debts.MapGet("/{debtId:guid}/history", async (
                [FromHeader(Name = InternalRequestHeaders.UserId)] Guid userId,
                Guid debtId,
                DebtManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.FindHistoryAsync(userId, debtId, cancellationToken)))
            .WithName("GetDebtHistory")
            .Produces<IReadOnlyList<DebtHistoryResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
