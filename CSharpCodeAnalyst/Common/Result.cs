namespace CSharpCodeAnalyst.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public Exception? Error { get; }

    private Result(bool isSuccess, T? data, Exception? error)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
    }

    public static Result<T> Success(T data) => new(true, data, null);
    public static Result<T> Failure(Exception error) => new(false, default, error);
    
    public static Result<T> Canceled() => new(false, default, null);
}