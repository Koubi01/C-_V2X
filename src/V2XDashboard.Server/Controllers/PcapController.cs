using Microsoft.AspNetCore.Mvc;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PcapController : ControllerBase
{
    private readonly IPcapService _pcapService;
    private readonly IConfiguration _configuration;

    public PcapController(IPcapService pcapService, IConfiguration configuration)
    {
        _pcapService = pcapService;
        _configuration = configuration;
    }

    [HttpGet("files")]
    public async Task<IActionResult> GetPcapFiles()
    {
        try
        {
            var files = await _pcapService.GetPcapFilesAsync();
            return Ok(files);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving PCAP files: {ex.Message}");
        }
    }

    [HttpPost("process/{fileName}")]
    public async Task<IActionResult> ProcessPcapFile(string fileName)
    {
        try
        {
            var success = await _pcapService.ProcessPcapFileAsync(fileName);
            if (success)
            {
                return Ok($"Successfully processed {fileName}");
            }
            else
            {
                return BadRequest($"Failed to process {fileName}");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing PCAP file: {ex.Message}");
        }
    }

    [HttpGet("process/allFiles")]
    public async Task<IActionResult> ProcessAllPcapFiles()
    {
        try
        {
            var success = await _pcapService.ProcessAllPcapFilesAsync();
            if (success)
            {
                return Ok("Successfully processed all PCAP files");
            }
            else
            {
                return BadRequest("Failed to process some PCAP files");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing PCAP files: {ex.Message}");
        }
    }


    [HttpGet("packets")]
    public async Task<IActionResult> GetPackets([FromQuery] string? filter = null, [FromQuery] int? limit = null)
    {
        try
        {
            var packets = await _pcapService.GetPacketsAsync(filter, limit);
            return Ok(packets);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving packets: {ex.Message}");
        }
    }

    [HttpGet("packets/paged")]
    public async Task<IActionResult> GetPacketsPaged(
        [FromQuery] string? filter = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        try
        {
            var packets = await _pcapService.GetPacketsPagedAsync(filter, pageNumber, pageSize);
            return Ok(packets);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving paged packets: {ex.Message}");
        }
    }

    [HttpGet("packets/{id}")]
    public async Task<IActionResult> GetPacketById(int id)
    {
        try
        {
            var packet = await _pcapService.GetPacketByIdAsync(id);
            if (packet == null)
            {
                return NotFound();
            }
            return Ok(packet);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving packet: {ex.Message}");
        }
    }

    [HttpGet("v2x-messages")]
    public async Task<IActionResult> GetV2XMessages([FromQuery] string? messageType = null, [FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetV2XMessagesAsync(messageType, limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving V2X messages: {ex.Message}");
        }
    }

    [HttpGet("messages/paged")]
    public async Task<IActionResult> GetMessageListPaged(
        [FromQuery] string messageType = "CAM",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        try
        {
            var messages = await _pcapService.GetMessageListPagedAsync(messageType, pageNumber, pageSize);
            return Ok(messages);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving paged message list: {ex.Message}");
        }
    }

    [HttpGet("v2x-messages/{id}")]
    public async Task<IActionResult> GetV2XMessageById(int id)
    {
        try
        {
            var message = await _pcapService.GetV2XMessageByIdAsync(id);
            if (message == null)
            {
                return NotFound();
            }
            return Ok(message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving V2X message: {ex.Message}");
        }
    }

    [HttpGet("cam")]
    public async Task<IActionResult> GetCAMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetCAMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving CAM messages: {ex.Message}");
        }
    }

    [HttpGet("denm")]
    public async Task<IActionResult> GetDENMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetDENMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving DENM messages: {ex.Message}");
        }
    }

    [HttpGet("mapem")]
    public async Task<IActionResult> GetMAPEMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetMAPEMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving MAPEM messages: {ex.Message}");
        }
    }

    [HttpGet("spatem")]
    public async Task<IActionResult> GetSPATEMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetSPATEMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving SPATEM messages: {ex.Message}");
        }
    }

    [HttpGet("srem")]
    public async Task<IActionResult> GetSREMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetSREMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving SREM messages: {ex.Message}");
        }
    }

    [HttpGet("ssem")]
    public async Task<IActionResult> GetSSEMMessages([FromQuery] int? limit = null)
    {
        try
        {
            var messages = await _pcapService.GetSSEMMessagesAsync(limit);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving SSEM messages: {ex.Message}");
        }
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
        try
        {
            var correlations = await _pcapService.GetCorrelationsAsync(
                obuStationId, rsuIntersectionId, requestId, correlationType,
                fromTime, toTime, limit ?? 100);
            return Ok(correlations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving correlations: {ex.Message}");
        }
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
        try
        {
            var correlations = await _pcapService.GetCorrelationsPagedAsync(
                obuStationId, rsuIntersectionId, requestId, correlationType,
                fromTime, toTime, pageNumber, pageSize);
            return Ok(correlations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving paged correlations: {ex.Message}");
        }
    }

    [HttpGet("srem/{id}/ssem-match")]
    public async Task<IActionResult> GetSsemForSrem(int id)
    {
        try
        {
            var ssem = await _pcapService.GetSsemForSremAsync(id);
            if (ssem == null)
            {
                return NotFound($"No SSEM correlation found for SREM {id}");
            }
            return Ok(ssem);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving SSEM match: {ex.Message}");
        }
    }

    [HttpGet("map-entities")]
    public async Task<IActionResult> GetMapEntities(
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null)
    {
        try
        {
            var entities = await _pcapService.GetMapEntitiesAsync(fromTime, toTime);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving map entities: {ex.Message}");
        }
    }

    [HttpPost("correlations/record")]
    public async Task<IActionResult> RecordCorrelations()
    {
        try
        {
            await _pcapService.RecordOBUToRSUCorrelationAsync();
            return Ok("Correlations recorded successfully");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error recording correlations: {ex.Message}");
        }
    }

    [HttpGet("map-config")]
    public IActionResult GetMapConfig()
    {
        var mapSection = _configuration.GetSection("MapConfig");

        var mapConfig = new MapConfigDto
        {
            TileStyleUrl = mapSection.GetValue<string>("TileStyleUrl") ?? "http://localhost:8081/styles/basic/style.json",
            DefaultCenterLatitude = mapSection.GetValue<double?>("DefaultCenterLatitude") ?? 50.0755,
            DefaultCenterLongitude = mapSection.GetValue<double?>("DefaultCenterLongitude") ?? 14.4378,
            DefaultZoom = mapSection.GetValue<double?>("DefaultZoom") ?? 12,
            MinZoom = mapSection.GetValue<double?>("MinZoom") ?? 3,
            MaxZoom = mapSection.GetValue<double?>("MaxZoom") ?? 20
        };

        return Ok(mapConfig);
    }
}