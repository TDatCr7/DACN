// invoice_confirm_page.dart
// UPDATE: Fix lỗi "Invalid userId (not found)" bằng cách:
// 1) Không gửi IdentityId trực tiếp cho /vnpay/create (API đang check Users.UserId).
// 2) Resolve "customerUserId" từ email qua API mới /users/resolve (phải thêm API ở backend - hướng dẫn bên dưới).
// 3) Lưu customer_user_id vào SharedPreferences để dùng lại.
// 4) Dọn biến userId bị khai báo 2 lần + thêm xử lý lỗi rõ ràng.

import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../services/api_config.dart';

class InvoiceConfirmArgs {
  final String showTimeId;
  final List<String> seatIds;
  final List<Map<String, dynamic>> snacks;
  final String? promoCode;

  InvoiceConfirmArgs({
    required this.showTimeId,
    required this.seatIds,
    required this.snacks,
    this.promoCode,
  });
}

class InvoiceConfirmPage extends StatefulWidget {
  const InvoiceConfirmPage({super.key});

  @override
  State<InvoiceConfirmPage> createState() => _InvoiceConfirmPageState();
}

class _InvoiceConfirmPageState extends State<InvoiceConfirmPage> {
  late InvoiceConfirmArgs args;

  bool loading = true;
  String? error;
  Map<String, dynamic>? preview;
  bool creating = false;

  late final TextEditingController _promoCtl;

  @override
  void initState() {
    super.initState();
    _promoCtl = TextEditingController();
  }

  @override
  void dispose() {
    _promoCtl.dispose();
    super.dispose();
  }

  Map<String, String> _headers() => const {'Content-Type': 'application/json'};

  Future<String?> _getEmail() async {
    final prefs = await SharedPreferences.getInstance();
    final e1 = (prefs.getString('email') ?? '').trim();
    if (e1.isNotEmpty) return e1;
    final e2 = (prefs.getString('user_email') ?? '').trim();
    if (e2.isNotEmpty) return e2;
    final e3 = (prefs.getString('userEmail') ?? '').trim();
    if (e3.isNotEmpty) return e3;
    return null;
  }

  Future<String> _getCachedCustomerUserId() async {
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getString('customer_user_id') ?? '').trim();
  }

  Future<void> _setCachedCustomerUserId(String userId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('customer_user_id', userId.trim());
  }

  /// Gọi API backend để resolve Users.UserId từ email
  /// Backend cần có endpoint: GET /api/users/resolve?email=...
  Future<String> _resolveCustomerUserIdOrThrow() async {
    // 1) ưu tiên cache
    final cached = await _getCachedCustomerUserId();
    if (cached.isNotEmpty) return cached;

    // 2) resolve bằng email
    final email = await _getEmail();
    if (email == null || email.isEmpty) {
      throw Exception('Thiếu email (không thể resolve userId).');
    }

    final uri = Uri.parse('${ApiConfig.apiBase}/users/resolve')
        .replace(queryParameters: {'email': email});

    final res = await http.get(uri, headers: _headers()).timeout(const Duration(seconds: 15));

    if (res.statusCode != 200) {
      throw Exception('HTTP ${res.statusCode}: ${res.body}');
    }

    final data = jsonDecode(res.body) as Map<String, dynamic>;

    // expected: { ok:true, userId:"U001" }
    final ok = (data['ok'] == true);
    final userId = (data['userId'] ?? '').toString().trim();

    if (!ok || userId.isEmpty) {
      throw Exception('Resolve userId thất bại: ${res.body}');
    }

    await _setCachedCustomerUserId(userId);
    return userId;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();

    final a = ModalRoute.of(context)?.settings.arguments;
    if (a is InvoiceConfirmArgs) {
      args = a;
      _promoCtl.text = (args.promoCode ?? '').trim();
      _loadPreview(promoOverride: _promoCtl.text);
      return;
    }

    setState(() {
      loading = false;
      error = 'Thiếu InvoiceConfirmArgs';
    });
  }

  Future<void> _loadPreview({String? promoOverride}) async {
    setState(() {
      loading = true;
      error = null;
      preview = null;
    });

    try {
      final body = {
        'showTimeId': args.showTimeId,
        'seatIds': args.seatIds,
        'snacks': args.snacks,
        'promoCode': (promoOverride ?? '').trim().isEmpty ? null : (promoOverride ?? '').trim(),
      };

      final res = await http
          .post(
        Uri.parse('${ApiConfig.apiBase}/bookings/preview'),
        headers: _headers(),
        body: jsonEncode(body),
      )
          .timeout(const Duration(seconds: 15));

      if (res.statusCode != 200) {
        throw Exception('HTTP ${res.statusCode}: ${res.body}');
      }

      preview = jsonDecode(res.body) as Map<String, dynamic>;
      setState(() => loading = false);
    } catch (e) {
      setState(() {
        loading = false;
        error = e.toString();
      });
    }
  }

  List<Map<String, dynamic>> _normalizeSnackLines(List<Map<String, dynamic>> raw) {
    return raw
        .map((e) {
      final sid = (e['snackId'] ?? e['id'] ?? '').toString().trim();
      final qRaw = e['quantity'] ?? e['qty'] ?? 0;
      final qty = qRaw is int ? qRaw : int.tryParse(qRaw.toString()) ?? 0;
      return {'snackId': sid, 'quantity': qty};
    })
        .where((x) => x['snackId']!.toString().isNotEmpty && (x['quantity'] as int) > 0)
        .toList();
  }

  Future<void> _createInvoiceAndPay() async {
    setState(() => creating = true);

    try {
      final promo = _promoCtl.text.trim();

      // ✅ đây là Users.UserId (CustomerId) thật sự trong DB, không phải IdentityId
      final customerUserId = await _resolveCustomerUserIdOrThrow();

      final email = await _getEmail();

      final createBody = {
        'userId': customerUserId,
        'email': email,
        'showtimeId': args.showTimeId,
        'seatIds': args.seatIds,
        'snacks': _normalizeSnackLines(args.snacks),
        'movieTitle': (preview?['meta']?['movieTitle'] ?? '').toString(),
      };

      debugPrint('[PAY] resolved customerUserId="$customerUserId"');

      final createRes = await http
          .post(
        Uri.parse('${ApiConfig.apiBase}/vnpay/create'),
        headers: _headers(),
        body: jsonEncode(createBody),
      )
          .timeout(const Duration(seconds: 15));

      if (createRes.statusCode != 200) {
        throw Exception('HTTP ${createRes.statusCode}: ${createRes.body}');
      }

      final createData = jsonDecode(createRes.body) as Map<String, dynamic>;
      final invoiceId = (createData['invoiceId'] ?? createData['orderId'] ?? '').toString().trim();
      final paymentUrl = (createData['paymentUrl'] ?? '').toString().trim();

      if (invoiceId.isEmpty || paymentUrl.isEmpty) {
        throw Exception('Thiếu invoiceId/paymentUrl từ API.');
      }

      // apply/remove promotion
      if (promo.isNotEmpty) {
        final applyBody = {'invoiceId': invoiceId, 'code': promo, 'userId': customerUserId};

        final applyRes = await http
            .post(
          Uri.parse('${ApiConfig.apiBase}/vnpay/apply-promotion'),
          headers: _headers(),
          body: jsonEncode(applyBody),
        )
            .timeout(const Duration(seconds: 15));

        if (applyRes.statusCode != 200) {
          final removeBody = {'invoiceId': invoiceId, 'userId': customerUserId};
          await http
              .post(
            Uri.parse('${ApiConfig.apiBase}/vnpay/remove-promotion'),
            headers: _headers(),
            body: jsonEncode(removeBody),
          )
              .timeout(const Duration(seconds: 15));
        }
      } else {
        final removeBody = {'invoiceId': invoiceId, 'userId': customerUserId};
        await http
            .post(
          Uri.parse('${ApiConfig.apiBase}/vnpay/remove-promotion'),
          headers: _headers(),
          body: jsonEncode(removeBody),
        )
            .timeout(const Duration(seconds: 15));
      }

      if (!mounted) return;

      Navigator.pushNamed(
        context,
        '/vnpay_webview',
        arguments: {
          'userId': customerUserId,
          'orderId': invoiceId,
          'invoiceId': invoiceId,
          'paymentUrl': paymentUrl,
        },
      );
    } finally {
      if (mounted) setState(() => creating = false);
    }
  }

  String _vnd(num v) {
    final s = v.round().toString();
    final b = StringBuffer();
    for (int i = 0; i < s.length; i++) {
      final idxFromEnd = s.length - i;
      b.write(s[i]);
      if (idxFromEnd > 1 && idxFromEnd % 3 == 1) b.write(',');
    }
    return '${b.toString()} đ';
  }

  String _fmtDate(dynamic v) {
    if (v == null) return '';
    final s = v.toString();
    if (s.contains('T')) {
      final d = s.split('T').first;
      final parts = d.split('-');
      if (parts.length == 3) return '${parts[2]}/${parts[1]}/${parts[0]}';
      return d;
    }
    final parts = s.split('-');
    if (parts.length == 3) return '${parts[2]}/${parts[1]}/${parts[0]}';
    return s;
  }

  String _fmtTime(dynamic v) {
    if (v == null) return '';
    final s = v.toString();
    if (s.contains('T')) {
      final t = s.split('T')[1];
      final parts = t.split(':');
      if (parts.length >= 2) return '${parts[0]}:${parts[1]}';
      return t;
    }
    final parts = s.split(':');
    if (parts.length >= 2) return '${parts[0]}:${parts[1]}';
    return s;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs = theme.colorScheme;

    if (loading) {
      return Scaffold(
        appBar: AppBar(title: const Text('Xác nhận hóa đơn')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    if (error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Xác nhận hóa đơn')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Text(error!, textAlign: TextAlign.center),
          ),
        ),
      );
    }

    final p = preview!;
    final meta = (p['meta'] as Map<String, dynamic>? ?? {});
    final seats = (p['seats'] as List<dynamic>? ?? []);
    final snacks = (p['snacks'] as List<dynamic>? ?? []);
    final promo = p['promo'] as Map<String, dynamic>? ?? {};

    final seatsTotal = (p['seatsTotal'] ?? 0);
    final snacksTotal = (p['snacksTotal'] ?? 0);
    final originalTotal = (p['originalTotal'] ?? 0);
    final discountAmount = (promo['discountAmount'] ?? 0);
    final totalAfter = (p['totalAfterDiscount'] ?? 0);

    final cinemaName = (meta['cinemaName'] ?? '').toString().trim();
    final screenName = (meta['screenName'] ?? '').toString().trim();

    final showDate = _fmtDate(meta['showDate']);
    final start = _fmtTime(meta['startTime']);
    final end = _fmtTime(meta['endTime']);

    final movieTitle = (meta['movieTitle'] ?? '').toString().trim();

    final poster = ApiConfig.fileUrl((meta['poster'] ?? '').toString());

    final genres = (meta['genres'] ?? '').toString().trim();
    final ageRating = (meta['ageRating'] ?? '').toString().trim();
    final durationMinutes = meta['durationMinutes'];

    final promoCode = (promo['code'] ?? '').toString().trim();

    final seatLabels = seats
        .map((e) => Map<String, dynamic>.from(e as Map)['label']?.toString() ?? '')
        .where((x) => x.trim().isNotEmpty)
        .toList();
    final seatLabelText = seatLabels.isEmpty ? '' : seatLabels.join(', ');
    final ticketCount = seatLabels.isNotEmpty ? seatLabels.length : args.seatIds.length;

    return Scaffold(
      appBar: AppBar(title: const Text('Xác nhận hóa đơn')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(12, 12, 12, 120),
        children: [
          _sectionHeader('THÔNG TIN VÉ'),
          const SizedBox(height: 8),
          _card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  cinemaName.isEmpty ? '-' : cinemaName,
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w900),
                ),
                const SizedBox(height: 4),
                Text(
                  '${showDate.isEmpty ? '-' : showDate} - ${start.isEmpty ? '--:--' : start}'
                      '${end.isEmpty ? '' : ' | $end'}'
                      '${screenName.isEmpty ? '' : ' | Phòng: $screenName'}',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.textTheme.bodyMedium?.color?.withOpacity(0.75),
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _card(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: Container(
                    width: 92,
                    height: 124,
                    color: cs.surfaceContainerHighest.withOpacity(0.45),
                    child: poster.isEmpty
                        ? const Icon(Icons.movie, size: 34)
                        : Image.network(
                      poster,
                      width: 92,
                      height: 124,
                      fit: BoxFit.cover,
                      errorBuilder: (_, __, ___) => const Icon(Icons.movie, size: 34),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        movieTitle.isEmpty ? '-' : movieTitle,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w900),
                      ),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          if (ageRating.isNotEmpty) _tag(text: ageRating),
                          if (genres.isNotEmpty) _tag(text: genres),
                        ],
                      ),
                      const SizedBox(height: 10),
                      if (durationMinutes != null)
                        Text(
                          'Thời lượng: ${durationMinutes.toString()} phút',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: theme.textTheme.bodyMedium?.color?.withOpacity(0.8),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      if (seatLabelText.isNotEmpty) ...[
                        const SizedBox(height: 6),
                        Text(
                          'Ghế: $seatLabelText',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: theme.textTheme.bodyMedium?.color?.withOpacity(0.85),
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _sectionHeader('THÔNG TIN VÉ'),
          const SizedBox(height: 8),
          _card(
            child: Column(
              children: [
                ...seats.map((e) {
                  final m = Map<String, dynamic>.from(e as Map);
                  final label = (m['label'] ?? '').toString().trim();
                  final price = (m['unitPrice'] ?? 0);
                  return _twoCol(left: label.isEmpty ? 'Ghế' : label, right: _vnd(price));
                }).toList(),
                const Divider(height: 18),
                _twoCol(left: 'Số lượng', right: ticketCount.toString()),
                _twoCol(left: 'Tổng', right: _vnd(seatsTotal), boldRight: true),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _sectionHeader('THÔNG TIN BẮP NƯỚC'),
          const SizedBox(height: 8),
          _card(
            child: snacks.isEmpty
                ? Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Không chọn',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.textTheme.bodyMedium?.color?.withOpacity(0.7),
                  ),
                ),
              ),
            )
                : Column(
              children: [
                ...snacks.map((e) {
                  final m = Map<String, dynamic>.from(e as Map);
                  final name = (m['name'] ?? '').toString();
                  final qty = (m['quantity'] ?? 0).round();
                  final lineTotal = (m['lineTotal'] ?? 0);
                  return _twoCol(
                    left: name.isEmpty ? 'Snack' : '$name x$qty',
                    right: _vnd(lineTotal),
                  );
                }).toList(),
                const Divider(height: 18),
                _twoCol(left: 'Tổng', right: _vnd(snacksTotal), boldRight: true),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _sectionHeader('MÃ KHUYẾN MÃI'),
          const SizedBox(height: 8),
          _card(
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _promoCtl,
                    textInputAction: TextInputAction.done,
                    decoration: InputDecoration(
                      hintText: 'Mã khuyến mãi',
                      filled: true,
                      fillColor: theme.colorScheme.surfaceContainerHighest.withOpacity(0.35),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(14),
                        borderSide: BorderSide(color: Colors.black.withOpacity(0.08)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(14),
                        borderSide: BorderSide(color: Colors.black.withOpacity(0.08)),
                      ),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                FilledButton(
                  onPressed: () => _loadPreview(promoOverride: _promoCtl.text),
                  child: const Text('ÁP DỤNG'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _sectionHeader('THANH TOÁN'),
          const SizedBox(height: 8),
          _card(
            child: Column(
              children: [
                _twoCol(left: 'Tổng cộng', right: _vnd(originalTotal)),
                _twoCol(left: 'Giảm giá', right: promoCode.isEmpty ? _vnd(0) : _vnd(discountAmount)),
                const Divider(height: 18),
                _twoCol(left: 'Còn lại', right: _vnd(totalAfter), boldLeft: true, boldRight: true),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
          decoration: BoxDecoration(
            color: theme.scaffoldBackgroundColor,
            border: Border(top: BorderSide(color: Colors.black.withOpacity(0.08))),
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Tổng cộng',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.textTheme.bodySmall?.color?.withOpacity(0.7),
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      _vnd(totalAfter),
                      style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w900),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              SizedBox(
                height: 48,
                child: FilledButton(
                  onPressed: creating ? null : _createInvoiceAndPay,
                  child: creating
                      ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                      : const Text('THANH TOÁN'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _sectionHeader(String title) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: Text(
        title,
        style: theme.textTheme.labelLarge?.copyWith(
          letterSpacing: 0.8,
          fontWeight: FontWeight.w900,
          color: theme.textTheme.labelLarge?.color?.withOpacity(0.7),
        ),
      ),
    );
  }

  Widget _card({required Widget child}) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      color: theme.colorScheme.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: BorderSide(color: Colors.black.withOpacity(0.08)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: child,
      ),
    );
  }

  Widget _twoCol({
    required String left,
    required String right,
    bool boldLeft = false,
    bool boldRight = false,
  }) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Expanded(
            child: Text(
              left,
              style: theme.textTheme.bodyMedium?.copyWith(
                fontWeight: boldLeft ? FontWeight.w900 : FontWeight.w600,
                color: theme.textTheme.bodyMedium?.color?.withOpacity(boldLeft ? 0.95 : 0.78),
              ),
            ),
          ),
          Text(
            right,
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: boldRight ? FontWeight.w900 : FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }

  Widget _tag({required String text}) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(999),
        color: theme.colorScheme.surfaceContainerHighest.withOpacity(0.45),
        border: Border.all(color: Colors.black.withOpacity(0.08)),
      ),
      child: Text(
        text,
        style: theme.textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w800),
      ),
    );
  }
}
