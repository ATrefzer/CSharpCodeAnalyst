namespace ParserGaps.PrimaryConstructors;

// KNOWN GAP: Primary constructors are not collected in phase 1 (only ConstructorDeclarationSyntax is).
// The generated positional properties are not collected either. Therefore the parameter types of
// positional records and primary constructors create no relationship at all.

public class Customer
{
}

public record OrderId(int Value);

// GAP: no relationship Order -> OrderId and no relationship Order -> Customer is created.
public record Order(OrderId Id, Customer Customer);

public class Warehouse
{
}

// GAP: no relationship Inventory -> Warehouse is created.
public class Inventory(Warehouse warehouse)
{
}
