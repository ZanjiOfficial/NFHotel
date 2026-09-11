enum RoomStatus {
  clean('Clean', null),
  needsCleaning('Needs Cleaning', 'lib/Assets/ImageMop.png'),
  dailyCleaning('Daily Cleaning', 'lib/Assets/ImageBed.png'),
  needsMaintenance('Needs Maintenance', 'lib/Assets/ImageHammer.png');

  const RoomStatus(this.label, this.icon);

  final String label;
  final String? icon;
}
