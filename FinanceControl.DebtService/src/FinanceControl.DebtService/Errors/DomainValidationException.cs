namespace FinanceControl.DebtService.Errors;

public sealed class DomainValidationException(
    IReadOnlyDictionary<string, string[]> errors) : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static DomainValidationException For(string field, string message)
    {
        return new DomainValidationException(new Dictionary<string, string[]>
        {
            [field] = [message]
        });
    }
}
