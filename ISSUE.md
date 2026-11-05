# A nested delegate returns the argument of an outer delegate instead of creating its result (CS0103 or wrong instance)

**Pure.DI master (d3440f5)**, compared with **2.5.7**. .NET SDK 10.0.403, macOS. Repro project attached (`dotnet build`, `-p:Variant=SWAPPED`, `-p:Variant=INSIDE_ONLY`).

## Minimal code

```csharp
using Pure.DI;
using static Pure.DI.Lifetime;

var composition = new Composition();
Console.WriteLine(composition.Root);

public interface IContext;

public sealed class Context(string name) : IContext
{
    public override string ToString() => name;
}

// A singleton that creates contexts with its own delegate
public sealed class ContextProvider(Func<string, IContext> factory)
{
    public IContext Open(string name) => factory(name);
}

public sealed class Persister(IContext context, ContextProvider provider)
{
    public override string ToString() => $"{context} {provider.Open("b")}";
}

public sealed class App(ContextProvider provider, Func<IContext, Persister> persisterFactory)
{
    public override string ToString() => $"{provider.Open("c")} {persisterFactory(new Context("a"))}";
}

partial class Composition
{
    private static void Setup() => DI.Setup(nameof(Composition))
        .Bind<IContext>().To<Context>()
        .Bind().As(Singleton).To<ContextProvider>()
        .Root<App>("Root");
}
```

## Expected

The code compiles and prints `c a b`: `ContextProvider`'s own `Func<string, IContext>` always creates a new `Context`, whichever delegate the singleton is first reached from. 2.5.7 does this.

## Actual (master d3440f5)

```
Composition.g.cs(126,16): error CS0103: The name 'overriddenIContext' does not exist in the current context
Program.cs(48,27): warning DIW003: The binding is not used.
```

The singleton gets one `Ensure…Exists` function inside the `Func<IContext, Persister>` lambda and one at the root level. In both, the singleton's `Func<string, IContext>` returns the outer `IContext` argument instead of creating a `Context`. At the root level that variable is not in scope:

```csharp
EnsureContextProviderExists309d();
System.Func<global::IContext, global::Persister> localFactory1= new System.Func<global::IContext, global::Persister>((global::IContext localArg1) =>
{
    global::IContext overriddenIContext = localArg1;
    EnsureContextProviderExists309d1();

    void EnsureContextProviderExists309d1()
    {
        ...
        System.Func<string, global::IContext> localFactory2= new System.Func<string, global::IContext>((string localArg1) =>
        {
            string overriddenString = localArg1;
            return overriddenIContext;          // should be new Context(overriddenString)
        });
        _singletonContextProvider309d = new global::ContextProvider(localFactory2);
    }
    return new global::Persister(overriddenIContext, _singletonContextProvider309d);
});
return new global::App(_singletonContextProvider309d, localFactory1);

void EnsureContextProviderExists309d()
{
    ...
    System.Func<string, global::IContext> localFactory= new System.Func<string, global::IContext>((string localArg1) =>
    {
        string overriddenString = localArg1;
        return overriddenIContext;              // CS0103: no such variable at the root level
    });
    _singletonContextProvider309d = new global::ContextProvider(localFactory);
}
```

`IContext` → `Context` is reported as unused (DIW003), because no `Context` is ever created.

## Variants

| variant | 2.5.7 | master d3440f5 |
|---|---|---|
| as above | prints `c a b` | CS0103 `overriddenIContext` |
| `App(Func<IContext, Persister> persisterFactory, ContextProvider provider)` | prints `c a b` | prints `c a b` |
| `App(Func<IContext, Persister> persisterFactory)` only, expected `a b` | DIE026 "The PerBlock lifetime does not support cyclic dependencies" | compiles, prints `a a`, DIW003 |

With the parameters swapped, the singleton is first built outside the delegate and master is correct. When the singleton is only reached inside the delegate, master compiles without any error, but the singleton's delegate returns the first `Persister`'s context for every call. The singleton keeps that delegate for the lifetime of the composition. No DIW015 is reported.

The result of a nested `Func<…, T>` is the object that delegate creates, so an outer override of `T` should not replace it, just as it does not when the singleton is first built outside the delegate.
