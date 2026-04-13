namespace backend.Models;

public sealed class ApiResponse<T>
{
    public int Code { get; set; } = 0;
    public string Message { get; set; } = "ok";
    public T? Data { get; set; }
}
