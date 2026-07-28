// One instance of every type-level construct Dart has, so the element mapping can be asserted.
// See Documentation/Dart/dart-import.md for the expected CodeElementType of each.

/// A plain class -> Class.
class PlainClass {
  int value = 0;
}

/// An abstract class still carries implementation, so it stays a Class.
abstract class AbstractBase {
  void template();

  void shared() {
    template();
  }
}

/// "interface class" cannot be extended, only implemented -> Interface.
interface class PureInterface {
  void contract() {}
}

/// "abstract interface class" is the idiomatic pure interface -> Interface.
abstract interface class Named {
  String get name;
}

/// A mixin has implementation and joins the superclass chain -> Class.
mixin Greeting {
  String greet() => 'hello';
}

/// The "on" constraint is a requirement on the user of the mixin -> Uses, not Inherits.
mixin CountingLog on AbstractBase {
  int count = 0;

  void log() {
    count++;
    shared();
  }
}

/// Extends + with + implements in one declaration:
/// Inherits AbstractBase, Inherits Greeting, Implements Named.
class Combined extends AbstractBase with Greeting implements Named {
  @override
  String get name => greet();

  @override
  void template() {}
}

/// -> Enum, with its constants as Field children.
enum Color {
  red,
  green,
  blue;

  bool get isWarm => this == Color.red;
}

/// A named extension is a container of methods -> Class.
extension StringPadding on String {
  String padBoth(int width) => padLeft(width).padRight(width);
}

/// An unnamed extension cannot be referenced by name and must be dropped entirely,
/// together with its members. It is only visible inside this library, hence the user below.
extension on int {
  int get doubled => this * 2;
}

/// Reads a member of the unnamed extension above. That member has no node in the graph, so the
/// reference must be counted as unresolved - not crash, and not resurrect an anonymous node.
int doubleIt(int value) => value.doubled;

/// A zero-cost wrapper over a representation type -> Struct.
extension type Meters(double value) {
  double get inCentimeters => value * 100;
}

/// A function typedef -> Delegate, plus a Uses edge to the aliased type.
typedef ColorPicker = Color Function(int index);

/// A non-function alias is a Delegate as well; the interesting part is the Uses edge.
typedef ColorList = List<Color>;
