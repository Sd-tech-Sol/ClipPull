namespace ClipPull.Services;

internal sealed class RequestedFormatUnavailableException(
    string message,
    IReadOnlyList<string> partialFiles) : Exception(message)
{
    public IReadOnlyList<string> PartialFiles { get; } = partialFiles;
}
