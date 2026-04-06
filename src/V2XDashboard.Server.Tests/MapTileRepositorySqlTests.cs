using System.Reflection;
using V2XDashboard.Server.Infrastructure.Persistence.Repositories;
using V2XDashboard.Shared;
using Xunit;

namespace V2XDashboard.Server.Tests;

public sealed class MapTileRepositorySqlTests
{
    private static readonly Type RepositoryType = typeof(MapTileRepository);

    [Fact]
    public void BuildSql_CamSpec_ContainsSecurityStationAndVehicleRolePredicates()
    {
        var spec = InvokeCreateStandardSpec(
            tableName: "cam_messages",
            layerName: TileLayerNames.Cam,
            selectList: "id, station_id, generation_time, station_type, vehicle_role, is_secure_signed, is_secure_encrypted",
            supportsStationType: true,
            supportsVehicleRole: true);

        var sql = InvokeBuildSql(spec, new TileFilterParams());

        Assert.Contains("is_secure_signed", sql, StringComparison.Ordinal);
        Assert.Contains("is_secure_encrypted", sql, StringComparison.Ordinal);
        Assert.Contains("station_type = ANY(@StationTypes::integer[])", sql, StringComparison.Ordinal);
        Assert.Contains("vehicle_role = ANY(@VehicleRoles::text[])", sql, StringComparison.Ordinal);
        Assert.Contains("FROM cam_messages", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSql_DenmSpec_DoesNotContainVehicleRolePredicate()
    {
        var spec = InvokeCreateStandardSpec(
            tableName: "denm_messages",
            layerName: TileLayerNames.Denm,
            selectList: "id, station_id, generation_time, station_type, cause_code, is_secure_signed, is_secure_encrypted",
            supportsStationType: true,
            supportsVehicleRole: false);

        var sql = InvokeBuildSql(spec, new TileFilterParams());

        Assert.Contains("station_type = ANY(@StationTypes::integer[])", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("vehicle_role = ANY(@VehicleRoles::text[])", sql, StringComparison.Ordinal);
        Assert.Contains("FROM denm_messages", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSql_CorrelationSpec_UsesSremTimestampAndNoSecurityPredicates()
    {
        var spec = CreateCorrelationSpec();

        var sql = InvokeBuildSql(spec, new TileFilterParams
        {
            FromTime = DateTime.UtcNow.AddMinutes(-10),
            ToTime = DateTime.UtcNow
        });

        Assert.Contains("srem_timestamp >= @FromTime::timestamp", sql, StringComparison.Ordinal);
        Assert.Contains("srem_timestamp <= @ToTime::timestamp", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@IsSecureSigned::boolean", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@IsSecureEncrypted::boolean", sql, StringComparison.Ordinal);
        Assert.Contains("FROM obu_rsu_correlations", sql, StringComparison.Ordinal);
    }

    private static object InvokeCreateStandardSpec(string tableName, string layerName, string selectList, bool supportsStationType, bool supportsVehicleRole)
    {
        var method = RepositoryType.GetMethod("CreateStandardSpec", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateStandardSpec method not found.");

        return method.Invoke(null, [tableName, layerName, selectList, supportsStationType, supportsVehicleRole])
            ?? throw new InvalidOperationException("CreateStandardSpec returned null.");
    }

    private static string InvokeBuildSql(object spec, TileFilterParams filters)
    {
        var method = RepositoryType.GetMethod("BuildSql", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildSql method not found.");

        return method.Invoke(null, [spec, filters]) as string
            ?? throw new InvalidOperationException("BuildSql returned null.");
    }

    private static object CreateCorrelationSpec()
    {
        var tileQuerySpecType = RepositoryType.GetNestedType("TileQuerySpec", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TileQuerySpec type not found.");

        return Activator.CreateInstance(
            tileQuerySpecType,
            "obu_rsu_correlations",
            TileLayerNames.Correlations,
            "id, srem_timestamp, FALSE AS is_secure_signed, FALSE AS is_secure_encrypted",
            "ST_MakeLine(ST_Transform(ST_SetSRID(ST_MakePoint(srem_longitude, srem_latitude), 4326), 3857), ST_Transform(ST_SetSRID(ST_MakePoint(ssem_longitude, ssem_latitude), 4326), 3857))",
            "ST_Intersects(ST_MakeLine(ST_Transform(ST_SetSRID(ST_MakePoint(srem_longitude, srem_latitude), 4326), 3857), ST_Transform(ST_SetSRID(ST_MakePoint(ssem_longitude, ssem_latitude), 4326), 3857)), tile_bbox.geom)",
            false,
            false,
            false,
            "srem_timestamp",
            "srem_latitude IS NOT NULL AND srem_longitude IS NOT NULL AND ssem_latitude IS NOT NULL AND ssem_longitude IS NOT NULL")
            ?? throw new InvalidOperationException("Failed to create TileQuerySpec instance.");
    }
}
