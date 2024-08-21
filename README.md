# AcMgdLib
Extension Library for managed AutoCAD.NET development

*** Important Note: This library requires C# 10.0 or later, 
see below for more ***

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
targeting releases of AutoCAD prior to 2025). Note that not all code in
the library requires C# 10.0, but there are no details on that.

A project targeting .NET Framework 4.x can be set to use C# 10 by
adding the `<LangVersion>10</LangVersion>` entry to .csproj file:

```
  <PropertyGroup>
     <TargetFramework>net4.7</TargetFramework>
     <LangVersion>10</LangVersion>
     <ImplicitUsings>disable</ImplicitUsings>
     <Nullable>disable</Nullable>
  </PropertyGroup>
```
While this project is dependent on C# 10.0, it does not use any of
the features of that langauge version that have a dependence on more-
recent framework versions (e.g., indexes and ranges), so it is safe
to use C# 10.0 with earlier framework versions, as long as the code
doesn't use features that are dependent on more-recent versions of
the framework.

If using this library with later framework versions, note that it 
requires `<Nullable>disable</Nullable>` which is because the code was
ported from older language versions and has not undergone the heavy
refactoring required to enable nullable support. Another reason is
to allow the code to remain compatible with older language versions
to the greatest extent possible. 

The roadmap for this library includes support for nullable and 
implicit usings at some point in the future.

The best strategy for consuming this library is to download it, add
it to a new project that targets the framework version used by the
AutoCAD releases that are targeted, with the language version set to
C# 10.0, and build a DLL that can be referenced from whatever projects
use the library. This will allow one to avoid the need to change the
language version of projects that consume this library, since it is
already compiled into a separate assembly that can be referenced into
projects that need to use it, without having to change the language
version is those projects. If you are also targeting AutoCAD 2025 or 
later in addition to older releases, will need to build a separate 
build of the library assembly for AutoCAD 2025 or later, which uses
.NET 8.0.
