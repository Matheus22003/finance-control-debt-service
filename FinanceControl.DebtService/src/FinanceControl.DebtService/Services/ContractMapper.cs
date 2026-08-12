using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Contracts.People;
using FinanceControl.DebtService.Domain;

namespace FinanceControl.DebtService.Services;

internal static class ContractMapper
{
    public static PersonResponse ToResponse(this Person person, Guid currentUserId)
    {
        return new PersonResponse(
            person.Id,
            person.Name,
            person.Email,
            person.LinkedUserId == currentUserId,
            person.CreatedAt,
            person.UpdatedAt);
    }

    public static PersonReferenceResponse ToReference(this Person person, Guid currentUserId)
    {
        return new PersonReferenceResponse(person.Id, person.Name, person.LinkedUserId == currentUserId);
    }

    public static DebtResponse ToResponse(this Debt debt, Guid currentUserId)
    {
        return new DebtResponse(
            debt.Id,
            debt.Description,
            debt.TotalAmount,
            debt.PaidByPerson.ToReference(currentUserId),
            debt.GroupId,
            debt.Category,
            debt.Status,
            debt.DueDate,
            debt.CreatedByUserId == currentUserId,
            debt.CreatedAt,
            debt.UpdatedAt,
            debt.Shares
                .OrderBy(share => share.Person.Name)
                .ThenBy(share => share.PersonId)
                .Select(share => new DebtShareResponse(
                    share.Id,
                    share.Person.ToReference(currentUserId),
                    share.Amount,
                    share.PaidAmount,
                    share.RemainingAmount,
                    share.PersonId == debt.PaidByPersonId))
                .ToList());
    }

    public static PaymentResponse ToResponse(this Payment payment, Debt debt, Guid currentUserId)
    {
        return new PaymentResponse(
            payment.Id,
            debt.Id,
            payment.DebtShareId,
            payment.DebtShare.Person.ToReference(currentUserId),
            debt.PaidByPerson.ToReference(currentUserId),
            payment.Amount,
            payment.PaymentDate,
            payment.Note,
            payment.RecordedByUserId,
            payment.ConfirmationRequiredFromUserId,
            payment.Status,
            payment.ConfirmedAt,
            payment.RejectedAt,
            payment.Status == PaymentStatus.Pending &&
                payment.ConfirmationRequiredFromUserId == currentUserId,
            payment.Status == PaymentStatus.Pending &&
                payment.ConfirmationRequiredFromUserId == currentUserId,
            payment.Status == PaymentStatus.Pending && payment.RecordedByUserId == currentUserId,
            payment.RecordedByUserId == currentUserId,
            payment.CreatedAt,
            payment.UpdatedAt);
    }

    public static DebtHistoryResponse ToResponse(this DebtHistory history)
    {
        return new DebtHistoryResponse(
            history.Id,
            history.Type,
            history.Description,
            history.OccurredAt);
    }
}
