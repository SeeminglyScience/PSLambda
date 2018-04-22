# PSLambda

PSLambda is a runtime compiler for PowerShell ScriptBlock objects. This project is in a very early
state and is not currently recommended for use in production environments.

This project adheres to the Contributor Covenant [code of conduct](https://github.com/SeeminglyScience/PSLambda/tree/master/docs/CODE_OF_CONDUCT.md).
By participating, you are expected to uphold this code. Please report unacceptable behavior to seeminglyscience@gmail.com.

## Features

- C# like syntax (due to being compiled from interpreted Linq expression trees) with some PowerShell
  convenience features built in. (See the "Differences from PowerShell" section)

- Run in threads without a `DefaultRunspace` as it does not actually execute any PowerShell code.

- Access and change variables local to the scope the delegate was created in (similar to closures in C#)

- Runs faster than PowerShell in most situations.

- Parse errors similar to the PowerShell parser for compiler errors, including extent of the error.

## Motivation

PowerShell is an excellent engine for exploration. When I'm exploring a new API, even if I intend to write
the actual project in C# I will do the majority of my exploration from PowerShell. Most of the time there
are no issues with doing that. Sometimes though, in projects that make heavy use of delegates you can run
into issues.

Yes the PowerShell engine can convert from `ScriptBlock` to any `Delegate` type, but it's just
a wrapper.  It still requires that `ScriptBlock` to be ran in a `Runspace` at some point. Sometimes
that isn't possible, or just isn't ideal. Mainly when it comes to API's that are mainly `async`/`await`
based.

I also just really like the idea of a more strict syntax in PowerShell without losing **too** much flavor,
so it was a fun project to get up and running.

## What would I use this for

For most folks the answer is probably nothing. This is pretty niche, if you haven't specifically wished
something like this existed in the past, it probably isn't applicable to you.

The reason cited in "Motivation" (interactive API exploration and prototyping) is the only real application
I am specifically planning for. If anyone has any intention of using this more broadly, I'd appreciate
it if you could open an issue or shoot me a DM so I can keep your scenario in mind.

## Installation

### Gallery

```powershell
Install-Module PSLambda -Scope CurrentUser
```

### Source

```powershell
git clone 'https://github.com/SeeminglyScience/PSLambda.git'
Set-Location .\PSLambda
Invoke-Build -Task Install -Configuration Release
```

## How

A custom `ICustomAstVisitor2` class visits each node in the abstract syntax tree of the ScriptBlock and
interprets it as a `System.Linq.Expressions.Expression`. Most of the PowerShell language features are
recreated from scratch as a Linq expression tree, using PowerShell API's where it makes sense to keep
some flavor. This is actually more or less what the PowerShell engine does as well, but it's obviously
still heavily reliant on the `Runspace`/`SessionState` system.

The `psdelegate` type acts as a stand in for the compiled delegate until it is interpreted. Using the
PowerShell type conversion system, if it is passed as an argument to a method that requires a specific
delegate type it is interpreted and compiled at method invocation.

## Usage

There's two main ways to use the module.

1. The `New-PSDelegate` command - this command will take a `ScriptBlock` and optionally a target `Delegate` type. With this method the delegate will be compiled immediately and will need to be recompiled if the delegate type needs to change.

1. The `psdelegate` type accelerator - you can cast a `ScriptBlock` as this type and it will retain the context until converted. This object can then be converted to a specific `Delegate` type later, either by explicittly casting the object as that type or implicitly as a method argument. Local variables are retained from when the `psdelegate` object is created. This method requires that the module be imported into the session as the type will not exist until it is.

### Create a psdelegate to pass to a method

```powershell
$a = 0
$delegate = { $a += 1 }
$actions = $delegate, $delegate, $delegate, $delegate
[System.Threading.Tasks.Parallel]::Invoke($actions)
$a
# 4
```

Creates a delegate that increments the local `PSVariable` "a", and then invokes it in a different thread
four times. Doing the same with `ScriptBlock` objects instead would result in an error stating the
the thread does not have a default runspace.

*Note*: Access to local variables from the `Delegate` is synced across threads for *some* thread safety,
but that doesn't effect anything accessing the variable directly so they are still *not* thread safe.

### Access all scope variables

```powershell
$delegate = New-PSDelegate { $ExecutionContext.SessionState.InvokeProvider.Item.Get("\") }
$delegate.Invoke()
#     Directory:
# Mode          LastWriteTime   Length Name
# ----          -------------   ------ ----
# d--hs-  3/32/2010   9:61 PM          C:\
```

### Use alternate C# esque delegate syntax

```powershell
$timer = [System.Timers.Timer]::new(1000)
$delegate = [psdelegate]{ ($sender, $e) => { $Host.UI.WriteLine($e.SignalTime.ToString()) }}
$timer.Enabled = $true
$timer.add_Elapsed($delegate)
Start-Sleep 10
$timer.remove_Elapsed($delegate)
```

Create a timer that fires an event every 1000 milliseconds, then add a compiled delegated as an event
handler. A couple things to note here.

1. Parameter type inference is done automatically during conversion. This is important because the
  compiled delegate is *not* dynamically typed like PowerShell is.
1. `psdelegate` objects that have been converted to a specific Delegate type are cached, so the instance
  is the same in both `add_Elapsed` and `remove_Elapsed`.

## Differences from PowerShell

While the `ScriptBlock`'s used in the examples look like (and for the most part are) valid PowerShell
syntax, very little from PowerShell is actually used. The abstract syntax tree (AST) is read and
interpreted into a `System.Linq.Expressions.Expression` tree. The rules are a lot closer to C# than
to normal PowerShell. There are some very PowerShell like things thrown in to make it feel a bit more
like PowerShell though.

### Supported PowerShell features

- All operators, including `like`, `match`, `split`, and all the case sensitive/insensitive comparision
  operators. These work mostly the exact same as in PowerShell due to using `LanguagePrimitives` under
  the hood.

- PowerShell conversion rules. Explicit conversion (e.g. `[int]$myVar`) is done using `LanguagePrimitives`.
  This allows for all of the PowerShell type conversions to be accessible from the compiled delegate.
  However, type conversion is *not* automatic like it often is in PowerShell. Comparision operators are
  the exception, as they also use `LanguagePrimitives`.

- Access to PSVariables.  Any variable that is either `AllScope` (like `$Host` and `$ExecutionContext`)
  is from the most local scope is available as a variable in the delegate. This works similar to
  closures in C#, allowing the current value to be read as well as changed.  Changes to the value will
  only be seen in the scope the delegate was created in (unless the variable is AllScope).

### Unsupported PowerShell features

- The extended type system and dynamic typing in genernal.  This means that things like methods, properties
  and even the specific method overload called by an expression is all determined at compile time. If
  a method declares a return type of `object`, you'll need to cast it to something else before you can
  do much with it.

- Commands. Yeah that's a big one I know. There's no way to run a command without a runspace, and if I
  require a runspace to run a command then there isn't a point in any of this. If you absolutely need
  to run a command, you can use the `$ExecutionContext` or `[powershell]` API's, but they are likely to
  be unreliable from a thread other than the pipeline thread.

- Variables need to assigned before they can be used.  If the type is not included in the assignment
  the type will be inferred from the assignment (similar to C#'s `var` keyword, but implied).

- A good amount more. I'll update this list as much as possible as I use this more. If you run into
  something that could use explaining here, opening an issue on this repo would be very helpful.

## Custom language keywords

No support for commands left some room to add some custom keywords that help with some problems
that aren't all that applicable to PowerShell, but may be here.

### With

```powershell
using namespace System.Management.Automation

$delegate = [psdelegate]{
    with ($ps = [powershell]::Create([RunspaceMode]::NewRunspace)) {
        $result = $ps.AddScript('Get-Date').Invoke()
        $result[0].Properties.Add(
            [psnoteproperty]::new(
                'Runspace',
                $ps.Runspace))

        return $result[0]
    }
}

$pso = $delegate.Invoke()
$pso
# Sunday, April 22, 2018 6:51:04 PM
$pso.Runspace
# Id Name        ComputerName  Type     State   Availability
# -- ----        ------------  ----     -----   ------------
# 13 Runspace13  localhost     Local    Closed  None
```

The `with` keyword works like the `using` keyword in C#. The object in the initialization expression (the
parenthesis) will be disposed when the statement ends, even if the event of an unhandled exception.

### Default

```powershell

$delegate = [psdelegate]{ default([ConsoleColor]) }
$delegate.Invoke()
# Black

$delegate = [psdelegate]{ default([object]) }
$delegate.Invoke()
# (returns null)
```

Returns the default value for a type. This will return `null` if the type is nullable, otherwise it
will return the default value for the that type.

### Generic

```powershell
$delegate = [psdelegate]{ generic ([array]::Empty(), [int]) }
$delegate.Invoke().GetType()
# IsPublic IsSerial Name       BaseType
# -------- -------- ----       --------
# True     True     Int32[]    System.Array

$delegate = [psdelegate]{ generic ([tuple]::Create(10, 'string'), [int], [string]) }
$delegate.Invoke()
# Item1 Item2  Length
# ----- -----  ------
#    10 string      2
```

I haven't built in any automatic generic argument inference yet (PowerShell has a little bit), so
right now this keyword is required to use any generic method. The syntax for the generic type arguments
is similar to the `params` keyword.  The first "parameter" should be the the member expression, and
any additional parameters should be types. The resulting `Expression` is just the member expression
with the resolved method.

## Contributions Welcome!

We would love to incorporate community contributions into this project.  If you would like to
contribute code, documentation, tests, or bug reports, please read our [Contribution Guide](https://github.com/SeeminglyScience/PSLambda/tree/master/docs/CONTRIBUTING.md) to learn more.

