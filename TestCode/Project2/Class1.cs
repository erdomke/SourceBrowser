﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Various extension methods
/// </summary>
public static class Extensions
{
    /// <summary>
    /// A method which extends a string
    /// </summary>
    /// <param name="s">String to extend</param>
    [System.ComponentModel.DisplayName("My Extension Method"), System.ComponentModel.Description("Some description here")]
    public static void ExtensionMethod(this string s) { }
}

class ArrayOfZeroLengthAllocationDetection
{
    private const int SIZE = 0;
    static void Test()
    {
        var a = new object[0];
        Console.WriteLine(a.Length);
        a = new object[] { };
        Console.WriteLine(a.Length);
        a = new object[SIZE];
    }
}

public class ExtensionUsage
{
    public void Test()
    {
        "".ExtensionMethod();
        Extensions.ExtensionMethod("");
    }

    ~ExtensionUsage()
    {
    }
}

public interface I1 { void Foo(); }
public interface I2 : I1 { }

interface I3 : IEnumerable<C> { }

public partial class Partial
{
    partial void Foo();

    internal const string Internal = "Friend";
}

namespace Acme
{
    public class Generic<T>
    {
        public static T M<T>(T t) { }
    }

    namespace Test
    {
    }

    namespace Test2 { class Baz { } }
}

[Guid(@"C09FBDCB-C126-4A4F-BF36-4B1E3AF4D376")]
class Class<U> : I1
{
    public class Nested1
    {
        public enum Nested2
        {
            A
        }
    }

    public class NestedGeneric<T>
    {
        public T GetT(U u) { }
    }

    void I1.Foo()
    {
#if _DEBUG
        whatnot
#endif
        C c = new C();
        throw new NotImplementedException();
    }
}

[Guid("C09FBDCB-C126-4A4F-BF36-4B1E3AF4D376")]
class Abc : I1
{
    public virtual void M() { }
    protected virtual string Name { get; set; }
    protected internal virtual event Action Event;

    public static readonly Guid guid = new Guid(@"AAAAAAAA-C126-4A4F-BF36-4B1E3AF4D376");
    public static readonly Guid guid2 = new Guid("{BBBBBBBB-C126-4A4F-BF36-4B1E3AF4D376}");
    public static readonly Guid guid3 = new Guid(@"{C09FBDCB-C126-4A4F-bf36-4B1E3AF4D376}");

    public void Foo()
    {
        var a = Name;
        Name = a;
        this.Name = a;
    }
}

class A
{

    /// <summary>
    /// Perform the message M
    /// </summary>
    [System.ComponentModel.DisplayName("My Awesome Method")]
    public virtual void M()
    {
    }

    [System.ComponentModel.Description("The name of object A")]
    public abstract string Name
    {
        get;
    }

    protected virtual event Action Event;
}

class B : A, System.ICloneable
{
    System.ICloneable c;
    A a = new A();

    public B()
    {
    }

    public override void M()
    {
        base.M();
        Name = Name;
        var b = new B();
    }

    protected override string Name
    {
        get
        {
            return base.Name;
        }
    }

    protected internal override event Action Event;
}

namespace ParentNs.ChildNs
{
    class NamespaceClass
    {

    }
}
