namespace StreamNumDeck.Core.Compatibility;

internal static class Guard
{
    public static T NotNull<T>(T? value, string parameterName) where T : class =>
        value ?? throw new ArgumentNullException(parameterName);

    public static void NotDisposed(bool disposed, object instance)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(instance.GetType().FullName);
        }
    }
}
