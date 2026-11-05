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

#if INSIDE_ONLY
// The singleton is only reached inside the delegate
public sealed class App(Func<IContext, Persister> persisterFactory)
{
    public override string ToString() => persisterFactory(new Context("a")).ToString();
}
#elif SWAPPED
// The singleton is reached inside the delegate before it is reached directly
public sealed class App(Func<IContext, Persister> persisterFactory, ContextProvider provider)
{
    public override string ToString() => $"{provider.Open("c")} {persisterFactory(new Context("a"))}";
}
#else
// The singleton is reached directly before it is reached inside the delegate
public sealed class App(ContextProvider provider, Func<IContext, Persister> persisterFactory)
{
    public override string ToString() => $"{provider.Open("c")} {persisterFactory(new Context("a"))}";
}
#endif

partial class Composition
{
    private static void Setup() => DI.Setup(nameof(Composition))
        .Bind<IContext>().To<Context>()
        .Bind().As(Singleton).To<ContextProvider>()
        .Root<App>("Root");
}
