namespace Toner.Application.Evidences;

public class EvidenceOptions
{
    // Si es true, el check-in de un ticket/orden exige la foto "antes" y el check-out que resuelve, la
    // "después". Es el interruptor de la política; el valor por defecto es exigirlas.
    public bool RequirePhotos { get; set; } = true;

    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}

// Almacenamiento de los archivos de evidencia. La base guarda solo la clave (Evidence.FileUrl), no una URL
// firmada: el contenido se sirve por un endpoint autenticado.
public interface IEvidenceStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
