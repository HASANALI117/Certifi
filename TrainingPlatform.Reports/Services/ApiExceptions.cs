namespace TrainingPlatform.Reports.Services;

public class ApiUnauthorizedException : Exception
{
    public ApiUnauthorizedException(string message) : base(message) { }
}

public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
