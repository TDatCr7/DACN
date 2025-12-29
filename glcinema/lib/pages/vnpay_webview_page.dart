// lib/pages/vnpay_webview_page.dart (FULL FILE)
// Update: khi status == paid -> tự về trang chủ (clear stack)

import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:url_launcher/url_launcher_string.dart';

import '../services/api_config.dart';
import '../main.dart'; // để dùng AppRoutes

class VNPayWebViewPage extends StatefulWidget {
  final String? userId; // optional
  final String orderId; // invoiceId
  final String paymentUrl;

  const VNPayWebViewPage({
    super.key,
    required this.userId,
    required this.orderId,
    required this.paymentUrl,
  });

  @override
  State<VNPayWebViewPage> createState() => _VNPayWebViewPageState();
}

class _VNPayWebViewPageState extends State<VNPayWebViewPage> {
  bool _opening = true;
  bool _checking = false;
  String _statusText = 'Đang mở VNPay...';
  Timer? _timer;
  int _tick = 0;

  String get _invoiceId => widget.orderId.trim();

  @override
  void initState() {
    super.initState();
    _startFlow();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _startFlow() async {
    final url = widget.paymentUrl.trim();
    if (url.isEmpty || _invoiceId.isEmpty) {
      if (!mounted) return;
      setState(() {
        _opening = false;
        _statusText = 'Thiếu paymentUrl hoặc invoiceId.';
      });
      return;
    }

    final ok = await launchUrlString(url, mode: LaunchMode.externalApplication);

    if (!mounted) return;
    setState(() {
      _opening = false;
      _statusText = ok
          ? 'Đã mở VNPay trong trình duyệt. Đang kiểm tra trạng thái...'
          : 'Không mở được trình duyệt.';
    });

    if (ok) _startPolling();
  }

  void _startPolling() {
    _timer?.cancel();
    _tick = 0;

    _timer = Timer.periodic(const Duration(seconds: 2), (_) async {
      _tick++;
      if (_tick > 60) {
        _timer?.cancel();
        if (!mounted) return;
        setState(() => _statusText = 'Hết thời gian chờ thanh toán.');
        return;
      }
      await _checkStatusOnce();
    });
  }

  void _goHome() {
    if (!mounted) return;
    Navigator.of(context).pushNamedAndRemoveUntil(AppRoutes.home, (r) => false);
  }

  Future<void> _checkStatusOnce() async {
    if (_checking) return;
    _checking = true;

    try {
      final uri = Uri.parse(
        '${ApiConfig.apiBase}/vnpay/status/${Uri.encodeComponent(_invoiceId)}',
      );

      final res = await http.get(uri).timeout(const Duration(seconds: 10));
      if (res.statusCode != 200) return;

      final data = jsonDecode(res.body);
      if (data is! Map) return;

      final status = data['status'];

      // 1 = paid
      if (status == 1 || status == '1') {
        _timer?.cancel();
        _goHome();
        return;
      }

      // 2 = failed
      if (status == 2 || status == '2') {
        _timer?.cancel();
        if (!mounted) return;
        setState(() => _statusText = 'Thanh toán thất bại.');
        return;
      }

      if (!mounted) return;
      setState(() => _statusText = 'Đang chờ thanh toán... (${_tick * 2}s)');
    } catch (_) {
      // bỏ qua lỗi mạng tạm thời
    } finally {
      _checking = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    return WillPopScope(
      onWillPop: () async {
        // người dùng bấm back -> về trang chủ (đúng yêu cầu “quay lại app tự về trang chủ”)
        _goHome();
        return false;
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Thanh toán VNPay'),
          backgroundColor: Colors.purple,
          leading: IconButton(
            icon: const Icon(Icons.close),
            onPressed: _goHome,
          ),
          actions: [
            IconButton(
              tooltip: 'Mở lại trình duyệt',
              onPressed: widget.paymentUrl.trim().isEmpty ? null : _startFlow,
              icon: const Icon(Icons.open_in_browser),
            ),
            IconButton(
              tooltip: 'Kiểm tra ngay',
              onPressed: _invoiceId.isEmpty ? null : _checkStatusOnce,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Mã hóa đơn: $_invoiceId',
                  style: const TextStyle(fontWeight: FontWeight.w900)),
              const SizedBox(height: 12),
              if (_opening) const LinearProgressIndicator(minHeight: 3),
              const SizedBox(height: 12),
              Text(_statusText,
                  style: const TextStyle(fontWeight: FontWeight.w700)),
              const Spacer(),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: _goHome,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF111827),
                  ),
                  child: const Text(
                    'Về trang chủ',
                    style: TextStyle(
                        color: Colors.white, fontWeight: FontWeight.w800),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
