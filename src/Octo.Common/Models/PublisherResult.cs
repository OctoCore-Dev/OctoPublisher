namespace Octo.Common.Models;

public readonly record struct PublisherResult(
    bool IsSuccess,
    int ExitCode,
    string? Message = null,
    string? OperationId = null)
{
    public static PublisherResult Success(string? message = null, string? opId = null) =>
        new(true, 0, message, opId);

    public static PublisherResult Failed(int exitCode, string message, string? opId = null) =>
        new(false, exitCode != 0 ? exitCode : 1, message, opId);
}
