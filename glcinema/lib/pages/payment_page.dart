// lib/pages/payment_page.dart
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../services/env.dart';
import 'vnpay_webview_page.dart';
import 'ticket_success_page.dart';

class PaymentPage extends StatefulWidget {
  static const routeName = '/payment';

  final String movieTitle;
  final String showtimeId;
  final List<String> selectedSeatIds;
  final List<Map<String, dynamic>> snacks;
  final int grandTotal; // fallback

  const PaymentPage({
    super.key,
    required this.movieTitle,
    required this.showtimeId,
    required this.selectedSeatIds,
    required this.snacks,
    required this.grandTotal,
  });

  @override
  State<PaymentPage> createState() => _PaymentPageState();
}

class _PaymentPageState extends State<PaymentPage> {
  bool _loading = true;
  bool _creating = false;

  /// seatId -> price/label
  final Map<String, int> _seatPrice = {};
  final Map<String, String> _seatLabel = {};

  // ---------------- helpers ----------------
  int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v) ?? 0;
    return 0;
  }

  String _fmtInt(int v) {
    final s = v.toString();
    final re = RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))');
    return s.replaceAllMapped(re, (m) => '${m[1]}.');
  }

  int get _seatTotal {
    int total = 0;
    for (final id in widget.selectedSeatIds) {
      total += _seatPrice[id] ?? 0;
    }
    return total;
  }

  int get _snackTotal {
    return widget.snacks.fold<int>(0, (p, s) {
      final price = _asInt(s['price']);
      final qty = _asInt(s['quantity'] ?? s['qty']);
      return p + price * qty;
    });
  }

  int get _grandTotal => _seatTotal + _snackTotal;

  // -------------- lifecycle --------------
  @override
  void initState() {
    super.initState();
    _fetchSeatPrices();
  }

  Future<void> _fetchSeatPrices() async {
    try {
      setState(() => _loading = true);
      final url = Uri.parse('${Env.baseUrl}/api/showtimes/${widget.showtimeId}/seats');
      final res = await http.get(url);
      if (res.statusCode != 200) {
        throw Exception('HTTP ${res.statusCode}');
      }
      final data = jsonDecode(res.body);

      final list = (data is Map && data['seats'] is List)
          ? (data['seats'] as List)
          : (data as List);

      _seatPrice.clear();
      _seatLabel.clear();
      for (final raw in list) {
        final m = Map<String, dynamic>.from(raw);
        final seatId = (m['seatId'] ?? m['Seat_ID']).toString();
        final row = (m['rowLabel'] ?? m['Row_Label'] ?? '').toString();
        final col = _asInt(m['colIndex'] ?? m['Col_Index']);
        final price = _asInt(m['basePrice'] ?? m['Base_Price']);
        _seatPrice[seatId] = price;
        _seatLabel[seatId] = '$row${col > 0 ? col : ''}';
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Lỗi tải giá ghế: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  // ---------------- payment ----------------
  Future<void> _payVNPay() async {
    setState(() => _creating = true);
    try {
      final prefs = await SharedPreferences.getInstance();

      // ✅ đọc UserId dạng STRING (vd: "U001")
      final String userIdStr = (prefs.getString('userId') ??
          prefs.getString('user_id') ??
          '')
          .trim();

      final email = (prefs.getString('email') ?? '').trim();

      if (userIdStr.isEmpty) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Thiếu userId (chưa đăng nhập hoặc chưa lưu userId đúng dạng "Uxxx").')),
        );
        return;
      }

      // nếu email trống thì vẫn gửi null/"" cũng được (backend không bắt buộc)
      final body = {
        'userId': userIdStr, // ✅ GỬI STRING
        'email': email.isEmpty ? null : email,
        'showtimeId': widget.showtimeId,
        'seatIds': widget.selectedSeatIds,
        'snacks': widget.snacks.map((s) {
          return {
            // backend CreateVnpayRequest expects: snackId + quantity
            'snackId': (s['snackId'] ?? s['id'] ?? '').toString(),
            'quantity': _asInt(s['quantity'] ?? s['qty']),
          };
        }).toList(),
        'movieTitle': widget.movieTitle,
      };

      final res = await http.post(
        Uri.parse('${Env.baseUrl}/api/vnpay/create'),
        headers: {'Content-Type': 'application/json'},
        body: json.encode(body),
      );

      if (res.statusCode != 200) {
        // hiện lỗi body để biết userId nào đang gửi
        throw Exception('HTTP ${res.statusCode}: ${res.body}');
      }

      final data = json.decode(res.body);
      if (data is! Map || data['paymentUrl'] == null) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Phản hồi VNPay không hợp lệ')),
        );
        return;
      }

      final payUrl = (data['paymentUrl'] ?? '').toString();
      final orderId = (data['orderId'] ?? data['invoiceId'] ?? '').toString();

      final result = await Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => VNPayWebViewPage(
            userId: null,
            orderId: orderId,
            paymentUrl: payUrl,
          ),
        ),
      );

      if (!mounted) return;

      if (result is Map && result['success'] == true) {
        Navigator.pushReplacement(
          context,
          MaterialPageRoute(
            builder: (_) => TicketSuccessPage(
              orderId: orderId,
              movieTitle: widget.movieTitle,
            ),
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              result is Map && result['error'] != null
                  ? 'Thanh toán không thành công: ${result['error']}'
                  : 'Thanh toán không thành công',
            ),
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Lỗi tạo thanh toán: $e')),
      );
    } finally {
      if (mounted) setState(() => _creating = false);
    }
  }

  // ---------------- UI atoms ----------------
  Widget _sectionHeader(String title) => Container(
    width: double.infinity,
    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
    color: const Color(0xFFEEEEEE),
    child: Text(
      title,
      style: const TextStyle(
        fontWeight: FontWeight.w700,
        color: Color(0xFF333333),
      ),
    ),
  );

  Widget _chipSeat(String label, int price) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFBDBDBD)),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        children: [
          Text(label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16)),
          const SizedBox(height: 4),
          Text(
            _fmtInt(price),
            style: const TextStyle(fontSize: 12, color: Colors.black54),
          ),
        ],
      ),
    );
  }

  Widget _rowKV(String k, String v, {FontWeight fw = FontWeight.w500, Color? color}) =>
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 16),
        child: Row(
          children: [
            Expanded(
                child: Text(k,
                    style:
                    const TextStyle(color: Colors.black87, fontWeight: FontWeight.w500))),
            Text(
              v,
              style: TextStyle(color: color ?? Colors.black87, fontWeight: fw),
            ),
          ],
        ),
      );

  // ---------------- build ----------------
  @override
  Widget build(BuildContext context) {
    final seatChips = widget.selectedSeatIds.map((id) {
      final label = _seatLabel[id] ?? id;
      final price = _seatPrice[id] ?? 0;
      return _chipSeat(label, price);
    }).toList();

    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF6A1B9A),
        title: const Text('Thanh toán'),
        centerTitle: true,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: Color(0xFF6A1B9A)))
          : Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                    child: Text(
                      widget.movieTitle,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  _sectionHeader('THÔNG TIN VÉ'),
                  Padding(
                    padding: const EdgeInsets.all(16),
                    child: Wrap(
                      spacing: 10,
                      runSpacing: 10,
                      children: seatChips,
                    ),
                  ),
                  _rowKV('SỐ LƯỢNG', '${widget.selectedSeatIds.length}',
                      fw: FontWeight.w700),
                  const Divider(height: 1),
                  _rowKV('Tổng', '${_fmtInt(_seatTotal)} đ', fw: FontWeight.w700),
                  const SizedBox(height: 10),
                  _sectionHeader('THÔNG TIN BẮP NƯỚC'),
                  if (widget.snacks.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text('Không chọn bắp nước',
                          style: TextStyle(color: Colors.black54)),
                    )
                  else
                    Column(
                      children: [
                        ...widget.snacks.map((s) {
                          final name = (s['name'] ?? 'Combo').toString();
                          final qty = _asInt(s['quantity'] ?? s['qty']);
                          final price = _asInt(s['price']);
                          final line = price * qty;
                          return _rowKV('$name x$qty', '${_fmtInt(line)} đ');
                        }),
                        const Divider(height: 1),
                        _rowKV('Tổng', '${_fmtInt(_snackTotal)} đ',
                            fw: FontWeight.w700),
                      ],
                    ),
                  const SizedBox(height: 12),
                  _sectionHeader('THANH TOÁN'),
                  _rowKV('Tổng cộng', '${_fmtInt(_grandTotal)} đ',
                      fw: FontWeight.w700),
                  const SizedBox(height: 100),
                ],
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: SizedBox(
                width: double.infinity,
                height: 50,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF6A1B9A),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    elevation: 0,
                  ),
                  onPressed: _creating ? null : _payVNPay,
                  icon: _creating
                      ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                      : const Icon(Icons.payment, color: Colors.white),
                  label: Text(
                    _creating ? 'Đang tạo phiên VNPay...' : 'THANH TOÁN VNPAY',
                    style: const TextStyle(
                        color: Colors.white, fontWeight: FontWeight.bold),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
