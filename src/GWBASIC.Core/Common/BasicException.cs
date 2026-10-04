namespace GWBASIC.Core.Common;

/// <summary>
/// Exception thrown during GW-BASIC parsing or execution.
/// </summary>
public class BasicException : Exception
{
    public BasicErrorCode ErrorCode { get; }
    public int? LineNumber { get; set; }

    public BasicException(BasicErrorCode code, int? lineNumber = null, string? extraInfo = null)
        : base(FormatMessage(code, lineNumber, extraInfo))
    {
        ErrorCode = code;
        LineNumber = lineNumber;
    }

    private static string FormatMessage(BasicErrorCode code, int? lineNumber, string? extraInfo)
    {
        var msg = BasicErrorMessages.GetMessage(code);
        if (!string.IsNullOrEmpty(extraInfo))
        {
            msg = $"{msg} ({extraInfo})";
        }
        if (lineNumber.HasValue && lineNumber.Value > 0)
        {
            msg = $"{msg} in {lineNumber.Value}";
        }
        return msg;
    }
}
