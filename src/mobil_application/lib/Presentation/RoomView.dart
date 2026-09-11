import 'package:flutter/material.dart';
import 'package:mobil_application/Model/Enums/room_status.dart';
import 'package:mobil_application/Model/roomModel.dart';
import 'package:mobil_application/Controllers/roomController.dart';
import 'package:mobil_application/Widgets/StatusIcon.dart';

class RoomView extends StatelessWidget {
  final RoomModel room;

  const RoomView({super.key, required this.room});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Room ${room.number}')),
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
                onTap: () => roomController.setRoomStatus(room, status),
              ),
          ],
        ),
      ),
    );
  }
}
