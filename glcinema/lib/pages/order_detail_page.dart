import 'dart:convert';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/env.dart';

class OrderDetailPage extends StatefulWidget {
  final String orderId;
  const OrderDetailPage({super.key, required this.orderId});

  @override
  State<OrderDetailPage> createState() => _OrderDetailPageState();
}

class _OrderDetailPageState extends State<OrderDetailPage> {
  bool _loading = true;
  String? _err;

  // dữ liệu chi tiết
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
    _fetch();
  }

  Future<void> _fetch() async {
    try {
      setState(() { _loading = true; _err = null; });

      final url = Uri.parse('${Env.baseUrl}/api/orders/${widget.orderId}/details');
      final r = await http.get(url);
      if (r.statusCode != 200) throw Exception('HTTP ${r.statusCode}');
      final m = Map<String, dynamic>.from(json.decode(r.body));

      _movieTitle = (m['movieTitle'] ?? '').toString();
      _cinemaName = (m['cinemaName'] ?? '').toString();
      _screenName = (m['screenName'] ?? '').toString();
      _total      = _asInt(m['total']);

      _seats = (m['seats'] is List)
          ? (m['seats'] as List).map((e) => e.toString()).toList()
          : <String>[];

      final st = m['startTime']?.toString();
      if (st != null && st.isNotEmpty) {
        _startTime = DateTime.tryParse(st);
      }

      final qr = (m['qrData'] ?? '').toString();
      if (qr.startsWith('data:image')) {
        _qrBytes = _decodeDataUrl(qr);
      }
    } catch (e) {
      _err = '$e';
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  // ---------- helpers ----------
  Uint8List? _decodeDataUrl(String dataUrl) {
    try {
      final idx = dataUrl.indexOf('base64,');
      if (idx < 0) return null;
      return base64Decode(dataUrl.substring(idx + 7));
    } catch (_) {
      return null;
    }
  }

  int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v) ?? 0;
    return 0;
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

  String _money(int v) =>
      v.toString().replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+(?!\d))'), (m) => '${m[1]}.');

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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Vé #${widget.orderId}'),
        backgroundColor: const Color(0xFF6A1B9A),
        centerTitle: true,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: Color(0xFF6A1B9A)))
          : (_err != null)
          ? Center(child: Text('Lỗi: $_err'))
          : SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: const [
                Icon(Icons.confirmation_number, color: Colors.deepPurple),
                SizedBox(width: 8),
                Text('Chi tiết vé',
                    style: TextStyle(
                        fontSize: 18, fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 12),
            _kv('Phim', _movieTitle),
            _kv('Rạp', _cinemaName),
            _kv('Phòng', _screenName),
            _kv('Suất', _fmtDateTime(_startTime)),
            const SizedBox(height: 8),
            const Divider(),
            _kv('Tổng tiền', '${_money(_total)} đ', fw: FontWeight.w700),

            const SizedBox(height: 12),
            const Text('Ghế', style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 6),
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
                  border: Border.all(color: const Color(0xFFBDBDBD)),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(s,
                    style: const TextStyle(
                        fontWeight: FontWeight.w600)),
              ))
                  .toList(),
            ),

            const SizedBox(height: 16),
            const Text('Mã QR',
                style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            if (_qrBytes != null)
              Center(
                child: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    border: Border.all(color: Colors.black12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Image.memory(_qrBytes!, width: 220, height: 220),
                ),
              )
            else
              const Text('Không có QR', style: TextStyle(color: Colors.black54)),
          ],
        ),
      ),
    );
  }
}
