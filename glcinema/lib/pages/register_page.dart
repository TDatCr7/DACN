import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  // ================== CONFIG ==================
  // PHẢI TRÙNG với LoginPage
  static const String apiBase = 'http://10.0.2.2:5080';
  // ===========================================

  final _emailController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _otpController = TextEditingController();
  final _passwordController = TextEditingController();

  bool _otpSent = false;
  bool _otpVerified = false;
  bool _loading = false;
  bool _obscure = true;

  Map<String, String> get _headers => const {
    'Content-Type': 'application/json; charset=utf-8',
    'Accept': 'application/json',
  };

  void _showMessage(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(msg, style: const TextStyle(fontSize: 16)),
        backgroundColor: Colors.deepPurpleAccent,
      ),
    );
  }

  String _readMessage(String body, {String fallback = 'Thao tác thất bại'}) {
    try {
      final m = jsonDecode(body);
      if (m is Map && m['message'] != null) return m['message'].toString();
      if (m is Map && m['errors'] != null) return m['errors'].toString();
    } catch (_) {}
    return fallback;
  }

  Future<void> _sendOtp() async {
    final email = _emailController.text.trim();
    final fullName = _fullNameController.text.trim();

    if (email.isEmpty) {
      _showMessage('Vui lòng nhập email');
      return;
    }
    if (fullName.isEmpty) {
      _showMessage('Vui lòng nhập họ và tên');
      return;
    }

    setState(() => _loading = true);
    try {
      final uri = Uri.parse('$apiBase/api/send-otp');
      final r = await http
          .post(
        uri,
        headers: _headers,
        body: jsonEncode({'email': email}),
      )
          .timeout(const Duration(seconds: 20));

      debugPrint('SEND_OTP url=$uri status=${r.statusCode} body=${r.body}');

      final msg = (r.statusCode == 200)
          ? _readMessage(r.body, fallback: 'Đã gửi mã OTP')
          : _readMessage(r.body, fallback: 'Gửi OTP thất bại');

      setState(() {
        _otpSent = r.statusCode == 200;
        if (!_otpSent) _otpVerified = false;
      });

      _showMessage(msg);
    } catch (e) {
      debugPrint('SEND_OTP exception=$e');
      _showMessage('Không kết nối được backend');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _verifyOtp() async {
    final email = _emailController.text.trim();
    final otp = _otpController.text.trim();

    if (email.isEmpty || otp.isEmpty) {
      _showMessage('Vui lòng nhập đủ email và mã OTP');
      return;
    }

    setState(() => _loading = true);
    try {
      final uri = Uri.parse('$apiBase/api/verify-otp');
      final r = await http
          .post(
        uri,
        headers: _headers,
        body: jsonEncode({'email': email, 'otp': otp}),
      )
          .timeout(const Duration(seconds: 20));

      debugPrint('VERIFY_OTP url=$uri status=${r.statusCode} body=${r.body}');

      final msg = (r.statusCode == 200)
          ? _readMessage(r.body, fallback: 'OTP hợp lệ')
          : _readMessage(r.body, fallback: 'OTP không đúng');

      setState(() {
        _otpVerified =
            (r.statusCode == 200) && msg.toLowerCase().contains('otp hợp lệ');
      });

      _showMessage(msg);
    } catch (e) {
      debugPrint('VERIFY_OTP exception=$e');
      _showMessage('Không kết nối được backend');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _register() async {
    final email = _emailController.text.trim();
    final fullName = _fullNameController.text.trim();
    final password = _passwordController.text.trim();

    if (!_otpVerified) {
      _showMessage('Vui lòng xác thực OTP trước khi đăng ký');
      return;
    }
    if (fullName.isEmpty) {
      _showMessage('Vui lòng nhập họ và tên');
      return;
    }
    if (password.isEmpty) {
      _showMessage('Vui lòng nhập mật khẩu');
      return;
    }

    setState(() => _loading = true);
    try {
      final uri = Uri.parse('$apiBase/api/register');
      final r = await http
          .post(
        uri,
        headers: _headers,
        body: jsonEncode(
          {'email': email, 'password': password, 'fullName': fullName},
        ),
      )
          .timeout(const Duration(seconds: 20));

      debugPrint('REGISTER url=$uri status=${r.statusCode} body=${r.body}');

      final msg = (r.statusCode == 200)
          ? _readMessage(r.body, fallback: 'Đăng ký thành công')
          : _readMessage(r.body, fallback: 'Đăng ký thất bại');

      _showMessage(msg);

      if (!mounted) return;
      if (r.statusCode == 200 && msg.toLowerCase().contains('thành công')) {
        Navigator.pushReplacementNamed(context, '/login');
      }
    } catch (e) {
      debugPrint('REGISTER exception=$e');
      _showMessage('Không kết nối được backend');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _emailController.dispose();
    _fullNameController.dispose();
    _otpController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0B0F1A),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 40),
          child: Container(
            padding: const EdgeInsets.all(28),
            decoration: BoxDecoration(
              color: const Color(0xFF161B2E),
              borderRadius: BorderRadius.circular(25),
              boxShadow: [
                BoxShadow(
                  color: Colors.deepPurpleAccent.withOpacity(0.4),
                  blurRadius: 25,
                  offset: const Offset(0, 8),
                ),
              ],
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.movie_outlined,
                    size: 70, color: Colors.deepPurpleAccent),
                const SizedBox(height: 16),
                const Text(
                  'Đăng ký thành viên 🎟️',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 26,
                    fontWeight: FontWeight.bold,
                    letterSpacing: 1,
                  ),
                ),
                const SizedBox(height: 30),

                _buildInputField(
                  controller: _emailController,
                  label: 'Email',
                  icon: Icons.email_outlined,
                ),
                const SizedBox(height: 15),

                _buildInputField(
                  controller: _fullNameController,
                  label: 'Họ và tên',
                  icon: Icons.person_outline,
                ),
                const SizedBox(height: 15),

                _buildNeonButton(
                  text: _loading ? 'Đang gửi...' : 'Gửi mã OTP',
                  onPressed: _loading ? null : _sendOtp,
                ),

                if (_otpSent) ...[
                  const SizedBox(height: 20),
                  _buildInputField(
                    controller: _otpController,
                    label: 'Nhập mã OTP',
                    icon: Icons.verified_outlined,
                  ),
                  const SizedBox(height: 15),
                  _buildNeonButton(
                    text: _loading ? 'Đang xác minh...' : 'Xác minh OTP',
                    onPressed: _loading ? null : _verifyOtp,
                  ),
                ],

                if (_otpVerified) ...[
                  const SizedBox(height: 25),
                  _buildInputField(
                    controller: _passwordController,
                    label: 'Mật khẩu',
                    icon: Icons.lock_outline,
                    obscureText: _obscure,
                    suffix: IconButton(
                      icon: Icon(
                        _obscure ? Icons.visibility_off : Icons.visibility,
                        color: Colors.white70,
                      ),
                      onPressed: () => setState(() => _obscure = !_obscure),
                    ),
                  ),
                  const SizedBox(height: 20),
                  _buildNeonButton(
                    text: _loading ? 'Đang tạo...' : 'Tạo tài khoản',
                    onPressed: _loading ? null : _register,
                  ),
                ],

                const SizedBox(height: 30),
                TextButton(
                  onPressed: () =>
                      Navigator.pushReplacementNamed(context, '/login'),
                  child: const Text(
                    'Đã có tài khoản? Đăng nhập ngay',
                    style: TextStyle(color: Colors.deepPurpleAccent),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildInputField({
    required TextEditingController controller,
    required String label,
    required IconData icon,
    bool obscureText = false,
    Widget? suffix,
  }) {
    return TextField(
      controller: controller,
      obscureText: obscureText,
      style: const TextStyle(color: Colors.white),
      decoration: InputDecoration(
        prefixIcon: Icon(icon, color: Colors.deepPurpleAccent),
        suffixIcon: suffix,
        labelText: label,
        labelStyle: const TextStyle(color: Colors.white70),
        filled: true,
        fillColor: Colors.white10,
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(15),
          borderSide: const BorderSide(color: Colors.white24),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(15),
          borderSide:
          const BorderSide(color: Colors.deepPurpleAccent, width: 1.5),
        ),
      ),
    );
  }

  Widget _buildNeonButton({
    required String text,
    required VoidCallback? onPressed,
  }) {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
            colors: [Colors.deepPurpleAccent, Colors.purple]),
        borderRadius: BorderRadius.circular(15),
        boxShadow: [
          BoxShadow(
            color: Colors.deepPurpleAccent.withOpacity(0.5),
            blurRadius: 15,
            offset: const Offset(0, 5),
          ),
        ],
      ),
      child: ElevatedButton(
        onPressed: onPressed,
        style: ElevatedButton.styleFrom(
          backgroundColor: Colors.transparent,
          shadowColor: Colors.transparent,
          elevation: 0,
          shape:
          RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
          padding: const EdgeInsets.symmetric(vertical: 14),
        ),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.bold,
            color: Colors.white,
            letterSpacing: 0.5,
          ),
        ),
      ),
    );
  }
}
