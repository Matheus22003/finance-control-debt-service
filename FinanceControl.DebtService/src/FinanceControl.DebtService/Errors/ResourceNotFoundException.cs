namespace FinanceControl.DebtService.Errors;

public sealed class ResourceNotFoundException(string message) : Exception(message);
