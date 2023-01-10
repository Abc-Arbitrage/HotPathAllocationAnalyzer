# Testing your code

If you just want to run the analyzer on a repository without installing the package, you can use the manual tests TestProject.

If you want to use custom binaries for a project you can do the following : 

* Replace the nuget config with : 
```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="temp/packages" />
    <add key="repositoryPath" value="temp/packages" />
  </config>
  <packageSources>
    <clear />
    <add key="Integration" value="temp/nuget" />
  </packageSources>
</configuration>
```

 * Copy your nupkg files in temp/nuget at the root of your project

# Publishing

For the analyzer the package is automatically generated after each build thus :
```bash
dotnet build
```

Since the configuration is an exe, we need to include the bin folder in the nuget.
But the copy of the files is made before the compilation thus it must be done in 2 steps :
```bash
dotnet publish 
dotnet pack --no-build --no-restore
```