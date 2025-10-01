using System.Diagnostics.CodeAnalysis;

namespace Asahi;

public readonly record struct Result<T>
{
    public T? Value { get; init; }
    public string? Error { get; init; }
    
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Value != null && Error == null;
    
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailed => !IsSuccess;

    public static Result<T> Ok(T value) => new Result<T>()
    {
        Value = value,
        Error = null
    };

    public static Result<T> Fail(string error) => new Result<T>()
    {
        Value = default,
        Error = error
    };
}
