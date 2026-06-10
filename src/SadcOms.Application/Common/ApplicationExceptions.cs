namespace SadcOms.Application.Common;

/// <summary>Requested resource does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>A conflict with the current state of a resource (e.g. concurrency). Mapped to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
