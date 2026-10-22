namespace GrocerEase.Contracts.Errors;

public record ErrorResponse(
    string Code,
    int Status,
    string Title,
    string Detail,
    string TraceId);

public record ValidationErrorResponse(
    string Code,
    int Status,
    string Title,
    IReadOnlyDictionary<string, string[]> Errors,
    string TraceId);
