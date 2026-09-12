import 'package:flutter/material.dart';
import 'package:mobil_application/Model/roomModel.dart';
import 'package:mobil_application/theme.dart';

class RoomTile extends StatelessWidget {
  final RoomModel room;
  final VoidCallback? onTap;

  const RoomTile({super.key, required this.room, this.onTap});

  @override
  Widget build(BuildContext context) {
    final icon = room.status.icon;
    final textTheme = Theme.of(context).textTheme;
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: AppColors.border),
          ),
          child: Stack(
            children: [
              if (icon != null)
                Positioned.fill(
                  child: Opacity(
                    opacity: 0.15,
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
                      style: textTheme.titleMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      room.status.label,
                      textAlign: TextAlign.center,
                      style: textTheme.bodyMedium?.copyWith(fontSize: 12),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
