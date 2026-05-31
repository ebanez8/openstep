using System.Runtime.InteropServices;

namespace OpenSteps.App;

internal static class NativeMethodsForApp
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();
}
