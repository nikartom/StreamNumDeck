#if NETFRAMEWORK
namespace System.Threading.Tasks;

internal static class TaskCompatibilityExtensions
{
    public static Task WaitAsync(this Task task, CancellationToken cancellationToken) =>
        WaitCoreAsync(task, Timeout.InfiniteTimeSpan, cancellationToken);

    public static async Task<T> WaitAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await WaitCoreAsync(task, timeout, cancellationToken).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    private static async Task WaitCoreAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        using (var timeoutCancellation = timeout == Timeout.InfiniteTimeSpan
                   ? new CancellationTokenSource()
                   : new CancellationTokenSource(timeout))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                   cancellationToken,
                   timeoutCancellation.Token))
        {
            var signal = new TaskCompletionSource<object?>();
            using (linked.Token.Register(
                       static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
                       signal))
            {
                if (await Task.WhenAny(task, signal.Task).ConfigureAwait(false) != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }
            }
        }

        await task.ConfigureAwait(false);
    }
}
#endif
