using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Server.Services.PcapReader.Interfaces;

namespace V2XDashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PcapController : ControllerBase
{
    private readonly IPacketQueryService _packetQueryService;
    private readonly IV2XMessageQueryService _messageQueryService;
    private readonly ICorrelationService _correlationService;
    private readonly IMapEntityService _mapEntityService;

    public PcapController(
        IPacketQueryService packetQueryService,
        IV2XMessageQueryService messageQueryService,
        ICorrelationService correlationService,
        IMapEntityService mapEntityService)
    {
        _packetQueryService = packetQueryService;
        _messageQueryService = messageQueryService;
        _correlationService = correlationService;
        _mapEntityService = mapEntityService;
    }


    [HttpGet("packets")]
    public async Task<IActionResult> GetPackets([FromQuery] string? filter = null, [FromQuery] int? limit = null)
    {
        var packets = await _packetQueryService.GetPacketsAsync(filter, limit);
        return Ok(packets);
    }

    [HttpGet("packets/paged")]
    public async Task<IActionResult> GetPacketsPaged(
        [FromQuery] string? filter = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        var packets = await _packetQueryService.GetPacketsPagedAsync(filter, pageNumber, pageSize);
        return Ok(packets);
    }

    [HttpGet("packets/{id}")]
    public async Task<IActionResult> GetPacketById(int id)
    {
        var packet = await _packetQueryService.GetPacketByIdAsync(id);
        if (packet == null)
        {
            return NotFound();
        }

        return Ok(packet);
    }

    [HttpGet("v2x-messages")]
    public async Task<IActionResult> GetV2XMessages([FromQuery] string? messageType = null, [FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetV2XMessagesAsync(messageType, limit);
        return Ok(messages);
    }

    [HttpGet("messages/counts")]
    public async Task<IActionResult> GetMessageCounts()
    {
        var counts = await _messageQueryService.GetMessageCountsAsync();
        return Ok(counts);
    }

    [HttpGet("messages/paged")]
    public async Task<IActionResult> GetMessageListPaged(
        [FromQuery] string messageType = "CAM",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        var messages = await _messageQueryService.GetMessageListPagedAsync(messageType, pageNumber, pageSize);
        return Ok(messages);
    }

    [HttpGet("v2x-messages/{id}")]
    public async Task<IActionResult> GetV2XMessageById(int id)
    {
        var message = await _messageQueryService.GetV2XMessageByIdAsync(id);
        if (message == null)
        {
            return NotFound();
        }

        return Ok(message);
    }

    [HttpGet("cam")]
    public async Task<IActionResult> GetCAMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetCAMMessagesAsync(limit);
        return Ok(messages);
    }

    [HttpGet("denm")]
    public async Task<IActionResult> GetDENMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetDENMMessagesAsync(limit);
        return Ok(messages);
    }

    [HttpGet("mapem")]
    public async Task<IActionResult> GetMAPEMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetMAPEMMessagesAsync(limit);
        return Ok(messages);
    }

    [HttpGet("spatem")]
    public async Task<IActionResult> GetSPATEMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetSPATEMMessagesAsync(limit);
        return Ok(messages);
    }

    [HttpGet("srem")]
    public async Task<IActionResult> GetSREMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetSREMMessagesAsync(limit);
        return Ok(messages);
    }

    [HttpGet("ssem")]
    public async Task<IActionResult> GetSSEMMessages([FromQuery] int? limit = null)
    {
        var messages = await _messageQueryService.GetSSEMMessagesAsync(limit);
        return Ok(messages);
    }

    // NEW: Correlation and map visualization endpoints

    [HttpGet("correlations")]
    public async Task<IActionResult> GetCorrelations(
        [FromQuery] string? obuStationId = null,
        [FromQuery] string? rsuIntersectionId = null,
        [FromQuery] string? requestId = null,
        [FromQuery] string? correlationType = null,
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null,
        [FromQuery] int? limit = null)
    {
        var correlations = await _correlationService.GetCorrelationsAsync(
            obuStationId, rsuIntersectionId, requestId, correlationType,
            fromTime, toTime, limit ?? 100);
        return Ok(correlations);
    }

    [HttpGet("correlations/paged")]
    public async Task<IActionResult> GetCorrelationsPaged(
        [FromQuery] string? obuStationId = null,
        [FromQuery] string? rsuIntersectionId = null,
        [FromQuery] string? requestId = null,
        [FromQuery] string? correlationType = null,
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var correlations = await _correlationService.GetCorrelationsPagedAsync(
            obuStationId, rsuIntersectionId, requestId, correlationType,
            fromTime, toTime, pageNumber, pageSize);
        return Ok(correlations);
    }

    [HttpGet("srem/{id}/ssem-match")]
    public async Task<IActionResult> GetSsemForSrem(int id)
    {
        var ssem = await _correlationService.GetSsemForSremAsync(id);
        if (ssem == null)
        {
            return NotFound($"No SSEM correlation found for SREM {id}");
        }

        return Ok(ssem);
    }

    [HttpGet("map-entities")]
    public async Task<IActionResult> GetMapEntities(
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null)
    {
        var entities = await _mapEntityService.GetMapEntitiesAsync(fromTime, toTime);
        return Ok(entities);
    }

    [HttpGet("map-entities/paged")]
    public async Task<IActionResult> GetMapEntitiesPaged(
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null,
        [FromQuery] List<string>? entityTypes = null,
        [FromQuery] List<string>? messageTypes = null,
        [FromQuery] List<string>? vehicleCategories = null,
        [FromQuery] List<int>? stationTypes = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = await _mapEntityService.GetMapEntitiesPagedAsync(
            fromTime,
            toTime,
            entityTypes,
            messageTypes,
            vehicleCategories,
            stationTypes,
            pageNumber,
            pageSize);

        return Ok(result);
    }

}