namespace FinGuardAML.Services;

public sealed class ExternalServiceException : Exception
{
    public ExternalServiceException(string message, Exception? inner = null)
        : base(message, inner) { }
}
