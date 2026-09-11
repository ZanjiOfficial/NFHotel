import 'package:flutter/foundation.dart';
import 'package:mobil_application/Model/Enums/roomStatus.dart';
import 'package:mobil_application/Model/roomModel.dart';
import 'package:flutter/material.dart';

class RoomController extends ChangeNotifier {
  final List<Room> rooms = List.generate(
    12,
    (index) => Room(number: index + 1, status: RoomStatus.clean),
  );

  void setRoomStatus(Room room, RoomStatus status) {
    room.status = status;
    notifyListeners();
  }
}

final roomController = RoomController();
