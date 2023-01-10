Hot Path Allocation Analyzer
===================================

Roslyn Analyzer detecting heap allocation in *hot path* (based on https://github.com/microsoft/RoslynClrHeapAllocationAnalyzer) 

Detect in hot path:  
 - explicit allocations 
 - implicit allocations 
    - boxing 
    - display classes a.k.a closures 
    - implicit delegate creations
    - ...

## Hot path

Hot path should be flagged as so using the `NoAllocation` attribute. This attribute indicates that the analyzer should run on the method.

It also forbids calling a method/property that is considered may allocate.

Methods/Properties are considered safe (i.e. they don't allocate) when flagged for analysis (`NoAllocation`), flagged to be ignored (`IgnoreAllocation`), whitelisted or in a safe scope.
Properties are also considered safe when they are auto properties.

Safe scopes are defined as:
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

1. To create a whitelist, you need to create a folder called `Analyzers\HotPathAllocationAnalyzer` at the root of your solution. 
2. This folder should contain a `csproj` (name does not matter) and reference the nuget `HotPathAllocationAnalyzer.Configuration`
3. You can then add some `class` in the project that implements the `AllocationConfiguration` class and define a method listing the whitelisted methods:

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
    
    [MakeSafe]
    public void WhitelistSomeCalls()
    {
        Console.WriteLine("Hello");
    }
    
    public void WhiteListCustomStrinHandler()
    {
        MakeStringInterpolationSafe(typeof(MyCustomStringHandler))    
    }
}
```
4. Compile this project to generate the whitelist.txt file
5. In the analyzed projects add the whitelist.txt file as an additional file in the csproj or a targets file
with the syntax : 

``
<AdditionalFiles Include="path_to_whitelist.txt" Visible="false" />
``