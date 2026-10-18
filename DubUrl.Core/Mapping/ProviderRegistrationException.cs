using System;

namespace DubUrl.Mapping;

public sealed class ProviderRegistrationException : ArgumentException
{
    public string Alias { get; }
    public string ExistingRegistration { get; }
    public string ConflictingRegistration { get; }

    internal ProviderRegistrationException(string alias, string existingRegistration, string conflictingRegistration)
        : base(
            $"Scheme alias '{alias}' is registered by both '{existingRegistration}' and '{conflictingRegistration}'. " +
            "Each scheme alias must identify exactly one provider.",
            nameof(alias))
        => (Alias, ExistingRegistration, ConflictingRegistration) =
            (alias, existingRegistration, conflictingRegistration);
}
