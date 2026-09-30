import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../models/client.dart';
import '../../services/api_client.dart';
import '../../services/client_api.dart';
import '../../services/contract_api.dart';
import '../../theme/app_theme.dart';
import '../../utils/date_only.dart';
import '../../widgets/clay_date_field.dart';

/// Espejo de ContractsListView.vue (diálogo "Nuevo contrato") — el botón `+`
/// junto al selector de cliente abre CreateClientDialog ahí; acá reusa el
/// mismo patrón `context.push<Client>('/clients/new')` que ya usa
/// ticket_create_sheet.dart, para no forzar al usuario a salir del flujo.
class ContractCreateScreen extends StatefulWidget {
  const ContractCreateScreen({super.key});

  @override
  State<ContractCreateScreen> createState() => _ContractCreateScreenState();
}

class _ContractCreateScreenState extends State<ContractCreateScreen> {
  final _printsController = TextEditingController();
  final _priceController = TextEditingController();
  final _notesController = TextEditingController();
  bool _loadingClients = true;
  bool _submitting = false;
  String? _loadError;
  List<Client> _clients = [];
  String? _clientId;
  DateTime _startDate = DateTime.now();
  DateTime? _endDate;

  @override
  void initState() {
    super.initState();
    _loadClients();
  }

  Future<void> _loadClients() async {
    try {
      final clients = await ClientApi(ApiClient.instance).list();
      setState(() {
        _clients = clients;
        _loadingClients = false;
      });
    } catch (e) {
      setState(() {
        _loadingClients = false;
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar los clientes.';
      });
    }
  }

  @override
  void dispose() {
    _printsController.dispose();
    _priceController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _createClient() async {
    final created = await context.push<Client>('/clients/new');
    if (created != null && mounted) {
      setState(() {
        _clients = [..._clients, created];
        _clientId = created.id;
      });
    }
  }

  bool get _isValid => _clientId != null;

  Future<void> _pickStartDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _startDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
    );
    if (picked != null) setState(() => _startDate = picked);
  }

  Future<void> _pickEndDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _endDate ?? _startDate,
      firstDate: _startDate,
      lastDate: DateTime(2100),
    );
    if (picked != null) setState(() => _endDate = picked);
  }

  Future<void> _submit() async {
    if (!_isValid) return;
    setState(() => _submitting = true);
    try {
      await ContractApi(ApiClient.instance).create(
        clientId: _clientId!,
        startDate: formatDateOnly(_startDate),
        endDate: _endDate == null ? null : formatDateOnly(_endDate!),
        includedPrintsPerMonth: int.tryParse(_printsController.text.trim()),
        pricePerExtraPage: double.tryParse(_priceController.text.trim()),
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear el contrato.',
            ),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Nuevo contrato')),
      body: _loadingClients
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
          ? Center(
              child: Text(
                _loadError!,
                style: const TextStyle(color: AppColors.signalRed),
              ),
            )
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: DropdownButtonFormField<String>(
                        initialValue: _clientId,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Cliente *',
                        ),
                        items: [
                          for (final c in _clients)
                            DropdownMenuItem(
                              value: c.id,
                              child: Text(
                                c.name,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                        ],
                        onChanged: (value) =>
                            setState(() => _clientId = value),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: IconButton.outlined(
                        onPressed: _createClient,
                        icon: const Icon(Icons.add),
                        tooltip: 'Crear cliente',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                ClayDateField(
                  label: 'Fecha de inicio',
                  value: formatDateOnly(_startDate),
                  onTap: _pickStartDate,
                ),
                const SizedBox(height: 12),
                ClayDateField(
                  label: 'Fecha de fin (opcional)',
                  value: _endDate == null ? 'Sin definir' : formatDateOnly(_endDate!),
                  trailing: _endDate == null
                      ? const Icon(Icons.calendar_today_outlined, size: 18)
                      : IconButton(
                          icon: const Icon(Icons.clear, size: 18),
                          onPressed: () => setState(() => _endDate = null),
                        ),
                  onTap: _pickEndDate,
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _printsController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    labelText: 'Impresiones incluidas / mes (opcional)',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _priceController,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Precio por página extra (opcional)',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _notesController,
                  minLines: 2,
                  maxLines: 4,
                  decoration: const InputDecoration(
                    labelText: 'Notas (opcional)',
                  ),
                ),
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: FilledButton(
                    onPressed: _isValid && !_submitting ? _submit : null,
                    child: _submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text(
                            'Crear contrato',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ],
            ),
    );
  }
}
