/// Tramo laboral de un técnico. `day`: 0 = domingo ... 6 = sábado. Horas "HH:mm" en la hora local de la empresa.
class WorkInterval {
  WorkInterval({required this.day, required this.start, required this.end});

  int day;
  String start;
  String end;

  factory WorkInterval.fromJson(Map<String, dynamic> json) => WorkInterval(
    day: json['day'] as int,
    start: json['start'] as String,
    end: json['end'] as String,
  );

  Map<String, dynamic> toJson() => {'day': day, 'start': start, 'end': end};
}

class TechnicianSchedule {
  final bool isDefault;
  final String timeZoneId;
  final List<WorkInterval> intervals;

  TechnicianSchedule({
    required this.isDefault,
    required this.timeZoneId,
    required this.intervals,
  });

  factory TechnicianSchedule.fromJson(Map<String, dynamic> json) =>
      TechnicianSchedule(
        isDefault: json['isDefault'] as bool,
        timeZoneId: json['timeZoneId'] as String,
        intervals: (json['intervals'] as List<dynamic>)
            .map((e) => WorkInterval.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class TimeOff {
  final String id;
  final DateTime startsAt;
  final DateTime effectiveEndsAt;
  final String? reason;
  final DateTime? cancelledAt;
  final bool isActive;

  TimeOff({
    required this.id,
    required this.startsAt,
    required this.effectiveEndsAt,
    required this.reason,
    required this.cancelledAt,
    required this.isActive,
  });

  bool get canCancel =>
      cancelledAt == null && effectiveEndsAt.isAfter(DateTime.now());

  String get statusLabel {
    if (cancelledAt != null && !cancelledAt!.isAfter(startsAt)) {
      return 'Anulado';
    }
    if (isActive) return 'En curso';
    if (startsAt.isAfter(DateTime.now())) return 'Programado';
    return cancelledAt != null ? 'Terminado antes' : 'Terminado';
  }

  factory TimeOff.fromJson(Map<String, dynamic> json) => TimeOff(
    id: json['id'] as String,
    startsAt: DateTime.parse(json['startsAt'] as String).toLocal(),
    effectiveEndsAt: DateTime.parse(json['effectiveEndsAt'] as String)
        .toLocal(),
    reason: json['reason'] as String?,
    cancelledAt: json['cancelledAt'] == null
        ? null
        : DateTime.parse(json['cancelledAt'] as String).toLocal(),
    isActive: json['isActive'] as bool,
  );
}

class Holiday {
  final String date; // yyyy-MM-dd
  final String name;
  final String source; // legal | custom
  final bool isWorkingDay;

  Holiday({
    required this.date,
    required this.name,
    required this.source,
    required this.isWorkingDay,
  });

  factory Holiday.fromJson(Map<String, dynamic> json) => Holiday(
    date: json['date'] as String,
    name: json['name'] as String,
    source: json['source'] as String,
    isWorkingDay: json['isWorkingDay'] as bool,
  );
}
