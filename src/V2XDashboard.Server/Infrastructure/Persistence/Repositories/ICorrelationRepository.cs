using V2XDashboard.Shared;

namespace V2XDashboard.Server.Infrastructure.Persistence.Repositories;

public interface ICorrelationRepository
{
    Task PopulateIntersectionMetadataForFileAsync(string fileName);
    Task<CorrelationRecordSummary> RecordOBUToRSUCorrelationAsync(int timeWindowSeconds);
    Task<List<CorrelationDto>> GetCorrelationsAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        int? limit = null);
    Task<PagedResult<CorrelationDto>> GetCorrelationsPagedAsync(
        string? obuStationId = null,
        string? rsuIntersectionId = null,
        string? requestId = null,
        string? correlationType = null,
        DateTime? fromTime = null,
        DateTime? toTime = null,
        bool? isSecureSigned = null,
        bool? isSecureEncrypted = null,
        int pageNumber = 1,
        int pageSize = 10);
    Task<SremSsemMatchDto?> GetSsemForSremAsync(int sremId);
}

public sealed class CorrelationRecordSummary
{
    public int TotalSrems { get; init; }
    public int SelectedCandidates { get; init; }
    public int InsertedTotal { get; init; }
    public int InsertedStrict { get; init; }
    public int InsertedFallback { get; init; }
}
