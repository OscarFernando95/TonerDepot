namespace Toner.Domain.Enums;

public enum AssetLifecycleStatus
{
    EnBodega = 0,
    Instalado = 1,
    EnMantenimiento = 2,
    DadoDeBaja = 3,

    // Estado intermedio: se alcanza automáticamente al vincular el activo a un contrato (ver
    // ContractAssetService.AddAsync), nunca es una opción manual del selector de estado en el frontend.
    PendienteInstalacion = 4
}
