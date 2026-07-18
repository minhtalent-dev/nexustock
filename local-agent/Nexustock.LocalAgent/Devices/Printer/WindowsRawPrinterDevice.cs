using System.Runtime.InteropServices;
using System.Text;

namespace Nexustock.LocalAgent.Devices.Printer;

/// <summary>
/// Gửi RAW command trực tiếp đến máy in Windows qua spooler API (GDI RAW print).
/// Chỉ hoạt động trên Windows — không dùng GDI+, không render, chỉ đẩy byte thô.
/// </summary>
public class WindowsRawPrinterDevice : IPrinterDevice
{
    private readonly PrinterDeviceConfig _config;

    public WindowsRawPrinterDevice(PrinterDeviceConfig config)
    {
        _config = config;
    }

    public string PrinterCode => _config.PrinterCode;
    public string Language => _config.Language;

    public Task<PrinterResult> PrintAsync(string rawCommand, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(new PrinterResult(false, "failed", "printer.command_rejected", "WindowsRawPrinterDevice chỉ hỗ trợ nền tảng Windows."));
        }

        if (string.IsNullOrEmpty(_config.PrinterName))
        {
            return Task.FromResult(new PrinterResult(false, "failed", "printer.command_rejected", "Thiếu cấu hình PrinterName cho Windows printer."));
        }

        try
        {
            var rawBytes = Encoding.UTF8.GetBytes(rawCommand);
            var success = RawPrint(_config.PrinterName, rawBytes);

            return Task.FromResult(success
                ? new PrinterResult(true, "printed")
                : new PrinterResult(false, "failed", "printer.offline", $"Windows spooler từ chối lệnh in cho máy in '{_config.PrinterName}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PrinterResult(false, "failed", "printer.timeout", $"Lỗi Windows spooler: {ex.Message}"));
        }
    }

    public Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult("unavailable");
        }

        var handle = NativeMethods.OpenPrinter(_config.PrinterName, out _, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            return Task.FromResult("offline");
        }

        NativeMethods.ClosePrinter(handle);
        return Task.FromResult("online");
    }

    // --------------- Win32 RAW print helper ---------------

    private static bool RawPrint(string printerName, byte[] bytes)
    {
        var handle = NativeMethods.OpenPrinter(printerName, out _, IntPtr.Zero);
        if (handle == IntPtr.Zero) return false;

        try
        {
            var di = new NativeMethods.DOCINFOA
            {
                pDocName = "Nexustock RAW Label",
                pDataType = "RAW"
            };

            if (NativeMethods.StartDocPrinter(handle, 1, ref di) == 0) return false;
            if (!NativeMethods.StartPagePrinter(handle)) return false;

            var ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                NativeMethods.WritePrinter(handle, ptr, bytes.Length, out var written);
                return written == bytes.Length;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        finally
        {
            NativeMethods.EndPagePrinter(handle);
            NativeMethods.EndDocPrinter(handle);
            NativeMethods.ClosePrinter(handle);
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOA di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBuf, int cbBuf, out int pcWritten);
    }
}
