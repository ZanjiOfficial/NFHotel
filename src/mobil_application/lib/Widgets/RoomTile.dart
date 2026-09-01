import 'package:flutter/material.dart';

class RoomTile extends StatelessWidget {
  final String roomName;
  final VoidCallback onTap;

  RoomTile({required this.roomName, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Card(
        child: Center(
          child: Text(roomName),
        ),
      ),
    );
  }
}


