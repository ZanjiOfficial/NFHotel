import 'package:flutter/material.dart';

class NewUserScreen extends StatefulWidget {
  const NewUserScreen({super.key});

  @override
  _NewUserScreenState createState() => _NewUserScreenState();
}

class _NewUserScreenState extends State<NewUserScreen> {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('New User')),
      body: Center(
        child: Form(
          child: Column(
            children: [
              TextFormField(decoration: InputDecoration(labelText: 'Email')),
              TextFormField(
                decoration: InputDecoration(labelText: 'Password'),
                obscureText: true,
              ),
              ElevatedButton(onPressed: () {}, child: Text('Sign Up')),
            ],
          ),
        ),
      ),
    );
  }
}
