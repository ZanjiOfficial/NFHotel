import 'package:flutter/material.dart';
import 'package:mobil_application/Model/Enums/room_status.dart';

class StatusIcon extends StatelessWidget {
  final RoomStatus status;
  final double size;
  const StatusIcon({super.key, required this.status, this.size = 32});

  @override
  Widget build(BuildContext context) {
    final path = status.icon;
    if (path == null) {
      return SizedBox(width: size, height: size);
    }
    return Image.asset(
      path,
      width: size,
      height: size,
      errorBuilder: (_, _, _) => Icon(Icons.broken_image, size: size),
    );
  }
}
