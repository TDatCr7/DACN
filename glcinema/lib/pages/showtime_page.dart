// lib/pages/showtime_page.dart
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

const String baseUrl = "http://10.0.2.2:5080/api";

class ShowtimePage extends StatefulWidget {
  static const routeName = '/showtimes';

  final String movieId;
  final String movieTitle;

  const ShowtimePage({
    super.key,
    required this.movieId,
    required this.movieTitle,
  });

  @override
  State<ShowtimePage> createState() => _ShowtimePageState();
}

class _ShowtimePageState extends State<ShowtimePage> {
  List<Map<String, dynamic>> showtimes = [];
  bool isLoading = true;

  @override
  void initState() {
    super.initState();
    _fetchShowtimes();
  }

  Future<void> _fetchShowtimes() async {
    try {
      final url = Uri.parse('$baseUrl/movies/${widget.movieId}/showtimes');
      final res = await http.get(url);

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        setState(() {
          showtimes = (data as List)
              .map((e) => Map<String, dynamic>.from(e))
              .toList();
          isLoading = false;
        });
      } else {
        setState(() => isLoading = false);
      }
    } catch (_) {
      setState(() => isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        title: Text(widget.movieTitle),
        backgroundColor: Colors.purple,
        centerTitle: true,
      ),
      body: isLoading
          ? const Center(
        child: CircularProgressIndicator(color: Colors.purple),
      )
          : showtimes.isEmpty
          ? const Center(
        child: Text(
          "Không có suất chiếu nào.",
          style: TextStyle(color: Colors.white70),
        ),
      )
          : ListView(
        padding: const EdgeInsets.all(16),
        children: _buildCards(),
      ),
    );
  }

  List<Widget> _buildCards() {
    // group theo rạp + phòng
    final Map<String, List<Map<String, dynamic>>> grouped = {};
    for (var s in showtimes) {
      final key =
          "${s['cinemaName'] ?? 'Rạp'} - ${s['screenName'] ?? 'Phòng'}";
      grouped.putIfAbsent(key, () => []).add(s);
    }

    return grouped.entries.map((e) {
      final shows = e.value
        ..sort((a, b) =>
            a['startTime'].toString().compareTo(b['startTime'].toString()));

      return Card(
        color: const Color(0xFF1E1E1E),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                e.key,
                style: const TextStyle(
                    color: Colors.white, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 10),
              Wrap(
                spacing: 10,
                runSpacing: 10,
                children: shows.map((s) {
                  final showId = s['showTimeId'].toString();
                  final time =
                  s['startTime'].toString().substring(11, 16); // HH:mm

                  return ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.purple,
                    ),
                    onPressed: () {
                      // ✅ Dùng tên route đã đăng ký trong main.dart
                      Navigator.pushNamed(
                        context,
                        '/seats',
                        arguments: {
                          'movieTitle': widget.movieTitle,
                          'showtimeId': showId,
                        },
                      );
                    },
                    child: Text(
                      time,
                      style: const TextStyle(color: Colors.white),
                    ),
                  );
                }).toList(),
              ),
            ],
          ),
        ),
      );
    }).toList();
  }
}
