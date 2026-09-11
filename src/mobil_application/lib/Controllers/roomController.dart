import 'package:flutter/foundation.dart';
import 'package:mobil_application/Model/Enums/room_status.dart';
import 'package:mobil_application/Model/roomModel.dart';
import 'package:flutter/material.dart';

class RoomController extends ChangeNotifier {
  final List<RoomModel> rooms = List.generate(
    12,
    (index) => RoomModel(number: index + 1, status: RoomStatus.clean),
  );

  void setRoomStatus(RoomModel room, RoomStatus status) {
    room.status = status;
    notifyListeners();
  }
}

final roomController = RoomController();
