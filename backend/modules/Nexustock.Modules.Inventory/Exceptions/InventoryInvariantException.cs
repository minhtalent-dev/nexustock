using System;

namespace Nexustock.Modules.Inventory.Exceptions;

public sealed class InventoryInvariantException : Exception
{
    public string ErrorCode { get; }

    public InventoryInvariantException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
