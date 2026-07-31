namespace ADSUS_BE.BLL.Common.Exceptions;

/// <summary>Thrown on a business-rule violation (e.g. slot already FULL). Mapped to HTTP 422 by GlobalExceptionHandler.</summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}
