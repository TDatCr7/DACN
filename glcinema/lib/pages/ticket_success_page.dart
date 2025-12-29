// lib/pages/ticket_success_page.dart
import 'dart:convert';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/env.dart';

class TicketSuccessPage extends StatefulWidget {
  final String orderId;
  final String movieTitle; // fallback
  final String? qrData;    // data URL base64 (nếu đã có)

  const TicketSuccessPage({
    super.key,
    required this.orderId,
    required this.movieTitle,
    this.qrData,
  });

  @override
  State<TicketSuccessPage> createState() => _TicketSuccessPageState();
}

class _TicketSuccessPageState extends State<TicketSuccessPage> {
  bool _loading = true;
  String? _err;

  // data từ API
  String _movieTitle = '';
  String _cinemaName = '';
  String _screenName = '';
  DateTime? _startTime;
  List<String> _seats = [];
  int _total = 0;

  Uint8List? _qrBytes;

  @override
  void initState() {
    super.initState();
    _startLoad();
  }

  Future<void> _startLoad() async {
    // nếu đã có qrData (dataURL) thì giải sẵn để hiển thị ngay
    if (widget.qrData != null && widget.qrData!.startsWith('data:image')) {
      _qrBytes = _decodeDataUrl(widget.qrData!);
    }
    await _tryFetchDetailsWithRetry();
  }

  // -------------------- FETCH + RETRY --------------------
  Future<void> _tryFetchDetailsWithRetry() async {
    _safeSetState(() {
      _loading = true;
      _err = null;
    });

    // Thử lại 6 lần: 1s,2s,3s,4s,5s,6s
    const attempts = 6;
    for (int i = 0; i < attempts; i++) {
      final ok = await _fetchDetailsOnce();
      if (ok) {
        _safeSetState(() => _loading = false);
        return;
      }
      // nếu chưa thành công (404) → delay tăng dần
      await Future.delayed(Duration(seconds: i + 1));
    }

    // Hết lượt thử
    _safeSetState(() {
      _loading = false;
      _err ??= 'Không thể tải chi tiết hóa đơn (404/pending).';
    });
  }

  /// Trả về true nếu lấy được 200 và parse thành công.
  /// Nếu 404 → false để thử lại. Các lỗi khác sẽ set _err và trả false.
  Future<bool> _fetchDetailsOnce() async {
    try {
      final encodedId = Uri.encodeComponent(widget.orderId.trim());
      final url = Uri.parse(
        '${Env.baseUrl}/api/orders/$encodedId/details?t=${DateTime.now().millisecondsSinceEpoch}',
      );

      final res = await http.get(url);
      if (res.statusCode == 404) {
        // Đơn chưa phát hành xong vé → thử lại
        return false;
      }
      if (res.statusCode != 200) {
        _safeSetState(() => _err = 'HTTP ${res.statusCode}');
        return false;
      }

      final m = Map<String, dynamic>.from(json.decode(res.body));

      final title = (m['movieTitle'] ?? widget.movieTitle).toString();
      final cinema = (m['cinemaName'] ?? '').toString();
      final screen = (m['screenName'] ?? '').toString();
      final total = _asInt(m['total']);
      final seats = (m['seats'] is List)
          ? (m['seats'] as List).map((e) => e.toString()).toList()
          : <String>[];

      DateTime? startTime;
      final st = m['startTime']?.toString();
      if (st != null && st.isNotEmpty) {
        startTime = DateTime.tryParse(st) ?? _parseLoose(st);
      }

      Uint8List? qrBytes = _qrBytes;
      if (qrBytes == null && m['qrData'] is String) {
        final s = m['qrData'] as String;
        if (s.startsWith('data:image')) {
          qrBytes = _decodeDataUrl(s);
        }
      }

      _safeSetState(() {
        _movieTitle = title;
        _cinemaName = cinema;
        _screenName = screen;
        _total = total;
        _seats = seats;
        _startTime = startTime;
        _qrBytes = qrBytes;
      });

      return true;
    } catch (e) {
      _safeSetState(() => _err = '$e');
      return false;
    }
  }

  // -------------------- helpers --------------------
  void _safeSetState(VoidCallback fn) {
    if (!mounted) return;
    setState(fn);
  }

  int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v) ?? 0;
    return 0;
  }

  String _fmtMoney(int v) {
    final s = v.toString();
    return s.replaceAllMapped(
      RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
          (m) => '${m[1]}.',
    );
  }

  String _fmtDateTime(DateTime? d) {
    if (d == null) return '—';
    final dd = d.day.toString().padLeft(2, '0');
    final mm = d.month.toString().padLeft(2, '0');
    final yyyy = d.year.toString();
    final hh = d.hour.toString().padLeft(2, '0');
    final mi = d.minute.toString().padLeft(2, '0');
    return '$dd/$mm/$yyyy - $hh:$mi';
  }

  DateTime? _parseLoose(String s) {
    try {
      // chấp nhận 'yyyy-MM-ddTHH:mm' hoặc ISO tương đương
      return DateTime.parse(s);
    } catch (_) {
      return null;
    }
  }

  Uint8List? _decodeDataUrl(String dataUrl) {
    try {
      final idx = dataUrl.indexOf('base64,');
      if (idx < 0) return null;
      final b64 = dataUrl.substring(idx + 7);
      return base64Decode(b64);
    } catch (_) {
      return null;
    }
  }

  Widget _kv(String k, String v, {FontWeight fw = FontWeight.w500}) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      children: [
        SizedBox(
          width: 110,
          child: Text(k,
              style: const TextStyle(
                  color: Colors.black54, fontWeight: FontWeight.w600)),
        ),
        const SizedBox(width: 8),
        Expanded(child: Text(v, style: TextStyle(fontWeight: fw))),
      ],
    ),
  );

  Widget _sectionTitle(String t) => Padding(
    padding: const EdgeInsets.only(top: 16, bottom: 8),
    child: Text(t, style: const TextStyle(fontWeight: FontWeight.w700)),
  );

  // -------------------- UI --------------------
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hoàn tất thanh toán'),
        backgroundColor: const Color(0xFF6A1B9A),
        centerTitle: true,
      ),
      body: _loading
          ? const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(color: Color(0xFF6A1B9A)),
            SizedBox(height: 12),
            Text('Đang phát hành vé, vui lòng đợi...'),
          ],
        ),
      )
          : _err != null
          ? Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline,
                color: Colors.red, size: 40),
            const SizedBox(height: 10),
            const Text('Không tải được chi tiết đơn.',
                style: TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 6),
            Text(_err!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.black54)),
            const SizedBox(height: 16),
            ElevatedButton.icon(
              onPressed: _tryFetchDetailsWithRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử lại'),
            ),
            const Spacer(),
            _backHomeBtn(context),
          ],
        ),
      )
          : SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: const [
                Icon(Icons.check_circle,
                    color: Colors.green, size: 28),
                SizedBox(width: 8),
                Text('Thanh toán thành công',
                    style: TextStyle(
                        fontSize: 18, fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 12),
            Text('Mã đơn: ${widget.orderId}',
                style:
                const TextStyle(fontWeight: FontWeight.w700)),

            _sectionTitle('Thông tin suất'),
            _kv('Phim',
                _movieTitle.isNotEmpty ? _movieTitle : widget.movieTitle),
            _kv('Rạp', _cinemaName.isNotEmpty ? _cinemaName : '—'),
            _kv('Phòng', _screenName.isNotEmpty ? _screenName : '—'),
            _kv('Suất', _fmtDateTime(_startTime)),

            _sectionTitle('Thông tin vé - ghế'),
            Wrap(
              spacing: 10,
              runSpacing: 10,
              children: _seats.isEmpty
                  ? [const Text('—')]
                  : _seats
                  .map((s) => Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: 14, vertical: 8),
                decoration: BoxDecoration(
                  border: Border.all(
                      color: const Color(0xFFBDBDBD)),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(s,
                    style: const TextStyle(
                        fontWeight: FontWeight.w600)),
              ))
                  .toList(),
            ),

            const SizedBox(height: 8),
            const Divider(),
            _kv('Tổng tiền', '${_fmtMoney(_total)} đ',
                fw: FontWeight.w700),

            _sectionTitle('Mã QR'),
            if (_qrBytes != null)
              Center(
                child: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    border: Border.all(color: Colors.black12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Image.memory(_qrBytes!,
                      width: 220, height: 220),
                ),
              )
            else
              const Text('Không có QR.',
                  style: TextStyle(color: Colors.black54)),

            const SizedBox(height: 24),
            _backHomeBtn(context),
          ],
        ),
      ),
    );
  }

  Widget _backHomeBtn(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: ElevatedButton(
        style: ElevatedButton.styleFrom(
          backgroundColor: const Color(0xFF6A1B9A),
          minimumSize: const Size.fromHeight(48),
          shape:
          RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
        onPressed: () => Navigator.of(context).popUntil((r) => r.isFirst),
        child: const Text('Về trang chủ',
            style:
            TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
      ),
    );
  }
}
