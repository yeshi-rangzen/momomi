namespace MomomiAPI.Helpers
{
    public static class FireAndForgetHelper
    {
        /// <summary>
        /// Runs the task in a fire-and-forget manner and logs any exceptions.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="logger">Logger instance for capturing exceptions.</param>
        /// <param name="contextMessage">Optional context message for logging.</param>
        public static void Run(Task task, ILogger logger, string? contextMessage = null)
        {
            if (task == null) return;

            _ = task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    logger.LogWarning(t.Exception,
                        "Fire-and-forget task failed. {Context}",
                        contextMessage ?? string.Empty);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Runs an async function in a fire-and-forget manner and logs any exceptions.
        /// Useful when you want to inline async lambdas.
        /// </summary>
        public static void Run(Func<Task> taskFunc, ILogger logger, string? contextMessage = null)
        {
            if (taskFunc == null) return;

            _ = Task.Run(taskFunc).ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    logger.LogWarning(t.Exception,
                        "Fire-and-forget task failed. {Context}",
                        contextMessage ?? string.Empty);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
