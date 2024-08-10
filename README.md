# AcMgdLib
Extension Library for managed AutoCAD.NET development

UPDATE: Some classes from other repos have been moved/merged
into AcMgdLib at https://github.com/ActivistInvestor/AcMgdLib.

The merged code has been updated with more documentation
and some minor bug fixes and changes to accomodate later
framework versions.

This library merges various components that were previously distributed
separately in various repositories, including parts of AcMgdUtility, 
AcDbLinq, and RibbonSupport into a single codebase, mainly because they 
are all dependent on common supporting code.

Additional components will be added regulary as they are documented.

Important Note:

This library requires C# varsion 10.0. You can specify C# 10.0 as the
language version in a project that targets the .NET framework 4.x (e.g.
targeting releases of AutoCAD prior to 2025).

A project targeting .NET Framework 4.x can be set to use C# 10 by
adding the `<LangVersion>10</LangVersion>` entry to .csproj file:

```
  <PropertyGroup>
     <TargetFramework>net4.7</TargetFramework>
     <LangVersion>10</LangVersion>
  </PropertyGroup>
```
