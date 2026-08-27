using Toner.Infrastructure.Email;

namespace Toner.Application.Tests.Auth;

public class SmtpSettingsValidatorTests
{
    private static SmtpSettings ValidSettings() => new()
    {
        Host = "localhost",
        Port = 2525,
        FromAddress = "no-reply@toner.local"
    };

    [Fact]
    public void EnsureValid_HostVacio_Lanza()
    {
        var settings = ValidSettings();
        settings.Host = "";

        var ex = Assert.Throws<InvalidOperationException>(() => SmtpSettingsValidator.EnsureValid(settings));
        Assert.Contains("Smtp:Host", ex.Message);
    }

    [Fact]
    public void EnsureValid_PuertoCeroOMenor_Lanza()
    {
        var settings = ValidSettings();
        settings.Port = 0;

        Assert.Throws<InvalidOperationException>(() => SmtpSettingsValidator.EnsureValid(settings));
    }

    [Fact]
    public void EnsureValid_FromAddressMalFormado_Lanza()
    {
        var settings = ValidSettings();
        settings.FromAddress = "no-es-un-correo";

        Assert.Throws<InvalidOperationException>(() => SmtpSettingsValidator.EnsureValid(settings));
    }

    [Fact]
    public void EnsureValid_ConfiguracionValida_NoLanza()
    {
        var exception = Record.Exception(() => SmtpSettingsValidator.EnsureValid(ValidSettings()));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureValid_UsernameYPasswordVacios_NoLanza()
    {
        // smtp4dev (desarrollo) acepta relay anónimo — Username/Password vacíos son válidos.
        var settings = ValidSettings();
        settings.Username = null;
        settings.Password = null;

        var exception = Record.Exception(() => SmtpSettingsValidator.EnsureValid(settings));

        Assert.Null(exception);
    }
}
