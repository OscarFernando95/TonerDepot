using Toner.Infrastructure.Notifications;

namespace Toner.Application.Tests.Auth;

public class PasswordDeliverySettingsValidatorTests
{
    [Fact]
    public void EnsureValid_AdminEmailVacio_Lanza()
    {
        var settings = new PasswordDeliverySettings { AdminEmail = "" };

        var ex = Assert.Throws<InvalidOperationException>(() => PasswordDeliverySettingsValidator.EnsureValid(settings));
        Assert.Contains("PasswordDelivery:AdminEmail", ex.Message);
    }

    [Fact]
    public void EnsureValid_AdminEmailMalFormado_Lanza()
    {
        var settings = new PasswordDeliverySettings { AdminEmail = "no-es-un-correo" };

        Assert.Throws<InvalidOperationException>(() => PasswordDeliverySettingsValidator.EnsureValid(settings));
    }

    [Fact]
    public void EnsureValid_AdminEmailValido_NoLanza()
    {
        var settings = new PasswordDeliverySettings { AdminEmail = "notificaciones-rrhh@toner.local" };

        var exception = Record.Exception(() => PasswordDeliverySettingsValidator.EnsureValid(settings));

        Assert.Null(exception);
    }
}
