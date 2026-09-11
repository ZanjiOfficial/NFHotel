import 'package:flutter/material.dart';
import 'package:mobil_application/Model/roomModel.dart';


class RoomTile extends StatelessWidget {
  final RoomModel room;
  final VoidCallback? onTap;

  const RoomTile({super.key, required this.room, this.onTap});

  @override
  Widget build(BuildContext context) {
    final icon = room.status.icon;
    return Material(
      color: Colors.grey.shade200,
      borderRadius: BorderRadius.circular(12),
      clipBehavior: Clip.antiAlias,
      elevation: 2,
      child: InkWell(
        onTap: onTap,
        child: Stack(
          children: [
            if (icon != null)
              Positioned.fill(
                child: Opacity(
                  opacity: 0.25,
                  child: Image.asset(icon, fit: BoxFit.cover),
                ),
              ),
            Container(
              padding: const EdgeInsets.all(8),
              alignment: Alignment.center,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    'Room ${room.number}',
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    room.status.label,
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 12),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
