namespace ClipPull.Localization;

internal sealed record LocalizedMessage(string Key, params object[] Arguments)
{
    public string Resolve() => LocalizationService.Get(
        Key,
        Arguments.Select(argument => argument is LocalizedMessage message ? message.Resolve() : argument).ToArray());
}
