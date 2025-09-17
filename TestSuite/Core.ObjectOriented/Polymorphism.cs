using System;
using System.Collections.Generic;

namespace Core.ObjectOriented;

// Test polymorphism and virtual method dispatch
public class Animal
{
    public string Name { get; protected set; } = string.Empty;

    public virtual void MakeSound()
    {
        Console.WriteLine($"{Name} makes a generic sound");
    }

    public virtual void Move()
    {
        Console.WriteLine($"{Name} moves around");
    }

    public void Eat()
    {
        Console.WriteLine($"{Name} is eating");
    }
}

public class Dog : Animal
{
    public Dog(string name)
    {
        Name = name;
    }

    public override void MakeSound()
    {
        Console.WriteLine($"{Name} barks: Woof!");
    }

    public override void Move()
    {
        Console.WriteLine($"{Name} runs on four legs");
    }

    public void Fetch()
    {
        Console.WriteLine($"{Name} fetches the ball");
    }
}

public class Cat : Animal
{
    public Cat(string name)
    {
        Name = name;
    }

    public override void MakeSound()
    {
        Console.WriteLine($"{Name} meows: Meow!");
    }

    public void Climb()
    {
        Console.WriteLine($"{Name} climbs the tree");
    }
}

public class Bird : Animal
{
    public Bird(string name)
    {
        Name = name;
    }

    public override void MakeSound()
    {
        Console.WriteLine($"{Name} chirps: Tweet!");
    }

    public override void Move()
    {
        Console.WriteLine($"{Name} flies through the air");
    }

    public void Fly(int altitude)
    {
        Console.WriteLine($"{Name} flies at {altitude} meters");
    }
}

// Polymorphic behavior tests
public class AnimalShelter
{
    private readonly List<Animal> _animals = new();

    public void AddAnimal(Animal animal)
    {
        _animals.Add(animal);
    }

    public void FeedAllAnimals()
    {
        foreach (var animal in _animals)
        {
            animal.Eat(); // Non-virtual method
            animal.MakeSound(); // Virtual method - polymorphic dispatch
            animal.Move(); // Virtual method - polymorphic dispatch
        }
    }

    public void WalkDogs()
    {
        foreach (var animal in _animals)
        {
            if (animal is Dog dog)
            {
                dog.Fetch(); // Dog-specific method
                dog.Move(); // Polymorphic call to Dog.Move()
            }
        }
    }

    public void CountAnimals()
    {
        var dogCount = 0;
        var catCount = 0;
        var birdCount = 0;
        var otherCount = 0;

        foreach (var animal in _animals)
        {
            switch (animal)
            {
                case Dog:
                    dogCount++;
                    break;
                case Cat:
                    catCount++;
                    break;
                case Bird:
                    birdCount++;
                    break;
                default:
                    otherCount++;
                    break;
            }
        }

        Console.WriteLine($"Dogs: {dogCount}, Cats: {catCount}, Birds: {birdCount}, Others: {otherCount}");
    }
}

// Abstract polymorphism
public abstract class Vehicle
{
    public abstract void Start();
    public abstract void Stop();

    public virtual void Honk()
    {
        Console.WriteLine("Generic vehicle horn");
    }
}

public class Car : Vehicle
{
    public override void Start()
    {
        Console.WriteLine("Car engine starts");
    }

    public override void Stop()
    {
        Console.WriteLine("Car engine stops");
    }

    public override void Honk()
    {
        Console.WriteLine("Car: Beep beep!");
    }
}

public class Motorcycle : Vehicle
{
    public override void Start()
    {
        Console.WriteLine("Motorcycle engine revs");
    }

    public override void Stop()
    {
        Console.WriteLine("Motorcycle engine dies down");
    }
}