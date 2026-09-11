enum RoomStatus {
  clean('Clean', Colors.green),
  needsCleaning('Needs Cleaning', Colors.yellow, 'Assets/ImageMop.png'),
  dailyCleaning('Daily Cleaning', Colors.blue, 'Assets/ImageBed.png'),
  needsMaintenance('Needs Maintenance', Colors.red, 'Assets/ImageMop.png'),

  const RoomStatus(this.label, this.color, this.icon);

  final String label;
  final Color color;
  final String icon;
}
