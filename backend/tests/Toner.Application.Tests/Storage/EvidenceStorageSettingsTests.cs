using Toner.Infrastructure.Storage;

namespace Toner.Application.Tests.Storage;

// AUDIT.md H-18: en Azure, la connection string es la CLAVE de la cuenta (acceso total, secreto a rotar). Con
// ServiceUri + Managed Identity no hay secreto y el permiso se acota con un rol.
public class EvidenceStorageSettingsTests
{
    private const string Azurite =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public void SoloServiceUri_SeAcepta_YApuntaAlContenedor()
    {
        var settings = new EvidenceStorageSettings { ServiceUri = "https://tonerprod.blob.core.windows.net", ContainerName = "evidence" };

        EvidenceStorageSettingsValidator.EnsureValid(settings);
        var container = EvidenceContainerClientFactory.Create(settings);

        Assert.Equal(new Uri("https://tonerprod.blob.core.windows.net/evidence"), container.Uri);
        Assert.Equal("tonerprod", container.AccountName);
        // Sin clave de cuenta no se pueden firmar SAS: confirma que NO se está usando una connection string.
        Assert.False(container.CanGenerateSasUri);
    }

    [Fact]
    public void ServiceUriConBarraFinal_NoDuplicaLaBarra()
    {
        var settings = new EvidenceStorageSettings { ServiceUri = "https://tonerprod.blob.core.windows.net/", ContainerName = "evidence" };

        EvidenceStorageSettingsValidator.EnsureValid(settings);

        Assert.Equal(new Uri("https://tonerprod.blob.core.windows.net/evidence"), EvidenceContainerClientFactory.Create(settings).Uri);
    }

    [Fact]
    public void SoloConnectionString_ComoHoy_ParaAzurite()
    {
        var settings = new EvidenceStorageSettings { ConnectionString = Azurite, ContainerName = "evidence" };

        EvidenceStorageSettingsValidator.EnsureValid(settings);

        Assert.True(EvidenceContainerClientFactory.Create(settings).CanGenerateSasUri);
    }

    [Fact]
    public void NingunaNiAmbas_FallaAlArrancar()
    {
        Assert.Throws<InvalidOperationException>(() => EvidenceStorageSettingsValidator.EnsureValid(new EvidenceStorageSettings()));

        var ex = Assert.Throws<InvalidOperationException>(() => EvidenceStorageSettingsValidator.EnsureValid(
            new EvidenceStorageSettings { ConnectionString = Azurite, ServiceUri = "https://tonerprod.blob.core.windows.net" }));
        Assert.Contains("ServiceUri", ex.Message);
    }

    [Theory]
    [InlineData("http://tonerprod.blob.core.windows.net")]
    [InlineData("tonerprod.blob.core.windows.net")]
    [InlineData("/relativa")]
    public void ServiceUriSinHttps_FallaAlArrancar(string uri)
    {
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceStorageSettingsValidator.EnsureValid(new EvidenceStorageSettings { ServiceUri = uri }));
    }
}
