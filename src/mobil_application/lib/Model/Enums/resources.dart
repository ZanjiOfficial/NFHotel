/// The kinds of things in the app that a [UserRole] can be checked against
/// for edit/view permissions. Adjust the values to match your domain.
enum Resource {
  room,
  booking,
  cleaningRecord,
  serviceRequest,
  user,
}
