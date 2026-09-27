import 'package:flutter/material.dart';

import '../services/api_client.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';

/// Pantalla accesible desde el login para apuntar la app a otra URL de API
/// sin recompilar — necesario porque la URL correcta depende de dónde corre
/// la app (emulador Android, simulador iOS, o un celular físico en la misma
/// red que el PC con la API). Ver comentario en AppConfig.
class ServerSettingsScreen extends StatefulWidget {
  const ServerSettingsScreen({super.key});

  @override
  State<ServerSettingsScreen> createState() => _ServerSettingsScreenState();
}

class _ServerSettingsScreenState extends State<ServerSettingsScreen> {
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController();
    ApiClient.instance.baseUrl.then((url) => setState(() => _controller.text = url));
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final url = _controller.text.trim();
    final uri = Uri.tryParse(url);
    final isValid = url.isNotEmpty &&
        uri != null &&
        uri.isAbsolute &&
        (uri.scheme == 'http' || uri.scheme == 'https') &&
        uri.host.isNotEmpty;
    if (!isValid) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('URL inválida. Debe empezar con http:// o https://')),
      );
      return;
    }
    await ApiClient.instance.setBaseUrl(url);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('URL guardada.')));
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Servidor')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: ClaySurface(
              radius: 26,
              padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Center(
                    child: Container(
                      width: 60,
                      height: 60,
                      decoration: const BoxDecoration(shape: BoxShape.circle, color: AppColors.signalBlueWash),
                      child: const Icon(Icons.dns_outlined, size: 28, color: AppColors.signalBlue),
                    ),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'URL base de la API (incluye /api al final). '
                    'En el emulador de Android usa 10.0.2.2 en vez de localhost. '
                    'En un celular físico, usa la IP de tu PC en la red local.',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                  const SizedBox(height: 20),
                  TextField(
                    controller: _controller,
                    keyboardType: TextInputType.url,
                    decoration: const InputDecoration(
                      labelText: 'URL de la API',
                      hintText: 'http://192.168.1.50:5250/api',
                    ),
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    height: 52,
                    child: FilledButton(
                      onPressed: _save,
                      child: const Text('Guardar', style: TextStyle(fontWeight: FontWeight.w700)),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
