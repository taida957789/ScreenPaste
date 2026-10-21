using System.Runtime.ExceptionServices;

namespace ScreenPaste.Tests;

/// <summary>
/// Runs a test body on a single-threaded-apartment thread, which WPF elements (shapes, text
/// boxes, images) require. Failures are re-thrown on the test thread with their stack trace.
/// </summary>
internal static class StaRunner
{
    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
