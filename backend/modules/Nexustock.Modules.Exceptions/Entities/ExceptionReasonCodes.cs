using System.Collections.Generic;

namespace Nexustock.Modules.Exceptions.Entities;

public static class ExceptionReasonCodes
{
    public const string Shortage = "SHORTAGE";
    public const string Overage = "OVERAGE";
    public const string LotMismatch = "LOT_MISMATCH";
    public const string LocationLocked = "LOCATION_LOCKED";
    public const string BarcodeInvalid = "BARCODE_INVALID";
    public const string HardwareError = "HARDWARE_ERROR";

    public static readonly HashSet<string> All = new()
    {
        Shortage,
        Overage,
        LotMismatch,
        LocationLocked,
        BarcodeInvalid,
        HardwareError
    };
}
