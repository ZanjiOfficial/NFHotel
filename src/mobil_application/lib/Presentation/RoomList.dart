import 'package:flutter/material.dart';
import 'package:mobil_application/Controllers/roomController.dart';
import 'package:mobil_application/Presentation/RoomView.dart';

class RoomOverview extends StatelessWidget {
  const RoomOverview({super.key});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: roomController,
      builder: (context, _) => GridView.builder(
        padding: const EdgeInsets.all(16),
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 3,
          mainAxisSpacing: 12,
          crossAxisSpacing: 12,
        ),
        itemCount: roomController.rooms.length,
        itemBuilder: (context, i) => RoomTile(room: roomController.rooms[i]),
      ),
    );
  }
}
