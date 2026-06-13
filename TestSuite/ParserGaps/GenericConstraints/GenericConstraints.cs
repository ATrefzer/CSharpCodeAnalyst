namespace ParserGaps.GenericConstraints;

// KNOWN GAP: Generic type-parameter constraints (where T : IFoo / where T : BaseClass) are not
// captured. AnalyzeInheritanceRelationships only looks at a type's own base type and interfaces,
// and AnalyzeMethodRelationships only at parameter and return types - never at
// ITypeParameterSymbol.ConstraintTypes. The constraint type is the ONLY reference to these types
// here, so without the constraint there is no relationship at all.

public interface IRepository
{
}

public class EntityBase
{
}

// GAP: no Uses relationship ConstrainedRepository -> IRepository (constraint only).
public class ConstrainedRepository<T> where T : IRepository
{
}

public class ConstraintUser
{
    // GAP: no Uses relationship Process -> EntityBase (constraint only).
    public void Process<T>(T item) where T : EntityBase
    {
    }
}
