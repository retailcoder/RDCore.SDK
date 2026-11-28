using RDCore.SDK.Client;

namespace RDCore.SDK.Platform;

/// <summary>
/// This exception is thrown when a process reads the platform root (<see cref="IPlatformEnvironment.Root"/>) and was given none: neither
/// <c>--platform-root &lt;path&gt;</c> on its command line nor the <c>RDCORE_PLATFORM_ROOT</c> environment variable
/// (<see cref="PlatformEnvironment.RootEnvironmentVariable"/>) names it.
/// </summary>
/// <remarks>
/// The root is never derived from the folder of the process's own files. A process that needs no platform path (its logs, its user's
/// data) never reads it, and never throws this.
/// </remarks>
public sealed class PlatformRootNotGivenException()
    : BadConfigurationException(Exceptions.PlatformRootNotGivenException_Message, Exceptions.PlatformRootNotGivenException_Verbose)
{
}
