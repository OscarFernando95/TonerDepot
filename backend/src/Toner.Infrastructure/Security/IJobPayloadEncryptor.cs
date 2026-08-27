namespace Toner.Infrastructure.Security;

// Cifra un valor sensible antes de que cruce a un almacenamiento durable que Application no controla
// (los argumentos de un job de Hangfire, persistidos en Postgres — ver HangfireBackgroundJobScheduler
// y SendGeneratedPasswordEmailJob). No es un concepto que Application necesite conocer: es un detalle
// de cómo Infrastructure protege el tránsito hacia Hangfire, no una decisión de negocio.
public interface IJobPayloadEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
