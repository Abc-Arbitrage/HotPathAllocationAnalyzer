Hot Path Allocation Analyzer
===================================

Roslyn Analyzer detecting heap allocation in *hot path* (based on https://github.com/microsoft/RoslynClrHeapAllocationAnalyzer) 

Detect in hot path:  
 - explicit allocation 
 - implicit allocations 
    - boxing 
    - display classes a.k.a closures 
    - implicit delegate creations
    - ...

## Hot path

Hot path should be flagged as so using the `NoAllocation` attribute. This attribute indicate that the analyzer should run on a method.

It also forbid calling a method/property that is considered unsafe. 

Methods/Properties are considered safe when flagged for analysis (`NoAllocation`), flagged for ignore (`IgnoreAllocation`), whitelisted or in a safe scope.
Properties are also considered safe when they are auto property. 

Safe scope are defined as:
```cs
[NoAllocation]
public int Something(string str) 
{
    using var safeScope = new AllocationFreeScope();

    // this is safe because it's in an AllocationFreeScope
    return str.Length;
}
```

## Setup

Install the nuget package `HotPathAllocationAnalyzer` on the project to analyze

## Whitelisting

Whitelisting can be used to mark third party and system methods as safe.

1. To create a whitelist, you need to create a folder called `HotPathAllocationAnalyzer` at the root of your solution. 
2. This folder should contain a `csproj` (name does not matter) and reference the nuget `HotPathAllocationAnalyzer.Configuration`
3. You can then add some `class` in the project that implement the `AllocationConfiguration` class and define some method listing whitelisted methods:

```cs
public class TestConfiguration : AllocationConfiguration
{
    public void WhitelistString(string str)
    {
        MakeSafe(() => str.Length);
        MakeSafe(() => str.IsNormalized());
        MakeSafe(() => str.Contains(default));
    }

    public void WhitelistNullable<T>(T? arg)
        where T: struct
    {
        MakeSafe(() => arg.Value); 
        MakeSafe(() => arg.HasValue); 
    }
}
```  