using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WorkLog.Launcher;

internal static class WindowsChildProcess
{
    // Atomic job enrollment at CreateProcess closes the parent-death race of create/assign/resume.
    // Windows 10+ supports PROC_THREAD_ATTRIBUTE_JOB_LIST, including nested client jobs.
    public static int Run(string executable, string[] arguments)
    {
        using var job = CreateJobObjectW(0, null);
        if (job.IsInvalid)
            throw Error("create child lifetime job");
        var limits = new JobLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<JobLimits>()))
            throw Error("set child lifetime limits");

        var handles = new List<SafeFileHandle>();
        nint attributes = 0, handleBuffer = 0, jobBuffer = 0;
        var initialized = false;
        try
        {
            foreach (var standard in new[] { -10, -11, -12 })
            {
                var source = GetStdHandle(standard);
                if (source == 0 || source == -1)
                    throw new IOException("The launcher requires valid standard input/output/error handles.");
                if (!DuplicateHandle(-1, source, -1, out var duplicate, 0, true, 2))
                    throw Error("inherit standard stream");
                handles.Add(new SafeFileHandle(duplicate, ownsHandle: true));
            }
            nuint bytes = 0;
            _ = InitializeProcThreadAttributeList(0, 2, 0, ref bytes);
            attributes = Marshal.AllocHGlobal(checked((nint)bytes));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref bytes))
                throw Error("initialize process attributes");
            initialized = true;
            handleBuffer = Marshal.AllocHGlobal(3 * nint.Size);
            for (var index = 0; index < handles.Count; index++)
                Marshal.WriteIntPtr(handleBuffer, index * nint.Size, handles[index].DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, 0x20002, handleBuffer, (nuint)(3 * nint.Size), 0, 0))
                throw Error("restrict inherited handles");
            jobBuffer = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(jobBuffer, job.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, 0x2000D, jobBuffer, (nuint)nint.Size, 0, 0))
                throw Error("enroll child lifetime job");
            var startup = new StartupInfoEx
            {
                Info = new StartupInfo
                {
                    Size = (uint)Marshal.SizeOf<StartupInfoEx>(),
                    Flags = 0x100,
                    Input = handles[0].DangerousGetHandle(),
                    Output = handles[1].DangerousGetHandle(),
                    Error = handles[2].DangerousGetHandle()
                },
                Attributes = attributes
            };
            var command = new StringBuilder(string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote)));
            if (command.Length >= 32767)
                throw new ArgumentException("Forwarded command line is too long.");
            var flags = 0x00080000u | (GetConsoleWindow() == 0 ? 0x08000000u : 0u);
            if (!CreateProcessW(executable, command, 0, 0, true, flags, 0, Environment.CurrentDirectory, ref startup, out var created))
                throw Error("start selected payload");
            using var process = new SafeFileHandle(created.Process, true);
            using var thread = new SafeFileHandle(created.Thread, true);
            foreach (var handle in handles)
                handle.Dispose();
            // Keep the launcher alive on Ctrl+C while the console event reaches the child normally.
            ConsoleCancelEventHandler cancel = (_, eventArgs) => eventArgs.Cancel = true;
            Console.CancelKeyPress += cancel;
            try
            {
                if (WaitForSingleObject(process, uint.MaxValue) != 0 || !GetExitCodeProcess(process, out var exitCode))
                    throw Error("wait for selected payload");
                return unchecked((int)exitCode);
            }
            finally { Console.CancelKeyPress -= cancel; }
        }
        finally
        {
            if (initialized)
                DeleteProcThreadAttributeList(attributes);
            if (attributes != 0)
                Marshal.FreeHGlobal(attributes);
            if (handleBuffer != 0)
                Marshal.FreeHGlobal(handleBuffer);
            if (jobBuffer != 0)
                Marshal.FreeHGlobal(jobBuffer);
            foreach (var handle in handles)
                handle.Dispose();
            GC.KeepAlive(job);
        }
    }

    private static string Quote(string value)
    {
        if (value.Contains('\0'))
            throw new ArgumentException("Arguments cannot contain NUL.");
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private static Win32Exception Error(string action) => new(Marshal.GetLastWin32Error(), "Could not " + action);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public nuint MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public uint Size;
        public nint Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize;
        public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo Info; public nint Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public nint Process, Thread; public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateJobObjectW(nint security, string? name);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref JobLimits info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GetStdHandle(int handle);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DuplicateHandle(nint sourceProcess, nint source, nint targetProcess, out nint target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute, nint value, nuint size, nint previous, nint returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(nint list);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcessW(string application, StringBuilder command, nint processSecurity, nint threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exitCode);
    [DllImport("kernel32.dll")] private static extern nint GetConsoleWindow();
}
