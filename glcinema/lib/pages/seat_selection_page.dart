// lib/pages/seat_selection_page.dart
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/api_config.dart';
import '../main.dart'; // NEW: dùng AppRoutes

class SeatSelectionPage extends StatefulWidget {
  final String movieTitle;
  final String showtimeId;

  const SeatSelectionPage({
    super.key,
    required this.movieTitle,
    required this.showtimeId,
  });

  @override
  State<SeatSelectionPage> createState() => _SeatSelectionPageState();
}

class _SeatSelectionPageState extends State<SeatSelectionPage> {
  bool isLoading = true;
  String? loadError;

  int numOfColumns = 0;

  final Map<String, List<_Seat>> seatsByRow = {};
  final Map<String, _Seat> seatById = {};
  final Set<String> selectedSeatIds = {};

  static const double _seatSize = 38;
  static const double _gap = 8;
  static const double _rowGap = 10;
  static const double _labelW = 28;

  @override
  void initState() {
    super.initState();
    fetchSeatMap();
  }

  Future<void> fetchSeatMap() async {
    setState(() {
      isLoading = true;
      loadError = null;
      seatsByRow.clear();
      seatById.clear();
      numOfColumns = 0;
      selectedSeatIds.clear();
    });

    try {
      final showtimeId = widget.showtimeId.trim();
      if (showtimeId.isEmpty) {
        setState(() {
          isLoading = false;
          loadError = 'showtimeId rỗng';
        });
        return;
      }

      final uri = Uri.parse(
        '${ApiConfig.apiBase}/showtimes/${Uri.encodeComponent(showtimeId)}/seats',
      );

      final res = await http.get(uri).timeout(const Duration(seconds: 12));
      if (res.statusCode < 200 || res.statusCode >= 300) {
        setState(() {
          isLoading = false;
          loadError = 'HTTP ${res.statusCode}';
        });
        return;
      }

      final decoded = jsonDecode(res.body);
      if (decoded is! List) {
        setState(() {
          isLoading = false;
          loadError = 'Response không phải List';
        });
        return;
      }

      final seats = decoded
          .whereType<Map>()
          .map((e) => _Seat.fromApi(Map<String, dynamic>.from(e)))
          .where((s) => s.isValid)
          .toList();

      int maxCol = 0;
      for (final s in seats) {
        if (s.columnIndex > maxCol) maxCol = s.columnIndex;
      }

      final grouped = <String, List<_Seat>>{};
      for (final s in seats) {
        grouped.putIfAbsent(s.rowIndex, () => <_Seat>[]).add(s);
      }
      for (final k in grouped.keys) {
        grouped[k]!.sort((a, b) => a.columnIndex.compareTo(b.columnIndex));
      }

      final mapById = <String, _Seat>{};
      for (final s in seats) {
        mapById[s.seatId] = s;
      }

      setState(() {
        numOfColumns = maxCol;
        seatsByRow.addAll(grouped);
        seatById.addAll(mapById);
        isLoading = false;
      });
    } catch (e) {
      setState(() {
        isLoading = false;
        loadError = 'Exception: ${e.toString()}';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final rows = seatsByRow.keys.toList()..sort();

    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF8A2BE2),
        title: Text('Chọn ghế - ${widget.movieTitle}'),
        centerTitle: true,
      ),
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0xFF0B1220), Color(0xFF0A1020)],
          ),
        ),
        child: isLoading
            ? const Center(
          child: CircularProgressIndicator(color: Color(0xFFE41E26)),
        )
            : (loadError != null)
            ? Center(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              loadError!,
              style: const TextStyle(
                color: Color(0xFFEF4444),
                fontWeight: FontWeight.w800,
              ),
              textAlign: TextAlign.center,
            ),
          ),
        )
            : Column(
          children: [
            const SizedBox(height: 10),
            _screenPill(),
            const SizedBox(height: 12),
            Expanded(child: _seatBoardMobile(rows)),
            _legendBar(),
            _bottomBarSimple(), // UPDATED: nút Chọn snack điều hướng
          ],
        ),
      ),
    );
  }

  Widget _screenPill() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Container(
        height: 44,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(999),
          gradient: const LinearGradient(
            colors: [Color(0xFFF3F4F6), Color(0xFFCBD5E1)],
          ),
        ),
        alignment: Alignment.center,
        child: const Text(
          'MÀN HÌNH',
          style: TextStyle(
            fontWeight: FontWeight.w900,
            color: Color(0xFF111827),
          ),
        ),
      ),
    );
  }

  Widget _seatBoardMobile(List<String> rows) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: Container(
        decoration: BoxDecoration(
          color: const Color(0xFF0E1728).withOpacity(.85),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFF1F2A44)),
        ),
        child: (rows.isEmpty || numOfColumns <= 0)
            ? const Center(
          child: Text(
            'Không có dữ liệu ghế',
            style: TextStyle(color: Color(0xFF9CA3AF)),
          ),
        )
            : ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: _TwoAxisSeatScroller(
            padding: const EdgeInsets.fromLTRB(12, 14, 12, 14),
            horizontalBuilder: (BuildContext context, ScrollController hCtrl) {
              return SingleChildScrollView(
                controller: hCtrl,
                scrollDirection: Axis.horizontal,
                child: ConstrainedBox(
                  constraints: BoxConstraints(minWidth: _gridWidth()),
                  child: Column(
                    children: rows.map((rowKey) {
                      final rowSeats = seatsByRow[rowKey] ?? const <_Seat>[];
                      return Padding(
                        padding: const EdgeInsets.symmetric(vertical: _rowGap / 2),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.center,
                          children: [
                            _rowLabel(rowKey),
                            const SizedBox(width: 10),
                            _rowCellsWebLike(rowSeats),
                            const SizedBox(width: 10),
                            _rowLabel(rowKey),
                          ],
                        ),
                      );
                    }).toList(),
                  ),
                ),
              );
            },
            verticalBuilder: (BuildContext context, ScrollController vCtrl, Widget child) {
              return Scrollbar(
                controller: vCtrl,
                thumbVisibility: true,
                child: SingleChildScrollView(
                  controller: vCtrl,
                  child: child,
                ),
              );
            },
          ),
        ),
      ),
    );
  }

  double _gridWidth() {
    return (numOfColumns * _seatSize) +
        ((numOfColumns - 1) * _gap) +
        (_labelW * 2) +
        20;
  }

  Widget _rowLabel(String r) {
    return SizedBox(
      width: _labelW,
      child: Text(
        r,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: Color(0xFFFBBF24),
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }

  Widget _rowCellsWebLike(List<_Seat> rowSeats) {
    final seatByCol = <int, _Seat>{};
    for (final s in rowSeats) {
      seatByCol[s.columnIndex] = s;
    }

    final processedCols = <int>{};
    final children = <Widget>[];

    for (int col = 1; col <= numOfColumns; col++) {
      if (processedCols.contains(col)) continue;

      final seat = seatByCol[col];

      if (seat == null || seat.isAisle) {
        children.add(_placeholder());
        if (col != numOfColumns) children.add(const SizedBox(width: _gap));
        continue;
      }

      final isCouple = seat.isCouple || seat.pairId.isNotEmpty;
      if (isCouple) {
        final mate = rowSeats.firstWhere(
              (x) => x.pairId == seat.pairId && x.seatId != seat.seatId,
          orElse: () => _Seat.empty(),
        );

        if (!mate.isValid) {
          processedCols.add(col);
          children.add(_singleSeatTile(seat));
          if (col != numOfColumns) children.add(const SizedBox(width: _gap));
          continue;
        }

        processedCols.add(seat.columnIndex);
        processedCols.add(mate.columnIndex);

        if (seat.isAisle || mate.isAisle) {
          children.add(_placeholder());
          children.add(const SizedBox(width: _gap));
          children.add(_placeholder());
          if (col != numOfColumns) children.add(const SizedBox(width: _gap));
          continue;
        }

        children.add(_coupleSeatTile(seat, mate));
        if (col != numOfColumns) children.add(const SizedBox(width: _gap));
        continue;
      }

      processedCols.add(col);
      children.add(_singleSeatTile(seat));
      if (col != numOfColumns) children.add(const SizedBox(width: _gap));
    }

    return Row(children: children);
  }

  Widget _placeholder() {
    return SizedBox(
      width: _seatSize,
      height: _seatSize,
      child: const Opacity(
        opacity: 0,
        child: DecoratedBox(decoration: BoxDecoration()),
      ),
    );
  }

  Widget _singleSeatTile(_Seat s) {
    final isSelected = selectedSeatIds.contains(s.seatId);

    final bool isBooked = s.isBooked;
    final bool isBroken = !s.isActive && !isBooked;
    final bool isVip = s.isVip && !isBooked && !isBroken;

    Color bg;
    if (isBooked) {
      bg = const Color(0xFF111827);
    } else if (isBroken) {
      bg = const Color(0xFF0B1220);
    } else if (isSelected) {
      bg = const Color(0xFFE11D48);
    } else if (isVip) {
      bg = const Color(0xFFF59E0B);
    } else {
      bg = const Color(0xFF374151);
    }

    final Color border =
    isVip ? const Color(0xFFFBBF24) : Colors.white.withOpacity(.12);

    return GestureDetector(
      onTap: (isBooked || isBroken) ? null : () => _toggleSingle(s),
      child: Container(
        width: _seatSize,
        height: _seatSize,
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: border, width: isVip ? 1.4 : 1),
        ),
        alignment: Alignment.center,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Center(
              child: isBooked
                  ? const Icon(Icons.check, color: Colors.white, size: 18)
                  : isBroken
                  ? const Text(
                '✗',
                style: TextStyle(
                  color: Color(0xFFE5E7EB),
                  fontWeight: FontWeight.w900,
                  fontSize: 16,
                ),
              )
                  : Text(
                s.label,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
                textAlign: TextAlign.center,
              ),
            ),
            if (isVip)
              Positioned(
                top: -5,
                right: -5,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFBBF24),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Text(
                    'V',
                    style: TextStyle(
                      color: Color(0xFF111827),
                      fontWeight: FontWeight.w900,
                      fontSize: 10,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _coupleSeatTile(_Seat a, _Seat b) {
    final selected =
        selectedSeatIds.contains(a.seatId) && selectedSeatIds.contains(b.seatId);

    final bool isBooked = a.isBooked || b.isBooked;
    final bool isBroken = (!a.isActive || !b.isActive) && !isBooked;

    final Color bg = isBooked
        ? const Color(0xFF111827)
        : isBroken
        ? const Color(0xFF0B1220)
        : (selected ? const Color(0xFFE11D48) : const Color(0xFFB453E6));

    final aLabel = a.label.trim().isNotEmpty ? a.label.trim() : '${a.rowIndex}${a.columnIndex}';
    final bLabel = b.label.trim().isNotEmpty ? b.label.trim() : '${b.rowIndex}${b.columnIndex}';
    final label = '$aLabel-$bLabel'.replaceAll('--', '-');

    final double coupleW = (_seatSize * 2) + _gap;

    return GestureDetector(
      onTap: (isBooked || isBroken) ? null : () => _toggleCouple(a, b),
      child: Container(
        width: coupleW,
        height: _seatSize,
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: Colors.white.withOpacity(.10)),
        ),
        alignment: Alignment.center,
        child: isBooked
            ? const Icon(Icons.check, color: Colors.white)
            : isBroken
            ? const Text(
          '✗',
          style: TextStyle(
            color: Color(0xFFE5E7EB),
            fontWeight: FontWeight.w900,
            fontSize: 16,
          ),
        )
            : Text(
          label,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 11,
            fontWeight: FontWeight.w900,
          ),
        ),
      ),
    );
  }

  Widget _legendBar() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 8),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            _legendItem(_legendDot(const Color(0xFF374151)), 'Trống'),
            const SizedBox(width: 10),
            _legendItem(_legendDot(const Color(0xFFE11D48)), 'Đang chọn'),
            const SizedBox(width: 10),
            _legendItem(_legendDot(const Color(0xFF111827), icon: Icons.check), 'Đã đặt'),
            const SizedBox(width: 10),
            _legendItem(_legendDot(const Color(0xFF0B1220), text: '✗'), 'Ghế hỏng'),
            const SizedBox(width: 10),
            _legendItem(_legendPill(const Color(0xFFB453E6)), 'Ghế đôi'),
            const SizedBox(width: 10),
            _legendItem(_legendDot(const Color(0x00000000), borderOnly: true), 'Lối đi/không ghế'),
            const SizedBox(width: 10),
            _legendItem(_legendDot(const Color(0xFFF59E0B), vip: true), 'VIP'),
          ],
        ),
      ),
    );
  }

  Widget _legendItem(Widget icon, String text) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: const Color(0xFF0E1728),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: const Color(0xFF1F2A44)),
      ),
      child: Row(
        children: [
          icon,
          const SizedBox(width: 8),
          Text(
            text,
            style: const TextStyle(
              color: Color(0xFFE5E7EB),
              fontWeight: FontWeight.w800,
              fontSize: 12,
            ),
          ),
        ],
      ),
    );
  }

  Widget _legendDot(
      Color bg, {
        IconData? icon,
        bool borderOnly = false,
        String? text,
        bool vip = false,
      }) {
    final border = vip ? const Color(0xFFFBBF24) : Colors.white.withOpacity(.14);
    return Container(
      width: 22,
      height: 22,
      decoration: BoxDecoration(
        color: borderOnly ? Colors.transparent : bg,
        borderRadius: BorderRadius.circular(7),
        border: Border.all(color: border, width: vip ? 1.4 : 1),
      ),
      alignment: Alignment.center,
      child: icon != null
          ? Icon(icon, size: 14, color: Colors.white)
          : (text != null
          ? Text(
        text,
        style: const TextStyle(
          color: Color(0xFFE5E7EB),
          fontWeight: FontWeight.w900,
          fontSize: 12,
        ),
      )
          : const SizedBox.shrink()),
    );
  }

  Widget _legendPill(Color bg) {
    return Container(
      width: 30,
      height: 22,
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: Colors.white.withOpacity(.12)),
      ),
    );
  }

  int _selectedSeatCount() => selectedSeatIds.length;

  int _selectedSeatTotal() {
    int total = 0;
    for (final id in selectedSeatIds) {
      final s = seatById[id];
      if (s != null) total += s.price;
    }
    return total;
  }


  Widget _bottomBarSimple() {
    final total = _selectedSeatTotal();

    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
        decoration: const BoxDecoration(
          color: Color(0xFF0E1728),
          border: Border(top: BorderSide(color: Color(0xFF1F2A44))),
        ),
        child: Row(
          children: [
            // FIX: không để Expanded ăn hết width -> dùng Flexible + ellipsis
            Flexible(
              child: Text(
                '${_selectedSeatCount()} ghế | Tổng tiền: ${_formatVnd(total)}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Color(0xFFE5E7EB),
                  fontWeight: FontWeight.w900,
                  fontSize: 13,
                ),
              ),
            ),
            const SizedBox(width: 10),

            // FIX: nút có minWidth + padding + text rõ
            ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 44, minWidth: 120),
              child: ElevatedButton.icon(
                onPressed: selectedSeatIds.isEmpty
                    ? null
                    : () {
                  Navigator.pushNamed(
                    context,
                    AppRoutes.snacks,
                    arguments: {
                      'movieTitle': widget.movieTitle,
                      'showtimeId': widget.showtimeId,
                      'selectedSeatIds': selectedSeatIds.toList(),
                      'seatTotal': total,
                    },
                  );
                },
                icon: const Icon(Icons.fastfood, size: 18),
                label: const Text('Chọn snack'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF8A2BE2),
                  foregroundColor: Colors.white,
                  disabledBackgroundColor: const Color(0xFF374151),
                  disabledForegroundColor: const Color(0xFF9CA3AF),
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                  shape: const StadiumBorder(),
                  textStyle: const TextStyle(
                    fontWeight: FontWeight.w900,
                    fontSize: 13,
                    letterSpacing: .2,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _formatVnd(int v) {
    final s = v.toString();
    final buf = StringBuffer();
    for (int i = 0; i < s.length; i++) {
      final idxFromEnd = s.length - i;
      buf.write(s[i]);
      if (idxFromEnd > 1 && idxFromEnd % 3 == 1) buf.write('.');
    }
    return '${buf.toString()} đ';
  }

  void _toggleSingle(_Seat s) {
    setState(() {
      if (selectedSeatIds.contains(s.seatId)) {
        selectedSeatIds.remove(s.seatId);
      } else {
        selectedSeatIds.add(s.seatId);
      }
    });
  }

  void _toggleCouple(_Seat a, _Seat b) {
    setState(() {
      final hasBoth = selectedSeatIds.contains(a.seatId) && selectedSeatIds.contains(b.seatId);
      if (hasBoth) {
        selectedSeatIds.remove(a.seatId);
        selectedSeatIds.remove(b.seatId);
      } else {
        selectedSeatIds.add(a.seatId);
        selectedSeatIds.add(b.seatId);
      }
    });
  }
}

class _TwoAxisSeatScroller extends StatefulWidget {
  final EdgeInsets padding;
  final Widget Function(BuildContext, ScrollController) horizontalBuilder;
  final Widget Function(BuildContext, ScrollController, Widget) verticalBuilder;

  const _TwoAxisSeatScroller({
    required this.padding,
    required this.horizontalBuilder,
    required this.verticalBuilder,
  });

  @override
  State<_TwoAxisSeatScroller> createState() => _TwoAxisSeatScrollerState();
}

class _TwoAxisSeatScrollerState extends State<_TwoAxisSeatScroller> {
  final ScrollController _v = ScrollController();
  final ScrollController _h = ScrollController();

  @override
  void dispose() {
    _v.dispose();
    _h.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final child = Padding(
      padding: widget.padding,
      child: widget.horizontalBuilder(context, _h),
    );

    return widget.verticalBuilder(context, _v, child);
  }
}

class _Seat {
  final bool isDeleted;
  final String seatId;
  final String rowIndex;
  final int columnIndex;
  final String label;

  final bool isBooked;
  final bool isAisle;
  final String pairId;

  final bool isActive;
  final bool isVip;
  final bool isCouple;
  final int price;

  final bool isValid;

  _Seat({
    required this.isDeleted,
    required this.seatId,
    required this.rowIndex,
    required this.columnIndex,
    required this.label,
    required this.isBooked,
    required this.isAisle,
    required this.pairId,
    required this.isActive,
    required this.isVip,
    required this.isCouple,
    required this.price,
    required this.isValid,
  });

  factory _Seat.empty() => _Seat(
    seatId: '',
    rowIndex: '',
    columnIndex: 0,
    label: '',
    isBooked: false,
    isAisle: false,
    pairId: '',
    isActive: true,
    isVip: false,
    isCouple: false,
    price: 0,
    isValid: false,
    isDeleted: false,

  );

  factory _Seat.fromApi(Map<String, dynamic> m) {
    final seatId = (m['seatId'] ?? m['SeatId'] ?? '').toString().trim();
    final row = (m['rowLabel'] ?? m['rowIndex'] ?? m['RowIndex'] ?? '').toString().trim();
    final col = _tryInt(m['colIndex'] ?? m['columnIndex'] ?? m['ColumnIndex']) ?? 0;

    final isAisle = _tryBool(m['isAisle'] ?? m['IsAisle']) ?? false;
    final pairId = (m['pairId'] ?? m['PairId'] ?? '').toString().trim();

    final status = (m['status'] ?? m['Status'] ?? '').toString().toLowerCase().trim();
    final bookedByStatus = status == 'booked' || status == 'paid' || status == '2';
    final bookedByBool = _tryBool(m['isBooked'] ?? m['IsBooked']) ?? false;
    final isBooked = bookedByStatus || bookedByBool;

    final isActive = _tryBool(m['isActive'] ?? m['IsActive']) ?? true;

    final isVipBool = _tryBool(m['isVip'] ?? m['isVIP'] ?? m['IsVIP']) ?? false;
    final seatTypeName = (m['seatTypeName'] ?? m['SeatTypeName'] ?? '').toString();
    final isVipByName = seatTypeName.toUpperCase().trim() == 'VIP';
    final isVip = isVipBool || isVipByName;

    final isCoupleBool = _tryBool(m['isCouple'] ?? m['IsCouple']) ?? false;
    final isCouple = isCoupleBool || pairId.isNotEmpty;
    final isDeleted =
        _tryBool(m['isDeleted'] ?? m['IsDeleted'] ?? m['deleted'] ?? m['Deleted']) ?? false;

    final price = (_tryInt(m['finalPrice']) ??
        _tryInt(m['seatTypePrice']) ??
        _tryInt(m['SeatTypePrice']) ??
        _tryInt(m['basePrice']) ??
        0)
        .toInt();

    final rawLabel = (m['label'] ?? m['Label'] ?? '').toString().trim();
    final label = rawLabel.isNotEmpty ? rawLabel : '$row$col';

    return _Seat(
      seatId: seatId,
      rowIndex: row,
      columnIndex: col,
      label: label,
      isBooked: isBooked,
      isAisle: isAisle,
      pairId: pairId,
      isActive: isActive,
      isVip: isVip,
      isCouple: isCouple,
      price: price,
      isDeleted: isDeleted,
      isValid: seatId.isNotEmpty && row.isNotEmpty && col > 0 && !isDeleted,
    );
  }
}

int? _tryInt(dynamic v) {
  if (v == null) return null;
  if (v is int) return v;
  return int.tryParse(v.toString());
}

bool? _tryBool(dynamic v) {
  if (v == null) return null;
  if (v is bool) return v;
  final s = v.toString().toLowerCase().trim();
  if (s == 'true' || s == '1') return true;
  if (s == 'false' || s == '0') return false;
  return null;
}
