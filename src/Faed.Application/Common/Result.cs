namespace Faed.Application.Common;

/// <summary>
/// The kind of failure an application operation produced. Controllers map these to
/// HTTP outcomes; routine validation does not use exceptions
/// (docs/06-ARCHITECTURE.md §9, docs/19-CODING-CONVENTIONS.md "Exceptions/results").
/// </summary>
public enum ResultErrorKind
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Forbidden = 3,
    Conflict = 4,
}

/// <summary>Outcome of an application operation that returns no value.</summary>
public class Result
{
    protected Result(bool succeeded, ResultErrorKind errorKind, string? error)
    {
        Succeeded = succeeded;
        ErrorKind = errorKind;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public ResultErrorKind ErrorKind { get; }

    public string? Error { get; }

    public static Result Success() => new(true, ResultErrorKind.None, null);

    public static Result Validation(string error) => new(false, ResultErrorKind.Validation, error);

    public static Result NotFound(string error = "The requested item was not found.") =>
        new(false, ResultErrorKind.NotFound, error);

    public static Result Forbidden(string error = "You are not allowed to perform this action.") =>
        new(false, ResultErrorKind.Forbidden, error);

    public static Result Conflict(string error) => new(false, ResultErrorKind.Conflict, error);
}

/// <summary>Outcome of an application operation that returns a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool succeeded, T? value, ResultErrorKind errorKind, string? error)
        : base(succeeded, errorKind, error)
    {
        _value = value;
    }

    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(true, value, ResultErrorKind.None, null);

    public static new Result<T> Validation(string error) => new(false, default, ResultErrorKind.Validation, error);

    public static new Result<T> NotFound(string error = "The requested item was not found.") =>
        new(false, default, ResultErrorKind.NotFound, error);

    public static new Result<T> Forbidden(string error = "You are not allowed to perform this action.") =>
        new(false, default, ResultErrorKind.Forbidden, error);

    public static new Result<T> Conflict(string error) => new(false, default, ResultErrorKind.Conflict, error);

    public static Result<T> From(Result failure) =>
        new(false, default, failure.ErrorKind, failure.Error);
}
