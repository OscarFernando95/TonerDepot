using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

// El proyecto no usa ninguna librería de mocking (Moq/NSubstitute) en ningún test existente — este
// fake sigue esa misma convención en vez de introducir una dependencia nueva solo para esto.
public sealed class FakeBackgroundJobScheduler : IBackgroundJobScheduler
{
    public List<(string Cedula, string GeneratedPassword)> EnqueuedEmails { get; } = new();

    public void EnqueueGeneratedPasswordEmail(string cedula, string generatedPassword) =>
        EnqueuedEmails.Add((cedula, generatedPassword));
}
