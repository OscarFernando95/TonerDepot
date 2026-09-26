import 'package:flutter/material.dart';

import '../services/api_client.dart';
import '../theme/app_theme.dart';

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
    if (url.isEmpty) return;
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
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'URL base de la API (incluye /api al final). '
              'En el emulador de Android usa 10.0.2.2 en vez de localhost. '
              'En un celular físico, usa la IP de tu PC en la red local.',
              style: TextStyle(color: AppColors.flapInkDim),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _controller,
              keyboardType: TextInputType.url,
              decoration: const InputDecoration(
                labelText: 'URL de la API',
                hintText: 'http://192.168.1.50:5250/api',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              child: FilledButton(onPressed: _save, child: const Text('Guardar')),
            ),
          ],
        ),
      ),
    );
  }
}
