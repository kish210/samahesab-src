namespace SamaHesab.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; }
    public string[] Errors { get; }
    public string ErrorMessage => string.Join("; ", Errors);

    protected Result(bool succeeded, params string[] errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static Result Success() => new(true);
    public static Result Failure(params string[] errors) => new(false, errors);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, params string[] errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value);
    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);
    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}
