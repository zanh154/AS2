using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Common;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Input checks shared by the services. Every failure is a
/// <see cref="BusinessValidationException"/>, so the message reaches the user as-is.
/// </summary>
internal static class BusinessValidation
{
    public static string RequiredText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessValidationException($"Vui lòng nhập {fieldName}.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessValidationException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    public static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new BusinessValidationException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    public static string NormalizeCode(string? value, string fieldName, int maxLength)
    {
        return RequiredText(value, fieldName, maxLength).ToUpperInvariant();
    }

    public static int PositiveId(int value, string fieldName)
    {
        if (value <= 0)
        {
            throw new BusinessValidationException($"Vui lòng chọn {fieldName}.");
        }

        return value;
    }

    public static int InRange(int value, int min, int max, string fieldName)
    {
        if (value < min || value > max)
        {
            throw new BusinessValidationException($"{fieldName} phải từ {min} đến {max}.");
        }

        return value;
    }

    public static DateOnly RequiredDate(DateOnly value, string fieldName)
    {
        if (value == default)
        {
            throw new BusinessValidationException($"Vui lòng chọn {fieldName}.");
        }

        return value;
    }
}

/// <summary>
/// Runs a service operation and turns the exceptions it may throw into the
/// error messages the views display, so no controller ever sees a raw exception.
/// </summary>
internal static class ServiceExecutor
{
    public static async Task<ServiceResponse<T>> RunAsync<T>(Func<Task<T>> operation, string failureMessage)
    {
        try
        {
            return ServiceResponse<T>.Ok(await operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessValidationException exception)
        {
            return ServiceResponse<T>.Fail(exception.Errors);
        }
        catch (ExamScheduleConflictException exception)
        {
            return ServiceResponse<T>.Fail(exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return ServiceResponse<T>.Fail(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ServiceResponse<T>.Fail(exception.Message);
        }
        catch (Exception)
        {
            return ServiceResponse<T>.Fail(failureMessage);
        }
    }

    public static async Task<ServiceResponse> RunAsync(Func<Task> operation, string failureMessage)
    {
        var response = await RunAsync<bool>(
            async () =>
            {
                await operation();
                return true;
            },
            failureMessage);

        return response.Success ? ServiceResponse.Ok() : ServiceResponse.Fail(response.Errors);
    }
}
