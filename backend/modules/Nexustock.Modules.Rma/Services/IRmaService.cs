using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nexustock.Modules.Rma.DTOs;

namespace Nexustock.Modules.Rma.Services;

public interface IRmaService
{
    Task<RmaDto> CreateRmaAsync(CreateRmaDto dto, string operatorName);
    Task<RmaDto> ReceiveRmaAsync(Guid rmaId, ReceiveRmaDto dto, string operatorName);
    Task<RmaDto> ProcessRmaQcAsync(Guid rmaId, ProcessRmaQcDto dto, string operatorName);
    Task<RmaDto> GetRmaDetailsAsync(Guid rmaId);
    Task<List<RmaDto>> GetAllRmasAsync();
}
