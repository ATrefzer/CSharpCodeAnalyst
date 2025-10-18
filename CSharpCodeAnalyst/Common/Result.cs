namespace CSharpCodeAnalyst.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsCanceled { get; }
    public T? Data { get; }
    public Exception? Error { get; }

    private Result(bool isSuccess, T? data, Exception? error, bool isCanceled)
    {
        IsCanceled = isCanceled;
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
    }

    public static Result<T> Success(T data) => new(true, data, null, false);
    public static Result<T> Failure(Exception error) => new(false, default, error, false);
    
    public static Result<T> Canceled() => new(false, default, null, true);
}