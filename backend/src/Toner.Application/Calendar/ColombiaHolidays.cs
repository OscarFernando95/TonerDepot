namespace Toner.Application.Calendar;

// Festivos nacionales de Colombia calculados por algoritmo (sin API externa: no depende de un tercero
// ni de conexión). Reglas: (1) fijos; (2) "Ley Emiliani" (Ley 51 de 1983): se trasladan al lunes
// siguiente si no caen en lunes; (3) los de Semana Santa dependen del domingo de Pascua — Jueves y
// Viernes Santo no se trasladan, Ascensión, Corpus Christi y Sagrado Corazón sí.
public static class ColombiaHolidays
{
    public static IReadOnlyDictionary<DateOnly, string> ForYear(int year)
    {
        var holidays = new Dictionary<DateOnly, string>();

        void Add(DateOnly date, string name)
        {
            // Dos festivos pueden coincidir (p. ej. Sagrado Corazón y San Pedro): se conservan ambos nombres.
            holidays[date] = holidays.TryGetValue(date, out var existing) ? $"{existing} / {name}" : name;
        }

        Add(new DateOnly(year, 1, 1), "Año Nuevo");
        Add(new DateOnly(year, 5, 1), "Día del Trabajo");
        Add(new DateOnly(year, 7, 20), "Día de la Independencia");
        Add(new DateOnly(year, 8, 7), "Batalla de Boyacá");
        Add(new DateOnly(year, 12, 8), "Inmaculada Concepción");
        Add(new DateOnly(year, 12, 25), "Navidad");

        Add(NextMonday(new DateOnly(year, 1, 6)), "Reyes Magos");
        Add(NextMonday(new DateOnly(year, 3, 19)), "San José");
        Add(NextMonday(new DateOnly(year, 6, 29)), "San Pedro y San Pablo");
        Add(NextMonday(new DateOnly(year, 8, 15)), "Asunción de la Virgen");
        Add(NextMonday(new DateOnly(year, 10, 12)), "Día de la Raza");
        Add(NextMonday(new DateOnly(year, 11, 1)), "Todos los Santos");
        Add(NextMonday(new DateOnly(year, 11, 11)), "Independencia de Cartagena");

        var easter = EasterSunday(year);
        Add(easter.AddDays(-3), "Jueves Santo");
        Add(easter.AddDays(-2), "Viernes Santo");
        Add(NextMonday(easter.AddDays(39)), "Ascensión del Señor");
        Add(NextMonday(easter.AddDays(60)), "Corpus Christi");
        Add(NextMonday(easter.AddDays(68)), "Sagrado Corazón de Jesús");

        return holidays;
    }

    // Si la fecha ya es lunes se queda; si no, pasa al lunes siguiente.
    public static DateOnly NextMonday(DateOnly date) => date.AddDays((8 - (int)date.DayOfWeek) % 7);

    // Algoritmo gregoriano anónimo (Meeus/Jones/Butcher).
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
