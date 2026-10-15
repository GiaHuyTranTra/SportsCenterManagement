using APIViewModel.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SportsCenterManagement.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Services.ScheduleQueryService.BusinessRuleException bre)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            int statusCode = bre.StatusCode ?? ResolveStatusCode(bre.Code);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            ApiErrorResponseAPIViewModel errorResponse = new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = bre.Code,
                    Message = bre.Message,
                    Details = bre.Details
                },
                TraceId = context.TraceIdentifier
            };

            string json = JsonSerializer.Serialize(errorResponse, JsonOptions);
            await context.Response.WriteAsync(json);
        }
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ||
            ex is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 1205 || sql.Number == 51010 || sql.Number == -2))
        {
            if (context.Response.HasStarted) throw;
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "CONCURRENCY_CONFLICT",
                    Message = "Dữ liệu đang được thay đổi. Vui lòng tải lại và thử lại."
                },
                TraceId = context.TraceIdentifier
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            ApiErrorResponseAPIViewModel errorResponse = new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "INTERNAL_SERVER_ERROR",
                    Message = "An unexpected error occurred.",
                    Details = null
                },
                TraceId = context.TraceIdentifier
            };

            string json = JsonSerializer.Serialize(errorResponse, JsonOptions);
            await context.Response.WriteAsync(json);
        }
    }

    private static int ResolveStatusCode(string code)
    {
        if (code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status404NotFound;
        }

        if (string.Equals(code, "FORBIDDEN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "ACCOUNT_INACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status403Forbidden;
        }

        if (string.Equals(code, "SESSION_IN_PAST", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "VALIDATION_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status400BadRequest;
        }

        return StatusCodes.Status409Conflict;
    }
}
