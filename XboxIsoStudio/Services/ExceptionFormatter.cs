using System.Globalization;
using System.Text;

namespace XboxIsoStudio.Services;

/// <summary>
/// Formats exceptions, including aggregate and inner exceptions, as human-readable text
/// appended to a <see cref="StringBuilder"/>.
/// </summary>
public static class ExceptionFormatter
{
    /// <summary>
    /// Appends the type, message, source, and stack trace of the specified exception and all
    /// of its inner or aggregate exceptions to the builder.
    /// </summary>
    /// <param name="sb">Builder that receives the formatted details.</param>
    /// <param name="exception">Exception to format.</param>
    /// <param name="level">Current nesting level used for indentation.</param>
    public static void AppendExceptionDetails(StringBuilder sb, Exception exception, int level = 0)
    {
        while (true)
        {
            var indent = new string(' ', level * 2);

            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}Type: {exception.GetType().FullName}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}Message: {exception.Message}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}Source: {exception.Source}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}StackTrace:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}{exception.StackTrace}");

            if (exception is AggregateException aggregateException)
            {
                var innerExceptions = aggregateException.InnerExceptions;
                for (var i = 0; i < innerExceptions.Count; i++)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}Inner Exception [{i}]:");
                    AppendExceptionDetails(sb, innerExceptions[i], level + 1);
                }

                return;
            }

            if (exception.InnerException != null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}Inner Exception:");
                exception = exception.InnerException;
                level++;
                continue;
            }

            break;
        }
    }
}