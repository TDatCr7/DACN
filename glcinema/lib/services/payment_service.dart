// services/payment_service.dart
import 'dart:convert';
import 'package:http/http.dart' as http;

class PaymentService {
  static const String baseUrl = 'http://10.0.2.2:5080/api';

  static Future<Map<String, dynamic>> createVNPay({
    required String userId,                  // 👈 int
    required String email,
    required String showtimeId,           // text/varchar ở server
    required List<String> seatIds,        // text/varchar ở server
    List<Map<String, dynamic>> snacks = const [],
    required String movieTitle,
  }) async {
    final body = {
      'userId': userId,                   // 👈 giữ nguyên int, KHÔNG .toString()
      'email': email,
      'showtimeId': showtimeId,           // nếu bạn đang giữ là int => .toString()
      'seatIds': seatIds,                 // nếu đang giữ là int => map((e)=>'$e').toList()
      'snacks': snacks,
      'movieTitle': movieTitle,
    };

    final res = await http.post(
      Uri.parse('$baseUrl/vnpay/create'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );

    if (res.statusCode != 200) {
      throw Exception('Tạo thanh toán thất bại (${res.statusCode}): ${res.body}');
    }
    return jsonDecode(res.body) as Map<String, dynamic>;
  }
}
