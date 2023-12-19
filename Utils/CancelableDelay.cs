using System;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.IO;

namespace VBoxCleaner.Utils
{
    internal static class CancelableDelay
    {
        public static async Task Delay(int millisecondsDelay, CancellationToken token)
        {
            try
            {
                if (!token.IsCancellationRequested)
                    await Task.Delay(millisecondsDelay, token);
            }
            catch (TaskCanceledException)
            {
                // Do nothing
            }
            catch (Exception other)
            {
                Logger.WriteLine($"CancelableDelay exception: {other}");
            }
        }

        /// <summary>
        /// Performs cancellable Task.Delay where task is cancelling only once (when cancellation is requested during 'await Task.Delay')
        /// </summary>
        /// <param name="millisecondsDelay"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task DelayOnceCancel(int millisecondsDelay, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                    await Task.Delay(millisecondsDelay, CancellationToken.None);
                else
                    await Task.Delay(millisecondsDelay, token);
            }
            catch (TaskCanceledException)
            {
                // Do nothing
            }
            catch (Exception other)
            {
                Logger.WriteLine($"CancelableDelay exception: {other}");
            }
        }

        /// <summary>
        /// Performs cancellable Task.Delay where task is cancelling only once (when cancellation is requested during 'await Task.Delay')
        /// </summary>
        /// <param name="millisecondsDelay">Cancellable Delay before cancellation request</param>
        /// <param name="afterCancelDelay">Regular delay after cancellation was requested</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task Delay(int millisecondsDelay, int afterCancelDelay, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                    await Task.Delay(afterCancelDelay, CancellationToken.None);
                else
                    await Task.Delay(millisecondsDelay, token);
            }
            catch (TaskCanceledException)
            {
                // Do nothing
            }
            catch (Exception other)
            {
                Logger.WriteLine($"CancelableDelay exception: {other}");
            }
        }
    }
}
