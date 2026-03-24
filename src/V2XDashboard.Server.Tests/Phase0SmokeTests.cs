using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using V2XDashboard.Server;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public class Phase0SmokeTests : IClassFixture<Phase0SmokeTests.TestAppFactory>
{
    private readonly HttpClient _client;

    public Phase0SmokeTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessOneFile_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/pcap/process/data1.pcap", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QueryCam_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/messages/cam?limit=10");
        var payload = await response.Content.ReadFromJsonAsync<List<CAM>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);
    }

    [Fact]
    public async Task QueryMapEntitiesPaged_ReturnsOkWithPagedResult()
    {
        var response = await _client.GetAsync("/api/map/entities/paged?pageNumber=1&pageSize=25");
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<MapEntityDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount > 0);
        Assert.NotEmpty(payload.Items);
    }

    [Fact]
    public async Task QueryCorrelationsPaged_ReturnsOkWithPagedResult()
    {
        var response = await _client.GetAsync("/api/correlations/paged?pageNumber=1&pageSize=10");
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<CorrelationDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount > 0);
        Assert.NotEmpty(payload.Items);
    }

    public sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IPcapIngestionService, FakePcapIngestionService>();
                services.AddSingleton<IV2XMessageQueryService, FakeMessageQueryService>();
                services.AddSingleton<IMapEntityService, FakeMapEntityService>();
                services.AddSingleton<ICorrelationService, FakeCorrelationService>();
            });
        }
    }

    private sealed class FakePcapIngestionService : IPcapIngestionService
    {
        public Task<List<string>> GetPcapFilesAsync() => Task.FromResult(new List<string> { "data1.pcap" });
        public Task<bool> ProcessPcapFileAsync(string fileName) => Task.FromResult(true);
        public Task<bool> ProcessAllPcapFilesAsync() => Task.FromResult(true);
    }

    private sealed class FakeMessageQueryService : IV2XMessageQueryService
    {
        public Task<List<V2XMessage>> GetV2XMessagesAsync(string? messageType = null, int? limit = null)
            => Task.FromResult<List<V2XMessage>>(new List<V2XMessage> { BuildCam() });

        public Task<PagedResult<MessageListItemDto>> GetMessageListPagedAsync(string messageType, int pageNumber = 1, int pageSize = 25)
            => Task.FromResult(new PagedResult<MessageListItemDto>
            {
                Items = new List<MessageListItemDto>
                {
                    new() { MessageType = messageType, StationLabel = "station-a", Detail = "detail", GenerationTime = DateTime.UtcNow }
                },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<MessageCountsDto> GetMessageCountsAsync()
            => Task.FromResult(new MessageCountsDto { TotalPackets = 1, CAM = 1, TotalCorrelations = 1 });

        public Task<List<CAM>> GetCAMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<CAM> { BuildCam() });

        public Task<List<DENM>> GetDENMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<DENM>());

        public Task<List<MAPEM>> GetMAPEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<MAPEM>());

        public Task<List<SPATEM>> GetSPATEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SPATEM>());

        public Task<List<SREM>> GetSREMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SREM>());

        public Task<List<SSEM>> GetSSEMMessagesAsync(int? limit = null)
            => Task.FromResult(new List<SSEM>());

        public Task<V2XMessage?> GetV2XMessageByIdAsync(int id)
            => Task.FromResult<V2XMessage?>(BuildCam());

        private static CAM BuildCam()
        {
            return new CAM
            {
                Id = 1,
                PacketId = 1,
                StationId = "station-a",
                GenerationTime = DateTime.UtcNow,
                Latitude = 49.82,
                Longitude = 18.26,
                Speed = 10
            };
        }
    }

    private sealed class FakeMapEntityService : IMapEntityService
    {
        public Task<List<MapEntityDto>> GetMapEntitiesAsync(DateTime? fromTime = null, DateTime? toTime = null)
            => Task.FromResult(new List<MapEntityDto> { BuildEntity() });

        public Task<PagedResult<MapEntityDto>> GetMapEntitiesPagedAsync(
            DateTime? fromTime = null,
            DateTime? toTime = null,
            IEnumerable<string>? entityTypes = null,
            IEnumerable<string>? messageTypes = null,
            IEnumerable<string>? vehicleCategories = null,
            IEnumerable<int>? stationTypes = null,
            int pageNumber = 1,
            int pageSize = 100)
            => Task.FromResult(new PagedResult<MapEntityDto>
            {
                Items = new List<MapEntityDto> { BuildEntity() },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        private static MapEntityDto BuildEntity()
        {
            return new MapEntityDto
            {
                EntityType = "OBU",
                MessageType = "CAM",
                StationId = "station-a",
                Latitude = 49.82,
                Longitude = 18.26,
                GenerationTime = DateTime.UtcNow
            };
        }
    }

    private sealed class FakeCorrelationService : ICorrelationService
    {
        public Task<List<CorrelationDto>> GetCorrelationsAsync(
            string? obuStationId = null,
            string? rsuIntersectionId = null,
            string? requestId = null,
            string? correlationType = null,
            DateTime? fromTime = null,
            DateTime? toTime = null,
            int? limit = null)
            => Task.FromResult(new List<CorrelationDto> { BuildCorrelation() });

        public Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
            string? obuStationId = null,
            string? rsuIntersectionId = null,
            string? requestId = null,
            string? correlationType = null,
            DateTime? fromTime = null,
            DateTime? toTime = null,
            int pageNumber = 1,
            int pageSize = 10)
            => Task.FromResult(new PagedResult<CorrelationDto>
            {
                Items = new List<CorrelationDto> { BuildCorrelation() },
                TotalCount = 1,
                PageNumber = pageNumber,
                PageSize = pageSize
            });

        public Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId)
            => Task.FromResult<SremSsemMatchDto?>(new SremSsemMatchDto
            {
                Id = 1,
                SsemId = 1,
                CorrelationType = "strict",
                MatchConfidence = 1.0
            });

        public Task RecordOBUToRSUCorrelationAsync() => Task.CompletedTask;

        private static CorrelationDto BuildCorrelation()
        {
            return new CorrelationDto
            {
                Id = 1,
                CorrelationType = "strict",
                MatchConfidence = 1.0,
                SremTimestamp = DateTime.UtcNow,
                SsemTimestamp = DateTime.UtcNow,
                TimeDeltaMs = 25
            };
        }
    }
}
