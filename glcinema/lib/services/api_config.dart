// lib/services/api_config.dart
class ApiConfig {
  // Host gốc (KHÔNG có /api)
  static const String host = 'http://10.0.2.2:5080';

  // Base API (CÓ /api)
  static const String apiBase = '$host/api';

  // Ghép URL ảnh/file ("/uploads/.." hoặc "uploads/..")
  static String fileUrl(String raw) {
    final s = (raw).trim();
    if (s.isEmpty) return '';
    if (s.startsWith('http://') || s.startsWith('https://')) return s;

    final p = s.startsWith('/') ? s : '/$s';
    return '$host$p';
  }
}
