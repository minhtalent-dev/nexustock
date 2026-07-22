using System;

namespace Nexustock.Modules.Qc.Abstractions;

public class QcGateException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatus { get; }

    public QcGateException(string errorCode, string message, int httpStatus = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatus = httpStatus;
    }
}
