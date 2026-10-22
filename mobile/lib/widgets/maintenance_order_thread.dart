import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/maintenance_order_comment.dart';
import '../models/maintenance_thread_text.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../state/maintenance_order_thread_state.dart';
import '../theme/app_theme.dart';

/// Hoja inferior con el hilo compartido de una orden de mantenimiento (cliente, técnico y staff): lista
/// cronológica de comentarios y programaciones, y una caja para responder. Se refresca sola con el tiempo real
/// de `MaintenanceOrder`. Con [readOnly] (orden Completada/Cancelada, o técnico con la visita en curso) solo se lee.
class MaintenanceOrderThreadSheet extends StatelessWidget {
  const MaintenanceOrderThreadSheet({
    super.key,
    required this.orderId,
    required this.title,
    this.readOnly = false,
    this.readOnlyNote,
    this.helpText,
  });

  final String orderId;

  /// Equipo al que pertenece la orden, p. ej. "HP M404".
  final String title;
  final bool readOnly;

  /// Por qué es de solo lectura; por defecto, que la orden está cerrada.
  final String? readOnlyNote;

  /// Ayuda bajo el título (p. ej. para el cliente: "Si la fecha no te sirve, cuéntanos y reprogramamos").
  final String? helpText;

  static Future<void> show(
    BuildContext context, {
    required String orderId,
    required String title,
    bool readOnly = false,
    String? readOnlyNote,
    String? helpText,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) {
        final media = MediaQuery.of(sheetContext);
        return Padding(
          padding: EdgeInsets.only(bottom: media.viewInsets.bottom),
          child: SizedBox(
            height: (media.size.height - media.viewInsets.bottom) * 0.85,
            child: MaintenanceOrderThreadSheet(
              orderId: orderId,
              title: title,
              readOnly: readOnly,
              readOnlyNote: readOnlyNote,
              helpText: helpText,
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MaintenanceOrderThreadState(
        MaintenanceOrderApi(ApiClient.instance),
        orderId,
      )..load(),
      child: _ThreadBody(
        title: title,
        readOnly: readOnly,
        readOnlyNote: readOnlyNote,
        helpText: helpText,
      ),
    );
  }
}

class _ThreadBody extends StatefulWidget {
  const _ThreadBody({
    required this.title,
    required this.readOnly,
    required this.readOnlyNote,
    required this.helpText,
  });

  final String title;
  final bool readOnly;
  final String? readOnlyNote;
  final String? helpText;

  @override
  State<_ThreadBody> createState() => _ThreadBodyState();
}

class _ThreadBodyState extends State<_ThreadBody> {
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  int _lastCount = 0;

  @override
  void dispose() {
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _scrollToEndIfGrew(int count) {
    if (count == _lastCount) return;
    _lastCount = count;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_scroll.hasClients) return;
      _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  Future<void> _send(MaintenanceOrderThreadState state) async {
    final ok = await state.send(_controller.text);
    if (ok && mounted) _controller.clear();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<MaintenanceOrderThreadState>();
    _scrollToEndIfGrew(state.comments.length);
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Comentarios',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              IconButton(
                tooltip: 'Cerrar',
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(context).maybePop(),
              ),
            ],
          ),
          Text(
            widget.title,
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          if (widget.helpText != null) ...[
            const SizedBox(height: 6),
            Text(widget.helpText!, style: const TextStyle(fontSize: 13)),
          ],
          const Divider(height: 20, color: AppColors.neutralSoft),
          Expanded(child: _buildList(state)),
          const SizedBox(height: 8),
          if (widget.readOnly)
            Text(
              widget.readOnlyNote ??
                  'La orden ya está cerrada: el hilo es de solo lectura.',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            )
          else
            _buildComposer(state),
        ],
      ),
    );
  }

  Widget _buildList(MaintenanceOrderThreadState state) {
    if (state.loading && state.comments.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (state.loadError != null && state.comments.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              state.loadError!,
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.signalRed),
            ),
            TextButton(onPressed: state.load, child: const Text('Reintentar')),
          ],
        ),
      );
    }
    if (state.comments.isEmpty) {
      return const Center(
        child: Text(
          'Aún no hay comentarios.',
          style: TextStyle(color: AppColors.inkSecondary),
        ),
      );
    }
    final flags = state.rescheduleFlagsList;
    return ListView.builder(
      controller: _scroll,
      itemCount: state.comments.length,
      itemBuilder: (context, i) {
        final entry = state.comments[i];
        return entry.isSchedule
            ? _ScheduleEvent(entry: entry, isReschedule: flags[i])
            : _CommentBubble(entry: entry);
      },
    );
  }

  Widget _buildComposer(MaintenanceOrderThreadState state) {
    final canSend = canSendComment(_controller.text) && !state.sending;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.sendError != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: Text(
              state.sendError!,
              style: const TextStyle(color: AppColors.signalRed, fontSize: 12),
            ),
          ),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: TextField(
                controller: _controller,
                minLines: 1,
                maxLines: 4,
                maxLength: maintenanceCommentMaxLength,
                textCapitalization: TextCapitalization.sentences,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  hintText: 'Escribe un comentario…',
                ),
              ),
            ),
            const SizedBox(width: 8),
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: FilledButton(
                onPressed: canSend ? () => _send(state) : null,
                child: state.sending
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Enviar'),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

/// "Juan programó el mantenimiento para el jue 30 oct, 10:00" — un evento del hilo, no una burbuja.
class _ScheduleEvent extends StatelessWidget {
  const _ScheduleEvent({required this.entry, required this.isReschedule});

  final MaintenanceOrderComment entry;
  final bool isReschedule;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.event_available_outlined,
            size: 18,
            color: AppColors.signalBlue,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  scheduleEventText(entry, isReschedule: isReschedule),
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 13,
                  ),
                ),
                Text(
                  formatDateTimeEsCo(entry.createdAt),
                  style: const TextStyle(
                    color: AppColors.inkSecondary,
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Comentario con autor, rol legible y fecha. Los del cliente se destacan (fondo azul y el rol siempre en texto,
/// nunca solo color).
class _CommentBubble extends StatelessWidget {
  const _CommentBubble({required this.entry});

  final MaintenanceOrderComment entry;

  @override
  Widget build(BuildContext context) {
    final fromClient = entry.isFromClient;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: fromClient
              ? AppColors.signalBlueWash
              : AppColors.claySurfaceRaised,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: fromClient ? AppColors.signalBlue : AppColors.neutralSoft,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${entry.authorName} · ${maintenanceRoleLabel(entry.authorRole)}',
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 12,
                color: fromClient
                    ? AppColors.signalBlue
                    : AppColors.inkSecondary,
              ),
            ),
            const SizedBox(height: 2),
            Text(entry.body),
            const SizedBox(height: 4),
            Text(
              formatDateTimeEsCo(entry.createdAt),
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 11,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
