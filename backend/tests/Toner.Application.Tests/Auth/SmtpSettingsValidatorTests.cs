using MailKit.Security;
using Toner.Infrastructure.Email;

namespace Toner.Application.Tests.Auth;

public class SmtpSettingsValidatorTests
{
    private static SmtpSettings ValidSettings() => new()
    {
        Host = "localhost",
        Port = 587,
        FromAddress = "no-reply@toner.local"
    };

    private static void Valid(SmtpSettings settings, bool isDevelopment = false) =>
        Assert.Null(Record.Exception(() => SmtpSettingsValidator.EnsureValid(settings, isDevelopment)));

    private static InvalidOperationException Invalid(SmtpSettings settings, bool isDevelopment = false) =>
        Assert.Throws<InvalidOperationException>(() => SmtpSettingsValidator.EnsureValid(settings, isDevelopment));

    [Fact]
    public void EnsureValid_HostVacio_Lanza()
    {
        var settings = ValidSettings();
        settings.Host = "";

        Assert.Contains("Smtp:Host", Invalid(settings).Message);
    }

    [Fact]
    public void EnsureValid_PuertoCeroOMenor_Lanza()
    {
        var settings = ValidSettings();
        settings.Port = 0;

        Invalid(settings);
    }

    [Fact]
    public void EnsureValid_FromAddressMalFormado_Lanza()
    {
        var settings = ValidSettings();
        settings.FromAddress = "no-es-un-correo";

        Invalid(settings);
    }

    [Fact]
    public void EnsureValid_ConfiguracionValida_NoLanza() => Valid(ValidSettings());

    [Fact]
    public void EnsureValid_UsernameYPasswordVacios_NoLanza()
    {
        // Relay anónimo (smtp4dev en desarrollo): ambos vacíos es válido.
        var settings = ValidSettings();
        settings.Username = null;
        settings.Password = "";

        Valid(settings);
    }

    // ── H-16: usuario y contraseña van juntos ────────────────────────────────────────────────────

    [Theory]
    [InlineData("usuario", null)]
    [InlineData("usuario", "")]
    [InlineData(null, "secreto")]
    [InlineData("", "secreto")]
    public void EnsureValid_SoloUnoDeUsernameOPassword_Lanza(string? username, string? password)
    {
        var settings = ValidSettings();
        settings.Username = username;
        settings.Password = password;

        var ex = Invalid(settings);
        Assert.Contains("Smtp:Username", ex.Message);
        Assert.Contains("Smtp:Password", ex.Message);
        Assert.DoesNotContain("secreto", ex.Message); // nunca se loguea el valor
    }

    [Fact]
    public void EnsureValid_UsernameYPasswordJuntos_NoLanza()
    {
        var settings = ValidSettings();
        settings.Username = "usuario";
        settings.Password = "secreto";

        Valid(settings);
    }

    // ── H-04: transporte ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SinValor_ElDefaultEsStartTls()
    {
        var settings = ValidSettings();
        Valid(settings);
        Assert.Equal(SecureSocketOptions.StartTls, settings.GetSecureSocketOptions());
    }

    [Theory]
    [InlineData("None")]
    [InlineData("StartTlsWhenAvailable")]
    [InlineData("Auto")]
    public void FueraDeDevelopment_ModosQuePermitenTextoPlano_Lanzan(string mode)
    {
        var settings = ValidSettings();
        settings.SecureSocketOptions = mode;

        Assert.Contains("Smtp:SecureSocketOptions", Invalid(settings, isDevelopment: false).Message);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("StartTlsWhenAvailable")]
    public void EnDevelopment_ModosSinTlsObligatorio_SePermiten(string mode)
    {
        var settings = ValidSettings();
        settings.Port = 2525;
        settings.SecureSocketOptions = mode;

        Valid(settings, isDevelopment: true);
    }

    [Theory]
    [InlineData("basura")]
    [InlineData("99")]
    public void ValorInvalido_Lanza_ListandoLosPermitidos(string mode)
    {
        var settings = ValidSettings();
        settings.SecureSocketOptions = mode;

        var ex = Invalid(settings, isDevelopment: true);
        Assert.Contains("StartTls", ex.Message);
    }

    [Theory]
    [InlineData("StartTls", 587)]
    [InlineData("SslOnConnect", 465)]
    public void ModoYPuertoCoherentes_NoLanzan(string mode, int port)
    {
        var settings = ValidSettings();
        settings.SecureSocketOptions = mode;
        settings.Port = port;

        Valid(settings);
    }

    [Theory]
    [InlineData("StartTls", 465)]
    [InlineData("SslOnConnect", 587)]
    [InlineData("SslOnConnect", 25)]
    public void ModoYPuertoQueCuelganLaConexion_Lanzan(string mode, int port)
    {
        var settings = ValidSettings();
        settings.SecureSocketOptions = mode;
        settings.Port = port;

        Assert.Contains("Smtp:Port", Invalid(settings).Message);
    }
}
