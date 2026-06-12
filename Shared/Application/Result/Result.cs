namespace Shared.Application.Result
{
    public interface IResult
    {
        bool IsSuccess { get; }
        string? ErrorMessage { get; }
    }

    public class Result : IResult
    {
        public bool IsSuccess { get; set; }

        public string? ErrorMessage {  set; get; }
    }

    public class Result<T> : Result
    {
        public T? Value { get; set; }
    }

}
