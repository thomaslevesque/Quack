Quack
=====

> *“If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck.”*

A very simple [duck-typing](http://en.wikipedia.org/wiki/Duck_typing) framework for .NET

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

Quack dynamically generates a proxy type that implements `IFoo` by calling the corresponding members in `Foo`. For better performance, the proxy type is generated once and cached per interface/target type couple.

**Disclaimer**: this is a *very* early version, isn't fully tested (far from it), and has a few limitations:

- it can only duck-type to an interface, not an abstract class
- events are not supported yet
- the signatures in the interface and the target type have to match exactly
- both the interface and the target type have to be public
- because it uses `Reflection.Emit`, it doesn't work with all .NET framework versions (e.g. it won't work in a PCL or a Windows Store app)
