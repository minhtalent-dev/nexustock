using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.ErpIntegration.Services;
using Nexustock.Modules.ErpIntegration.Entities;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Webhook.Services;

namespace Nexustock.Modules.ErpIntegration.Controllers;

[ApiController]
[Route("api/integration/inbound-orders")]
public class IntegrationInboundOrdersController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IContractVersionService _versionService;
    private readonly IIntegrationMappingService _mappingService;
    private readonly InboundDbContext _inboundContext;
    private readonly IWebhookOutboxService _webhookOutbox;
    private readonly ILogger<IntegrationInboundOrdersController> _logger;

    public IntegrationInboundOrdersController(
        ITenantProvider tenantProvider,
        IIdempotencyService idempotencyService,
        IContractVersionService versionService,
        IIntegrationMappingService mappingService,
        InboundDbContext inboundContext,
        IWebhookOutboxService webhookOutbox,
        ILogger<IntegrationInboundOrdersController> logger)
    {
        _tenantProvider = tenantProvider;
        _idempotencyService = idempotencyService;
        _versionService = versionService;
        _mappingService = mappingService;
        _inboundContext = inboundContext;
        _webhookOutbox = webhookOutbox;
        _logger = logger;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    [HttpPost]
    public async Task<IActionResult> ReceiveInboundOrder([FromBody] ErpInboundOrderPayloadDto dto)
    {
        var tenantId = GetTenantId();
        var traceId = HttpContext.TraceIdentifier;

        // 1. Validate Idempotency-Key header
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idemKey) || string.IsNullOrWhiteSpace(idemKey))
        {
            return BadRequest(new { errorCode = "integration.idempotencyKeyRequired", message = "Idempotency-Key header is required." });
        }

        var idempotencyKey = idemKey.ToString();

        // 2. Validate Contract Version
        if (!Request.Headers.TryGetValue("X-Contract-Version", out var versionHeader) || string.IsNullOrWhiteSpace(versionHeader))
        {
            versionHeader = "v1.1";
        }
        var version = versionHeader.ToString();

        var versionStatus = _versionService.CheckVersion(version);
        if (versionStatus == ContractVersionStatus.Retired)
        {
            return BadRequest(new { errorCode = "integration.contractVersionRetired", message = $"Contract version {version} is retired." });
        }

        // Get raw payload string for hashing and auditing
        // Note: in C# Web API, reading body multiple times requires EnableBuffering,
        // but we can just serialize the DTO back to JSON as canonical representation.
        var rawPayload = JsonSerializer.Serialize(dto);

        var messageType = "purchaseOrder";
        var extSystem = dto.IntegrationHeader?.ExternalSystem ?? "UNKNOWN";
        var extRef = dto.IntegrationHeader?.ExternalReference ?? "UNKNOWN";

        // 3. Check Idempotency Matrix
        var idemResult = await _idempotencyService.CheckIdempotencyAsync(
            tenantId,
            idempotencyKey,
            messageType,
            extSystem,
            extRef,
            version,
            rawPayload,
            traceId);

        if (idemResult.Status == IdempotencyStatus.Replay)
        {
            if (!string.IsNullOrEmpty(idemResult.ResponsePayload))
            {
                return Content(idemResult.ResponsePayload, "application/json");
            }
            return Ok(new { message = "Request already processed successfully." });
        }
        else if (idemResult.Status == IdempotencyStatus.Conflict)
        {
            return Conflict(new { errorCode = "integration.payloadHashMismatch", message = "Payload hash mismatch for the same idempotency key." });
        }

        // 4. Resolve mappings and create business mutation
        using var transaction = await _inboundContext.Database.BeginTransactionAsync();
        try
        {
            // Verify if order already exists in WMS
            var inboundOrder = dto.InboundOrder;
            var orderNo = inboundOrder?.EBELN;
            if (inboundOrder is null || string.IsNullOrWhiteSpace(orderNo))
            {
                throw new ArgumentException("EBELN (orderNo) is required.");
            }

            var orderExists = await _inboundContext.InboundOrders
                .AnyAsync(o => o.TenantId == tenantId && o.OrderNo == orderNo);

            if (orderExists)
            {
                var errorResponse = JsonSerializer.Serialize(new { errorCode = "validation.orderAlreadyProcessed", message = "Order already exists in WMS." });
                await _idempotencyService.SaveResponseAsync(tenantId, idempotencyKey, messageType, errorResponse, "failed", "validation.orderAlreadyProcessed", "Order already exists in WMS.");
                await transaction.RollbackAsync();
                return UnprocessableEntity(new { errorCode = "validation.orderAlreadyProcessed", message = "Order already exists in WMS." });
            }

            var partnerCode = inboundOrder.LIFNR;
            var warehouseCode = inboundOrder.WERKS;

            var partnerId = await _mappingService.ResolvePartnerAsync(tenantId, extSystem, partnerCode);
            var warehouseId = await _mappingService.ResolveWarehouseAsync(tenantId, extSystem, warehouseCode); // Validates existence

            var orderId = Guid.NewGuid();
            var username = User.Identity?.Name ?? "System";

            var wmsOrder = new InboundOrder
            {
                Id = orderId,
                TenantId = tenantId,
                OrderNo = orderNo,
                PartnerId = partnerId,
                Status = InboundOrderStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };

            var items = new List<InboundOrderItem>();
            foreach (var item in inboundOrder.Items ?? Enumerable.Empty<ErpInboundOrderItemDto>())
            {
                var itemId = await _mappingService.ResolveItemAsync(tenantId, extSystem, item.MATNR);
                var uomId = await _mappingService.ResolveUomAsync(tenantId, extSystem, item.MEINS);

                items.Add(new InboundOrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InboundOrderId = orderId,
                    ItemId = itemId,
                    UomId = uomId,
                    ExpectedQty = item.ExpectedQty,
                    ReceivedQty = 0,
                    Tolerance = 0.1m // Default tolerance 10%
                });
            }

            wmsOrder.Items = items;
            _inboundContext.InboundOrders.Add(wmsOrder);
            await _inboundContext.SaveChangesAsync();

            // Commit WMS Transaction
            await transaction.CommitAsync();

            var successResponseObj = new
            {
                messageId = Guid.NewGuid().ToString(),
                type = "integration.inboundOrder.response",
                timestamp = DateTime.UtcNow.ToString("o"),
                payload = new
                {
                    orderId = orderId,
                    orderNo = orderNo,
                    status = "Open",
                    traceId = traceId
                }
            };
            var successResponse = JsonSerializer.Serialize(successResponseObj);

            // Save successful response payload in idempotency log
            await _idempotencyService.SaveResponseAsync(tenantId, idempotencyKey, messageType, successResponse, "accepted");

            // Enqueue Webhook outbound event (best-effort: không rollback business nếu enqueue fail)
            try
            {
                var eventPayload = JsonSerializer.Serialize(new
                {
                    orderId = orderId.ToString(),
                    orderNo,
                    externalReference = extRef,
                    traceId
                });
                await _webhookOutbox.EnqueueAsync(tenantId, "inbound.completed", eventPayload, traceId);
            }
            catch (Exception webhookEx)
            {
                // Log lỗi nhưng không fail request vì business đã commit thành công
                _logger.LogWarning(webhookEx, "[WebhookOutbox] Enqueue inbound.completed thất bại. traceId={TraceId}", traceId);
            }

            return StatusCode(201, successResponseObj);
        }
        catch (UnresolvedMappingException ex)
        {
            await transaction.RollbackAsync();
            var errorResponse = JsonSerializer.Serialize(new { errorCode = ex.ErrorCode, message = ex.Message });
            await _idempotencyService.SaveResponseAsync(tenantId, idempotencyKey, messageType, errorResponse, "failed", ex.ErrorCode, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            var errorResponse = JsonSerializer.Serialize(new { errorCode = "integration.serverError", message = ex.Message });
            await _idempotencyService.SaveResponseAsync(tenantId, idempotencyKey, messageType, errorResponse, "failed", "integration.serverError", ex.Message);
            return StatusCode(500, new { errorCode = "integration.serverError", message = ex.Message });
        }
    }
}

public class ErpInboundOrderPayloadDto
{
    public ErpIntegrationHeaderDto? IntegrationHeader { get; set; }
    public ErpInboundOrderDto? InboundOrder { get; set; }
}

public class ErpIntegrationHeaderDto
{
    public string ExternalSystem { get; set; } = null!;
    public string ExternalReference { get; set; } = null!;
    public string ContractVersion { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string Timestamp { get; set; } = null!;
}

public class ErpInboundOrderDto
{
    public string tenantId { get; set; } = null!;
    public string WERKS { get; set; } = null!;
    public string EBELN { get; set; } = null!;
    public string LIFNR { get; set; } = null!;
    public string orderDate { get; set; } = null!;
    public string expectedArrivalDate { get; set; } = null!;
    public List<ErpInboundOrderItemDto>? Items { get; set; }
}

public class ErpInboundOrderItemDto
{
    public int EBELP { get; set; }
    public string MATNR { get; set; } = null!;
    public decimal ExpectedQty { get; set; }
    public string MEINS { get; set; } = null!;
}
