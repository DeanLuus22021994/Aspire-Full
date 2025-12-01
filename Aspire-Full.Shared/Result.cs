namespace Aspire_Full.Shared;

public class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error == null;

    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(string error)
    {
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}

public class Result
{
    public string? Error { get; }
    public bool IsSuccess => Error == null;

    private Result(string? error)
    {
        Error = error;
    }

    public static Result Success() => new(null);
    public static Result Failure(string error) => new(error);
}
