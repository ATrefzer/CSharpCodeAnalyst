using System;
using System.Collections.Generic;

namespace Core.Generics;

// Test generic methods
public class GenericMethodsClass
{
    // Simple generic method
    public static T Identity<T>(T value)
    {
        return value;
    }

    // Generic method with constraints
    public static T CreateInstance<T>() where T : new()
    {
        return new T();
    }

    // Multiple type parameters
    public static TResult Transform<TSource, TResult>(TSource source, Func<TSource, TResult> transformer)
    {
        return transformer(source);
    }

    // Generic method with class constraint
    public static bool AreEqual<T>(T first, T second) where T : class
    {
        return ReferenceEquals(first, second) || (first?.Equals(second) ?? false);
    }

    // Generic method with interface constraint
    public static void ProcessItems<T>(IEnumerable<T> items) where T : IProcessor
    {
        foreach (var item in items)
        {
            item.Process();
        }
    }

    // Generic method with inheritance constraint
    public static void SaveEntities<T>(IEnumerable<T> entities) where T : BaseEntity
    {
        foreach (var entity in entities)
        {
            entity.Save();
        }
    }

    // Extension-like generic method
    public static List<TResult> ConvertAll<TSource, TResult>(
        IEnumerable<TSource> sources,
        Func<TSource, TResult> converter)
    {
        var results = new List<TResult>();
        foreach (var source in sources)
        {
            results.Add(converter(source));
        }

        return results;
    }

    // Generic method with out parameter
    public static bool TryParse<T>(string input, out T result) where T : new()
    {
        try
        {
            result = (T)Convert.ChangeType(input, typeof(T));
            return true;
        }
        catch
        {
            result = new T();
            return false;
        }
    }

    // Generic method with ref parameter
    public static void Swap<T>(ref T first, ref T second)
    {
        var temp = first;
        first = second;
        second = temp;
    }
}

// Generic methods in generic class
public class GenericService<TEntity> where TEntity : class
{
    private readonly List<TEntity> _entities = new();

    public void Add(TEntity entity)
    {
        _entities.Add(entity);
    }

    // Generic method with additional type parameter
    public TResult ProcessEntity<TResult>(TEntity entity, Func<TEntity, TResult> processor)
    {
        return processor(entity);
    }

    // Generic method with constraint on method type parameter
    public void ProcessWithValidator<TValidator>(TEntity entity, TValidator validator)
        where TValidator : IValidator<TEntity>
    {
        if (validator.IsValid(entity))
        {
            Console.WriteLine("Entity is valid");
        }
    }

    // Generic method returning generic type
    public GenericContainer<TResult> WrapResult<TResult>(TResult result)
    {
        return new GenericContainer<TResult>(result);
    }
}

// Interface for testing generic method constraints
public interface IValidator<T>
{
    bool IsValid(T item);
}

// Implementation for testing
public class StringValidator : IValidator<string>
{
    public bool IsValid(string item)
    {
        return !string.IsNullOrEmpty(item);
    }
}

public class NumberValidator : IValidator<int>
{
    public bool IsValid(int item)
    {
        return item >= 0;
    }
}

// Static generic class
public static class GenericUtilities
{
    public static bool IsNull<T>(T value) where T : class
    {
        return value == null;
    }

    public static T GetDefault<T>()
    {
        return default!;
    }

    public static Type GetGenericType<T>()
    {
        return typeof(T);
    }

    public static GenericPair<T, U> MakePair<T, U>(T first, U second)
    {
        return new GenericPair<T, U>(first, second);
    }
}