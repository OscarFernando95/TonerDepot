namespace Toner.Domain.Enums;

// Origen de un vínculo técnico-activo. Existe para poder REVOCAR solo lo que el sistema concedió solo: un
// vínculo automático por zona se retira cuando el técnico deja de cubrir ese activo, mientras que uno hecho a
// mano por un administrador (o por la instalación que el técnico confirmó) permanece hasta que alguien lo quite.
public enum TechnicianAssetSource
{
    // Hecho a mano por un administrador, o consecuencia de que el técnico instaló el equipo. Nunca se revoca
    // por cambios de zona. Es el valor de todo vínculo anterior a esta columna.
    Manual = 0,

    // Derivado de la cobertura: el activo está Instalado en un municipio de una zona asignada al técnico.
    // Se crea y se retira solo al cambiar zonas, municipios o la sede/estado del activo.
    Zone = 1
}
