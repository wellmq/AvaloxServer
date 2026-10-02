// Standard server response payload
public class Response
{
    public bool IsSuccessful { get; set; }
    public string Message { get; set; } = "";
    public object? Obj { get; set; }
}
