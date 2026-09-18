import 'package:flutter/material.dart';

import '../Repositories/user_repository.dart';

class AdministrationPanel extends StatefulWidget {
  const AdministrationPanel({super.key});

  @override
  _AdministrationPanelState createState() => _AdministrationPanelState();
}

const _roles = ['admin', 'cleaner', 'technician'];

class _AdministrationPanelState extends State<AdministrationPanel> {
  final _repo = UserRepository();
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _creating = false;

  List<AdminUser> _users = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _loadUsers();
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _createAccount() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _creating = true);
    final res = await _repo.register(_emailController.text, _passwordController.text);
    if (!mounted) return;
    setState(() => _creating = false);

    if (res.statusCode == 200) {
      _emailController.clear();
      _passwordController.clear();
      _loadUsers();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            res.statusCode == 409 ? 'Email already registered' : 'Could not create account',
          ),
        ),
      );
    }
  }

  Future<void> _loadUsers() async {
    setState(() => _loading = true);
    final res = await _repo.fetchUsers();
    if (!mounted) return;
    if (res.statusCode == 200) {
      setState(() {
        _users = _repo.parseUsers(res.body);
        _loading = false;
      });
    } else {
      setState(() => _loading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Could not load users (${res.statusCode})')),
      );
    }
  }

  Future<void> _editEmail(AdminUser user) async {
    final controller = TextEditingController(text: user.email);
    final newEmail = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Edit email'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(labelText: 'Email'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, controller.text),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    if (newEmail == null || newEmail == user.email) return;

    final res = await _repo.updateEmail(user.id, newEmail);
    if (!mounted) return;
    if (res.statusCode == 200) {
      _loadUsers();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            res.statusCode == 409 ? 'Email already in use' : 'Could not update email',
          ),
        ),
      );
    }
  }

  Future<void> _editPassword(AdminUser user) async {
    final controller = TextEditingController();
    final newPassword = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Change password for ${user.email}'),
        content: TextField(
          controller: controller,
          obscureText: true,
          decoration: const InputDecoration(labelText: 'New password'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, controller.text),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    if (newPassword == null || newPassword.isEmpty) return;

    final res = await _repo.updatePassword(user.id, newPassword);
    if (!mounted) return;
    if (res.statusCode != 200) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not update password')),
      );
    }
  }

  Future<void> _editRole(AdminUser user) async {
    final newRole = await showDialog<String>(
      context: context,
      builder: (context) => SimpleDialog(
        title: Text('Change role for ${user.email}'),
        children: _roles
            .map((role) => SimpleDialogOption(
                  onPressed: () => Navigator.pop(context, role),
                  child: Text(role),
                ))
            .toList(),
      ),
    );
    if (newRole == null || newRole == user.role) return;

    final res = await _repo.updateRole(user.id, newRole);
    if (!mounted) return;
    if (res.statusCode == 200) {
      _loadUsers();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not update role')),
      );
    }
  }

  Future<void> _deleteUser(AdminUser user) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete account'),
        content: Text('Delete ${user.email}? This cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    final res = await _repo.deleteUser(user.id);
    if (!mounted) return;
    if (res.statusCode == 200) {
      _loadUsers();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not delete account')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Administration')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('Create account', style: textTheme.headlineSmall),
                const SizedBox(height: 8),
                Text(
                  'Set up staff access to manage rooms.',
                  style: textTheme.bodyMedium,
                ),
                const SizedBox(height: 24),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          TextFormField(
                            controller: _emailController,
                            decoration: const InputDecoration(
                              labelText: 'Email',
                            ),
                            validator: (value) =>
                                (value == null || value.isEmpty) ? 'Required' : null,
                          ),
                          const SizedBox(height: 16),
                          TextFormField(
                            controller: _passwordController,
                            decoration: const InputDecoration(
                              labelText: 'Password',
                            ),
                            obscureText: true,
                            validator: (value) =>
                                (value == null || value.isEmpty) ? 'Required' : null,
                          ),
                          const SizedBox(height: 24),
                          ElevatedButton(
                            onPressed: _creating ? null : _createAccount,
                            child: _creating
                                ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Text('Sign Up'),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 32),
                Text('Users', style: textTheme.headlineSmall),
                const SizedBox(height: 8),
                if (_loading)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 24),
                    child: Center(child: CircularProgressIndicator()),
                  )
                else if (_users.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 16),
                    child: Text('No other users.'),
                  )
                else
                  Card(
                    child: ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _users.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
                      itemBuilder: (context, i) {
                        final user = _users[i];
                        return ListTile(
                          title: Text(user.email),
                          subtitle: Text(user.role),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              IconButton(
                                icon: const Icon(Icons.edit),
                                tooltip: 'Edit email',
                                onPressed: () => _editEmail(user),
                              ),
                              IconButton(
                                icon: const Icon(Icons.lock_reset),
                                tooltip: 'Change password',
                                onPressed: () => _editPassword(user),
                              ),
                              IconButton(
                                icon: const Icon(Icons.badge),
                                tooltip: 'Change role',
                                onPressed: () => _editRole(user),
                              ),
                              IconButton(
                                icon: const Icon(Icons.delete, color: Colors.red),
                                tooltip: 'Delete account',
                                onPressed: () => _deleteUser(user),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
