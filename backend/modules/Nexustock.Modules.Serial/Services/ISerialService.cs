using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nexustock.Modules.Serial.DTOs;

namespace Nexustock.Modules.Serial.Services;

public interface ISerialService
{
    Task<SerialDto> ReceiveSerialAsync(ReceiveSerialDto dto, string operatorName);
    Task<bool> ValidateSerialForPickAsync(ValidateSerialDto dto);
    Task<List<SerialDto>> ImportFromCsvAsync(Stream csvStream, Guid itemId, Guid locationId, string operatorName);
    Task<List<object>> GetSerialTimelineAsync(string serialNo);
}
