namespace ClipPull.Localization;

internal sealed class LocalizedException(string key, params object[] arguments) : Exception
{
    public string ResourceKey { get; } = key;

    public object[] Arguments { get; } = arguments;

    public override string Message => LocalizationService.Get(ResourceKey, Arguments);
}
