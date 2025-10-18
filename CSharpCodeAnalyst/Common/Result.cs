namespace CSharpCodeAnalyst.Common;

/// <summary>
/// Without data
/// </summary>
public class Result
{
    private Result(bool isSuccess, bool isCanceled, Exception? error)
    {
        IsSuccess = isSuccess;
        IsCanceled = isCanceled;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsCanceled { get; }

    public bool IsFailure
    {
        get => !IsSuccess && !IsCanceled;
    }

    public Exception? Error { get; }

    public static Result Success()
    {
        return new Result(true, false, null);
    }

    public static Result Failure(Exception error)
    {
        return new Result(false, false, error);
    }

    public static Result Canceled()
    {
        return new Result(false, true, null);
    }
}

public class Result<T>
{

    private Result(bool isSuccess, T? data, Exception? error, bool isCanceled)
    {
        IsCanceled = isCanceled;
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsCanceled { get; }
    public T? Data { get; }
    public Exception? Error { get; }

    public static Result<T> Success(T data)
    {
        return new Result<T>(true, data, null, false);
    }

    public static Result<T> Failure(Exception error)
    {
        return new Result<T>(false, default, error, false);
    }

    public static Result<T> Canceled()
    {
        return new Result<T>(false, default, null, true);
    }
}