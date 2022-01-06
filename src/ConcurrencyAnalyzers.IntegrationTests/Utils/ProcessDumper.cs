using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ConcurrencyAnalyzers.IntegrationTests;

// TODO: use generated win api calls (when source generators were used to create an API).
public class ProcessDumper
{
    /// <summary>
    /// Protects calling <see cref="DumpProcess"/> since all Windows DbgHelp functions are single threaded.
    /// </summary>
    private static readonly object DumpProcessLock = new object();

    public static void DumpProcess(IntPtr processHandle, int processId, string dumpPath, bool compress = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);

        File.Delete(dumpPath);
        var uncompressedDumpPath = dumpPath;

        if (compress)
        {
            uncompressedDumpPath = dumpPath + ".dmp.tmp";
            File.Delete(uncompressedDumpPath);
        }

        using (FileStream fs = new FileStream(uncompressedDumpPath, FileMode.Create))
        {
            lock (DumpProcessLock)
            {
                bool dumpSuccess = MiniDumpWriteDump(
                    hProcess: processHandle,
                    processId: (uint)processId,
                    hFile: fs.SafeFileHandle,
                    dumpType: (uint)MiniDumpType.MiniDumpWithFullMemory,
                    expParam: IntPtr.Zero,
                    userStreamParam: IntPtr.Zero,
                    callbackParam: IntPtr.Zero);

                if (!dumpSuccess)
                {
                    var code = Marshal.GetLastWin32Error();
                    var message = new Win32Exception(code).Message;

                    throw new Exception($"Failed to create process dump. Native error: ({code:x}) {message}, dump-path={dumpPath}");
                }
            }
        }

        if (compress)
        {
            using (FileStream compressedDumpStream = new FileStream(dumpPath, FileMode.Create))
            using (var archive = new ZipArchive(compressedDumpStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(Path.GetFileNameWithoutExtension(dumpPath) + ".dmp", CompressionLevel.Fastest);

                using (FileStream uncompressedDumpStream = File.Open(uncompressedDumpPath, FileMode.Open))
                using (var entryStream = entry.Open())
                {
                    uncompressedDumpStream.CopyTo(entryStream);
                }
            }

            File.Delete(uncompressedDumpPath);
        }
    }

    [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall,
        CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        SafeHandle hFile,
        uint dumpType,
        IntPtr expParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    /// <summary>
    /// Defined: http://msdn.microsoft.com/en-us/library/windows/desktop/ms680519(v=vs.85).aspx
    /// </summary>
    [Flags]
    public enum MiniDumpType : uint
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithFullMemory = 0x00000002,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpFilterMemory = 0x00000008,
        MiniDumpScanMemory = 0x00000010,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
        MiniDumpFilterModulePaths = 0x00000080,
        MiniDumpWithProcessThreadData = 0x00000100,
        MiniDumpWithPrivateReadWriteMemory = 0x00000200,
        MiniDumpWithoutOptionalData = 0x00000400,
        MiniDumpWithFullMemoryInfo = 0x00000800,
        MiniDumpWithThreadInfo = 0x00001000,
        MiniDumpWithCodeSegs = 0x00002000,
        MiniDumpWithoutAuxiliaryState = 0x00004000,
        MiniDumpWithFullAuxiliaryState = 0x00008000,
        MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
        MiniDumpIgnoreInaccessibleMemory = 0x00020000,
        MiniDumpWithTokenInformation = 0x00040000,
        MiniDumpWithModuleHeaders = 0x00080000,
        MiniDumpFilterTriage = 0x00100000,
        MiniDumpValidTypeFlags = 0x001fffff,
    }
}