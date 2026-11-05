# Pure.DI: a nested delegate returns the argument of an outer delegate instead of creating its result

Minimal repro for Pure.DI master (d3440f5), compared with 2.5.7. A singleton has its own `Func<string, IContext>`. When the singleton is reached inside an outer `Func<IContext, Persister>`, the singleton's delegate returns the outer `IContext` argument instead of creating a new `Context`. Depending on where the singleton is reached first, master fails with CS0103 `overriddenIContext`, or compiles and prints the wrong context.

[`ISSUE.md`](ISSUE.md) is the text to paste into a Pure.DI GitHub issue.

```
dotnet build                                                                                   # 2.5.7 from nuget.org
dotnet build -p:PureDIVersion=<master build version> -p:RestoreAdditionalProjectSources=<folder with the package>
dotnet build -p:Variant=SWAPPED                                                                # App's constructor parameters swapped
dotnet build -p:Variant=INSIDE_ONLY                                                            # the singleton is only reached inside the delegate
```

| variant | 2.5.7 | master d3440f5 |
|---|---|---|
| default | prints `c a b` | CS0103 `overriddenIContext` |
| `SWAPPED` | prints `c a b` | prints `c a b` |
| `INSIDE_ONLY` (expected `a b`) | DIE026 "PerBlock lifetime does not support cyclic dependencies" | prints `a a` |

The generated composition is written under `obj/Debug/net10.0/generated/Pure.DI`.
