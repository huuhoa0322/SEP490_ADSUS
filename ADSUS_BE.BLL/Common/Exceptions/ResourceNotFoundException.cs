namespace ADSUS_BE.BLL.Common.Exceptions;

/// <summary>Thrown when a requested resource does not exist. Mapped to HTTP 404 by GlobalExceptionHandler.</summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message) { }
}
