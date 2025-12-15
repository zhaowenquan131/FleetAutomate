namespace FleetAutomate.Model
{
    /// <summary>
    /// Interface for actions that support retry logic.
    /// Actions implementing this interface will automatically retry on failure with a configurable delay.
    /// </summary>
    public interface IRetryableAction : IAction
    {
        /// <summary>
        /// Number of times to retry the action if it fails (0 means no retry, just one attempt).
        /// For example, RetryTimes = 3 means 4 total attempts (1 initial + 3 retries).
        /// </summary>
        int RetryTimes { get; set; }

        /// <summary>
        /// Delay in milliseconds between retry attempts.
        /// This provides a pause before retrying to allow conditions to stabilize.
        /// </summary>
        int RetryDelayMilliseconds { get; set; }
    }
}
