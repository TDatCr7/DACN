import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class AuthService {
  static const String baseUrl = 'http://10.0.2.2:5080/api';
  static const Map<String, String> _headers = {'Content-Type': 'application/json'};

  static Map<String, dynamic> _safeJson(String body) {
    try {
      final v = jsonDecode(body);
      return v is Map<String, dynamic> ? v : <String, dynamic>{};
    } catch (_) {
      return <String, dynamic>{};
    }
  }

  static String _msgFrom(http.Response r, {String fallback = 'Thao tác thất bại'}) {
    final data = _safeJson(r.body);
    final m = (data['message'] ?? '').toString().trim();
    if (m.isNotEmpty) return m;

    // nếu backend trả errors: [...]
    final errors = data['errors'];
    if (errors is List && errors.isNotEmpty) {
      final first = errors.first?.toString().trim();
      if (first != null && first.isNotEmpty) return first;
    }

    return fallback;
  }

  // ---------------- OTP ----------------
  static Future<String> sendOtp(String email) async {
    try {
      final r = await http
          .post(
        Uri.parse('$baseUrl/send-otp'),
        headers: _headers,
        body: jsonEncode({'email': email}),
      )
          .timeout(const Duration(seconds: 20));

      return (r.statusCode == 200)
          ? _msgFrom(r, fallback: 'Đã gửi mã OTP')
          : _msgFrom(r, fallback: 'Gửi OTP thất bại');
    } catch (_) {
      return 'Không thể kết nối đến máy chủ.';
    }
  }

  static Future<String> verifyOtp(String email, String otp) async {
    try {
      final r = await http
          .post(
        Uri.parse('$baseUrl/verify-otp'),
        headers: _headers,
        body: jsonEncode({'email': email, 'otp': otp}),
      )
          .timeout(const Duration(seconds: 20));

      return (r.statusCode == 200)
          ? _msgFrom(r, fallback: 'OTP hợp lệ')
          : _msgFrom(r, fallback: 'OTP không đúng hoặc đã hết hạn');
    } catch (_) {
      return 'Không thể kết nối đến máy chủ.';
    }
  }

  // ---------------- Register ----------------
  static Future<String> register(String email, String password, String fullName) async {
    try {
      final r = await http
          .post(
        Uri.parse('$baseUrl/register'),
        headers: _headers,
        body: jsonEncode({'email': email, 'password': password, 'fullName': fullName}),
      )
          .timeout(const Duration(seconds: 20));

      return (r.statusCode == 200)
          ? _msgFrom(r, fallback: 'Đăng ký thành công')
          : _msgFrom(r, fallback: 'Đăng ký thất bại');
    } catch (_) {
      return 'Không thể kết nối đến máy chủ.';
    }
  }

  // ---------------- Login ----------------
  static Future<Map<String, dynamic>> login(String email, String password) async {
    try {
      final r = await http
          .post(
        Uri.parse('$baseUrl/login'),
        headers: _headers,
        body: jsonEncode({'email': email, 'password': password}),
      )
          .timeout(const Duration(seconds: 20));

      print('MOBILE LOGIN => status=${r.statusCode} body=${r.body}');

      final data = _safeJson(r.body);

      if (r.statusCode != 200) {
        return {
          'ok': false,
          'message': (data['message'] ?? 'Sai email hoặc mật khẩu').toString(),
        };
      }

      final prefs = await SharedPreferences.getInstance();

      await prefs.setString('token', 'session_ok');
      await prefs.setString('email', (data['email'] ?? email).toString());

      // userId là string (vd: "U8DC78E19E")
      final userIdRaw = (data['userId'] ?? '').toString().trim();
      if (userIdRaw.isNotEmpty) {
        await prefs.setString('userId', userIdRaw);

        // IMPORTANT: xóa key cũ kiểu int để tránh đọc nhầm / type mismatch
        await prefs.remove('user_id');

        // nếu vẫn cần "user_id" cho màn khác => lưu cùng giá trị nhưng dưới dạng String
        await prefs.setString('user_id', userIdRaw);
      }

      return {
        'ok': true,
        'message': (data['message'] ?? 'Đăng nhập thành công').toString(),
      };
    } catch (_) {
      return {'ok': false, 'message': 'Không thể kết nối đến máy chủ.'};
    }
  }

  static Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('token');
    await prefs.remove('email');
    await prefs.remove('userId');
    await prefs.remove('user_id');
  }

  static Future<bool> isLoggedIn() async {
    final prefs = await SharedPreferences.getInstance();
    final tokenOk = (prefs.getString('token') ?? '').trim().isNotEmpty;

    final uid = (prefs.getString('userId') ?? '').trim();
    final uid2 = (prefs.getString('user_id') ?? '').trim(); // nếu bạn lưu tương thích

    final userOk = uid.isNotEmpty || uid2.isNotEmpty;
    return tokenOk && userOk;
  }

}
