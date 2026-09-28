using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Auth;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

public static class TestSessions
{
    public static SessionService Create(IApplicationDbContext db, int idleTimeoutMinutes = 30, int touchIntervalSeconds = 60) =>
        new(db, Options.Create(new UserSessionOptions { IdleTimeoutMinutes = idleTimeoutMinutes, TouchIntervalSeconds = touchIntervalSeconds }),
            NullLogger<SessionService>.Instance);
}
