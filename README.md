Quack
=====

A very simple duck-typing framework for .NET

Suppose you have a class `Foo` that you don't own:

```
public class Foo
{
    public int Id { get; }
    public string Name { get; }

    public void Frob() { ... }
}
```

An interface `IFoo` with the same members:

```
public interface IFoo
{
    int Id { get; }
    string Name { get; }

    void Frob();
}
```

And a method `UseFoo` that takes an `IFoo`:

```
public void UseFoo(IFoo foo) { ... }
```

Since `Foo` doesn't implement `IFoo`, you can't pass it to `UseFoo`, even though it has all the correct members...
That's where [duck typing](http://en.wikipedia.org/wiki/Duck_typing) comes into play. C# doesn't natively support
duck typing, but Quack lets you do this:


```
Foo foo = ...
IFoo proxy = foo.DuckTypeAs<IFoo>();
UseFoo(proxy)
```

**Disclaimer**: this is a *very* early version, isn't fully tested (far from it), and has a few limitations:

- it can only duck-type to an interface, not an abstract class
- events are not supported yet
- the signatures in the interface and the target type have to match exactly
- both the interface and the target type have to be public
