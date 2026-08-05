using System.Net.Mail;
using FinanceControl.DebtService.Contracts.Debts;
using FinanceControl.DebtService.Contracts.People;
using FinanceControl.DebtService.Errors;

namespace FinanceControl.DebtService.Services;

internal static class RequestValidator
{
    public static void Validate(PersonRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRequiredText(request.Name, 120, "name", errors);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (request.Email.Trim().Length > 254 || !MailAddress.TryCreate(request.Email.Trim(), out _))
            {
                errors["email"] = ["Email must be a valid address with at most 254 characters."];
            }
        }

        ThrowIfAny(errors);
    }

    public static void Validate(CreateDebtRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRequiredText(request.Description, 200, "description", errors);
        ValidateMoney(request.TotalAmount, "totalAmount", errors);

        if (request.PaidByPersonId == Guid.Empty)
        {
            errors["paidByPersonId"] = ["PaidByPersonId is required."];
        }

        if (request.Shares is null || request.Shares.Count == 0)
        {
            errors["shares"] = ["At least one share is required."];
        }
        else
        {
            if (request.Shares.Any(share => share.PersonId == Guid.Empty))
            {
                errors["shares.personId"] = ["Every share must reference a person."];
            }

            if (request.Shares.GroupBy(share => share.PersonId).Any(group => group.Count() > 1))
            {
                errors["shares"] = ["A person can appear only once in the split."];
            }

            if (request.Shares.Any(share => !IsValidMoney(share.Amount)))
            {
                errors["shares.amount"] = ["Every share amount must be positive with at most two decimal places."];
            }

            if (request.Shares.Sum(share => share.Amount) != request.TotalAmount)
            {
                errors["shares"] = ["The sum of all shares must equal TotalAmount."];
            }
        }

        ThrowIfAny(errors);
    }

    public static void Validate(UpdateDebtRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateRequiredText(request.Description, 200, "description", errors);

        if (request.PaidByPersonId == Guid.Empty)
        {
            errors["paidByPersonId"] = ["PaidByPersonId is required."];
        }

        ValidateShares(request.Shares, errors);
        ThrowIfAny(errors);
    }

    public static void Validate(PaymentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateMoney(request.Amount, "amount", errors);

        if (request.PaymentDate == default)
        {
            errors["paymentDate"] = ["PaymentDate is required."];
        }

        if (request.Note?.Trim().Length > 500)
        {
            errors["note"] = ["Note must contain at most 500 characters."];
        }

        ThrowIfAny(errors);
    }

    public static void Validate(RecordSettlementTransferRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.FromPersonId == Guid.Empty)
        {
            errors["fromPersonId"] = ["FromPersonId is required."];
        }

        if (request.ToPersonId == Guid.Empty)
        {
            errors["toPersonId"] = ["ToPersonId is required."];
        }

        if (request.FromPersonId == request.ToPersonId)
        {
            errors["toPersonId"] = ["The payer and recipient must be different people."];
        }

        ValidateMoney(request.Amount, "amount", errors);
        if (request.PaymentDate == default)
        {
            errors["paymentDate"] = ["PaymentDate is required."];
        }

        if (request.Note?.Trim().Length > 500)
        {
            errors["note"] = ["Note must contain at most 500 characters."];
        }

        ThrowIfAny(errors);
    }

    private static void ValidateRequiredText(
        string? value,
        int maximumLength,
        string field,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors[field] = [$"{field} must contain at most {maximumLength} characters."];
        }
    }

    private static void ValidateMoney(
        decimal value,
        string field,
        IDictionary<string, string[]> errors)
    {
        if (!IsValidMoney(value))
        {
            errors[field] = [$"{field} must be positive with at most two decimal places."];
        }
    }

    private static void ValidateShares(
        IReadOnlyList<DebtShareRequest>? shares,
        IDictionary<string, string[]> errors)
    {
        if (shares is null || shares.Count == 0)
        {
            errors["shares"] = ["At least one share is required."];
            return;
        }

        if (shares.Any(share => share.PersonId == Guid.Empty))
        {
            errors["shares.personId"] = ["Every share must reference a person."];
        }

        if (shares.GroupBy(share => share.PersonId).Any(group => group.Count() > 1))
        {
            errors["shares"] = ["A person can appear only once in the split."];
        }

        if (shares.Any(share => !IsValidMoney(share.Amount)))
        {
            errors["shares.amount"] = ["Every share amount must be positive with at most two decimal places."];
        }
    }

    private static bool IsValidMoney(decimal value)
    {
        return value > 0m && decimal.Round(value, 2) == value;
    }

    private static void ThrowIfAny(IReadOnlyDictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }
}
