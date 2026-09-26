namespace InfiniteCanvasDesktop;

public sealed record ApiConfiguration(string BaseUrl, string ApiKey, string Model)
{
    public static ApiConfiguration Default { get; } = new("https://api.openai.com", "", "gpt-image-2");
}
