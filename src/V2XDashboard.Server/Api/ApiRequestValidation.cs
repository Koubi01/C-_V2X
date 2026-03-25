using System.Globalization;

namespace V2XDashboard.Server.Api;

internal static class ApiRequestValidation
{
    public static IResult? ValidatePaging(int pageNumber, int pageSize, int maxPageSize = 500)
    {
        if (pageNumber < 1)
        {
            return ValidationProblem("pageNumber", "pageNumber must be >= 1.");
        }

        if (pageSize < 1)
        {
            return ValidationProblem("pageSize", "pageSize must be >= 1.");
        }

        if (pageSize > maxPageSize)
        {
            return ValidationProblem("pageSize", $"pageSize must be <= {maxPageSize}.");
        }

        return null;
    }

    public static IResult? ValidateLimit(int? limit, int maxLimit = 5000)
    {
        if (!limit.HasValue)
        {
            return null;
        }

        if (limit.Value < 1)
        {
            return ValidationProblem("limit", "limit must be >= 1.");
        }

        if (limit.Value > maxLimit)
        {
            return ValidationProblem("limit", $"limit must be <= {maxLimit}.");
        }

        return null;
    }

    public static IResult? ValidateDateRange(DateTime? fromTime, DateTime? toTime)
    {
        if (!fromTime.HasValue || !toTime.HasValue)
        {
            return null;
        }

        if (fromTime.Value > toTime.Value)
        {
            return ValidationProblem(
                "fromTime",
                $"fromTime ({fromTime.Value.ToString("O", CultureInfo.InvariantCulture)}) must be <= toTime ({toTime.Value.ToString("O", CultureInfo.InvariantCulture)}).",
                "Invalid time range.");
        }

        return null;
    }

    public static IResult? ValidateRequiredNonEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationProblem(fieldName, $"{fieldName} is required.");
        }

        return null;
    }

    private static IResult ValidationProblem(string field, string message, string title = "Invalid request.")
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [field] = new[] { message }
            },
            title: title,
            statusCode: StatusCodes.Status400BadRequest);
    }
}
