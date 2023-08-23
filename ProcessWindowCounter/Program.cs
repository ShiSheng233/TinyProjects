using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessWindowCounter;

class ProcessWindowCounter
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    static void Main()
    {
        if (Process.GetProcesses().Length == 0)
        {
            Console.WriteLine("No processes found.");
            return;
        }

        Dictionary<string, int> processWindowCounts = new Dictionary<string, int>();

        EnumWindows((hWnd, lParam) =>
        {
            int windowProcessId;
            GetWindowThreadProcessId(hWnd, out windowProcessId);
            Process process = null;

            try
            {
                process = Process.GetProcessById(windowProcessId);
            }
            catch (ArgumentException)
            {
                // Process not found, continue with the next window
                return true;
            }

            if (!processWindowCounts.ContainsKey(process.ProcessName))
            {
                processWindowCounts[process.ProcessName] = 1;
            }
            else
            {
                processWindowCounts[process.ProcessName]++;
            }

            return true;
        }, IntPtr.Zero);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Processes with windows:");
        Console.ResetColor();
        Console.WriteLine();
        foreach (var processWindowCount in processWindowCounts)
        {
            Console.WriteLine($"{processWindowCount.Key} - {processWindowCount.Value} windows");
        }
    }
}