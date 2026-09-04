namespace Faed.Web.Models;

/// <summary>
/// Raised when a domain entity is asked to perform a transition that would violate an
/// invariant. Application services validate preconditions and return a result before
/// calling into the domain, so hitting this exception indicates a programming error
/// rather than routine user input.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
