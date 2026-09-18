import 'resources.dart';

enum UserRole {
  admin,
  cleaner,
  technician,
}

extension UserRoleX on UserRole {
  bool canEdit(Resource resource) {
    switch (this) {
      case UserRole.admin:
        return true;
      case UserRole.cleaner:
        return false;
      case UserRole.technician:
        return false;
    }
  }
}


