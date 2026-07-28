// Sits two directories deep, so the namespace chain must be
// test_suite_dart -> features -> reporting -> report_builder.

import '../../members.dart';
import '../../types.dart';

/// Marks the relationship for an annotation (UsesAttribute).
class Important {
  const Important();
}

@Important()
class ReportBuilder extends AbstractBase with Greeting {
  ReportBuilder(this.accounts);

  /// A generic type argument must produce a Uses edge to Account, not only to List.
  final List<Account> accounts;

  @override
  void template() {}

  /// Direct calls and a constructor invocation.
  Account merge() {
    var result = Account.empty();
    for (final account in accounts) {
      result = result + account;
    }
    return result;
  }

  /// Everything inside the closure must become Uses instead of Calls - the closure body is
  /// not executed where it is written.
  List<int> balancesDeferred() {
    return accounts.map((account) {
      return account.balance;
    }).toList();
  }

  /// A tear-off references a method without calling it.
  ColorPicker get picker => _pickColor;

  Color _pickColor(int index) => Color.values[index % Color.values.length];

  /// Calls a method that only exists on the mixin.
  String describe() => greet();
}
