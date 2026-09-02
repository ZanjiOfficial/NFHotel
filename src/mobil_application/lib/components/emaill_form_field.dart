import 'package:flutter/material.dart';

class EmailFormField extends StatelessWidget {
  final ValueChanged<String> onChanged;
  final String? currentValue;
  final FormFieldValidator<String>? validator;
  final FocusNode? focusNode;
  final FocusNode? nextFocusNode;

  const EmailFormField({
    required this.onChanged,
    this.currentValue,
    this.validator,
    this.focusNode,
    this.nextFocusNode,
    Key? key,
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      onChanged: onChanged,
      initialValue: currentValue,
      keyboardType: TextInputType.emailAddress,
      validator: validator,
      decoration: InputDecoration(
        hintText: 'example@example.com',
        labelText: 'Email',
        helperText: "",
      ),
      focusNode: focusNode,
      onFieldSubmitted: (value) {
        if (nextFocusNode != null) {
          FocusScope.of(context).requestFocus(nextFocusNode);
        }
      },
    );
  }
}
