#if NETFRAMEWORK
namespace System.Threading.Tasks;

internal static class TaskCompatibilityExtensions
{
    public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var cancellationTask = new TaskCompletionSource<object?>();
        using (cancellationToken.Register(
                   static state => ((TaskCompletionSource<object?>)state!).TrySetCanceled(),
                   cancellationTask))
        {
            var completed = await Task.WhenAny(task, cancellationTask.Task).ConfigureAwait(false);
            await completed.ConfigureAwait(false);
        }
    }
}
#endif
