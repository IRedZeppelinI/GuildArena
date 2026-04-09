using System;

namespace GuildArena.Shared.Results;

/// <summary>
/// Represents the outcome of an operation that returns a value on success.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the value of the result. 
    /// Throws an exception if accessed on a failure result.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");

    /// <summary>
    /// Allows implicit conversion from a value to a successful result.
    /// This removes boilerplate from Handlers (e.g., 'return myDto;' instead of 'return Result.Success(myDto);').
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
}