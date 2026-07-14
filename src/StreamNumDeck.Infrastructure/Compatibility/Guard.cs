namespace StreamNumDeck.Infrastructure.Compatibility;

internal static class Guard
{
    public static T NotNull<T>(T? value, string parameterName) where T : class =>
        value ?? throw new ArgumentNullException(parameterName);

    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value!;
    }

    public static void NotDisposed(bool disposed, object instance)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(instance.GetType().FullName);
        }
    }
}
