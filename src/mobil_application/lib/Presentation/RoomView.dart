import 'package:flutter/material.dart';
import 'package:mobil_application/Controllers/roomController.dart';
import 'package:mobil_application/Model/roomModel.dart';
import 'package:mobil_application/Widgets/RoomTile.dart';

class RoomPage extends StatelessWidget {
  final RoomModel room;
  const RoomPage({super.key, required this.room});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(room.number.toString())),
      body: ListenableBuilder(
        listenable: roomController,
        builder: (context, _) => ListView(
          children: [
            for (final status in RoomStatus.values)
              ListTile(
                leading: StatusIcon(status: status, size: 24),
                title: Text(status.label),
                selected: room.status == status,
                trailing: room.status == status
                    ? const Icon(Icons.check)
                    : null,
                onTap: () => roomController.setStatus(room, status),
              ),
          ],
        ),
      ),
    );
  }
}
