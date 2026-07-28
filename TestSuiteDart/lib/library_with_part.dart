// A library split across two files. The declarations of the part must be folded into this
// library's namespace - a part has no library of its own, and Dart's privacy model treats
// both files as one unit.

import 'members.dart';

part 'parts/ledger_part.dart';

class Bookkeeper {
  final Ledger ledger = Ledger();

  int total() => _sumBalances(ledger);
}
