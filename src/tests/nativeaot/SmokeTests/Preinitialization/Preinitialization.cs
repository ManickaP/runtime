// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

using BindingFlags = System.Reflection.BindingFlags;

internal class Program
{
    private static int Main()
    {
#if !MULTIMODULE_BUILD
        TestHardwareIntrinsics.Run();
        TestLdstr.Run();
        TestException.Run();
        TestThreadStaticNotInitialized.Run();
        TestUntouchedThreadStaticInitialized.Run();
        TestPointers.Run();
        TestConstants.Run();
        TestArray.Run();
        TestArrayOutOfRange.Run();
        TestMdArray.Run();
        TestSimpleObject.Run();
        TestFinalizableObject.Run();
        TestStoreIntoOtherStatic.Run();
        TestCctorCycle.Run();
        TestReferenceTypeAllocation.Run();
        TestReferenceTypeWithGCPointerAllocation.Run();
        TestRelationalOperators.Run();
        TestTryFinally.Run();
        TestTryCatch.Run();
        TestBadClass.Run();
        TestRefs.Run();
        TestDelegate.Run();
        TestDelegateReflectionVisible.Run();
        TestInitFromOtherClass.Run();
        TestInitFromOtherClassDouble.Run();
        TestDelegateToOtherClass.Run();
        TestLotsOfBackwardsBranches.Run();
        TestSwitch.Run();
        TestDrawCircle.Run();
        TestValueTypeDup.Run();
        TestFunctionPointers.Run();
        TestGCInteraction.Run();
        TestDuplicatedFields.Run();
        TestInstanceDelegate.Run();
        TestStringFields.Run();
        TestSharedCode.Run();
        TestSpan.Run();
        TestReadOnlySpan.Run();
        TestRvaDataReads.Run();
        TestStaticInterfaceMethod.Run();
        TestConstrainedCall.Run();
        TestTypeHandles.Run();
        TestPreinitDefinition.Run();
        TestIsValueType.Run();
        TestIndirectLoads.Run();
        TestInitBlock.Run();
        TestDataflow.Run();
        TestConversions.Run();
        TestVTables.Run();
        TestVTableManipulation.Run();
        TestVTableNegativeScenarios.Run();
        TestByRefFieldAddressEquality.Run();
        TestComInterfaceEntry.Run();
        TestPreinitializedBclTypes.Run();
#else
        Console.WriteLine("Preinitialization is disabled in multimodule builds for now. Skipping test.");
#endif

        return 100;
    }
}

class TestHardwareIntrinsics
{
    class Simple1
    {
        public static bool IsSseSupported = Sse.IsSupported;
    }

    class Simple2
    {
        public static bool IsAvxVnniSupported = AvxVnni.IsSupported;
    }

    class Complex
    {
        public static bool IsPopcntSupported = Popcnt.IsSupported;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(Simple1));
        Assert.AreEqual(Sse.IsSupported, Simple1.IsSseSupported);

        Assert.IsPreinitialized(typeof(Simple2));
        Assert.AreEqual(AvxVnni.IsSupported, Simple2.IsAvxVnniSupported);

        if (RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64)
            Assert.IsLazyInitialized(typeof(Complex));
        else
            Assert.IsPreinitialized(typeof(Complex));
        Assert.AreEqual(Popcnt.IsSupported, Complex.IsPopcntSupported);
    }
}

class TestLdstr
{
    static string s_mine;
    static bool s_literalsEqual;

    static string GetOtherString() => "Hello";

    static TestLdstr()
    {
        s_mine = nameof(TestLdstr);
        s_literalsEqual = Object.ReferenceEquals("Hello", GetOtherString());
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestLdstr));
        Assert.AreSame(nameof(TestLdstr), s_mine);
        Assert.True(s_literalsEqual);
    }
}

class TestException
{
    static bool s_wasThrown;

    static TestException()
    {
        try
        {
            throw new Exception();
        }
        catch (Exception)
        {
            s_wasThrown = true;
        }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestException));
        Assert.True(s_wasThrown);
    }
}

class TestThreadStaticNotInitialized
{
    [ThreadStatic]
    static bool s_wasRun = true;

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestThreadStaticNotInitialized));
        Assert.True(s_wasRun);
    }
}

class TestUntouchedThreadStaticInitialized
{
    [ThreadStatic]
#pragma warning disable 169
    static bool s_unused;
#pragma warning restore 169
    static bool s_wasRun = true;

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestUntouchedThreadStaticInitialized));
        Assert.True(s_wasRun);
    }
}

unsafe class TestPointers
{
    static byte* s_myByte = (byte*)123;
    static void* s_myVoid = GimmeVoid(s_myByte);
    static byte*[] s_byteStarArray = new byte*[] { (byte*)123, (byte*)456 };

    static void* GimmeVoid(byte* template)
    {
        return template;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestPointers));
        Assert.AreEqual((void*)123, s_myByte);
        Assert.AreEqual((void*)123, s_myVoid);

        Assert.AreEqual(2, s_byteStarArray.Length);
        Assert.AreEqual((byte*)123, s_byteStarArray[0]);
        Assert.AreEqual((byte*)456, s_byteStarArray[1]);
    }
}

class TestConstants
{
    static bool s_bool = true;
    static int s_smallInt = 3;
    static int s_mediumInd = 70;
    static int s_bigInt = 2000000;
    static long s_hugeInt = 20000000000;
    static float s_float = 3.14f;
    static double s_double = 3.14;

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestConstants));
        Assert.AreEqual(true, s_bool);
        Assert.AreEqual(3, s_smallInt);
        Assert.AreEqual(70, s_mediumInd);
        Assert.AreEqual(2000000, s_bigInt);
        Assert.AreEqual(20000000000, s_hugeInt);
        Assert.AreEqual(3.14f, s_float);
        Assert.AreEqual(3.14, s_double);
    }
}

class TestArray
{
    struct MyValueType
    {
        public bool B;
        public int I;
    }

    enum MyEnum
    {
        One, Two
    }

    static byte[] s_byteArray;
    static MyValueType[] s_valueTypeArray;
    static int s_byteArrayCount;
    static MyEnum[] s_enumArray;
    static byte s_byteArrayFirstElement;

    static TestArray()
    {
        s_byteArray = new byte[]
        {
            1, 2, 3, 9, 8, 7, 1, 2, 3, 9, 8, 7
        };

        s_byteArrayCount = s_byteArray.Length;

        s_valueTypeArray = new MyValueType[2]
        {
            new MyValueType { B = false, I = 555 },
            new MyValueType { B = true, I = 565 },
        };

        s_enumArray = new MyEnum[2] { MyEnum.One, MyEnum.Two };

        s_byteArrayFirstElement = s_byteArray[0];
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestArray));
        Assert.AreEqual(s_byteArray.Length, 12);
        Assert.AreEqual(s_byteArray[0], 1);
        Assert.AreEqual(s_byteArray[1], 2);
        Assert.AreEqual(s_byteArray[11], 7);
        Assert.AreEqual(s_byteArrayCount, 12);

        Assert.AreEqual(s_valueTypeArray.Length, 2);
        Assert.AreEqual(s_valueTypeArray[0].B, false);
        Assert.AreEqual(s_valueTypeArray[0].I, 555);
        Assert.AreEqual(s_valueTypeArray[1].B, true);
        Assert.AreEqual(s_valueTypeArray[1].I, 565);

        Assert.AreEqual(s_enumArray.Length, 2);
        Assert.AreEqual((int)s_enumArray[0], (int)MyEnum.One);
        Assert.AreEqual((int)s_enumArray[1], (int)MyEnum.Two);

        Assert.AreEqual(s_byteArrayFirstElement, 1);
    }
}

class TestArrayOutOfRange
{
    class OutOfRange
    {
        public static byte[] s_byteArray;

        static OutOfRange()
        {
            s_byteArray = new byte[2];
            s_byteArray[2] = 1;
        }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(OutOfRange));

        bool thrown = false;
        try
        {
            OutOfRange.s_byteArray[0] = 1;
        }
        catch (TypeInitializationException)
        {
            thrown = true;
        }

        Assert.True(thrown);
    }
}

class TestMdArray
{
    static byte[,] s_myMdArray = new byte[10, 10];

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestMdArray));
        Assert.AreEqual(100, s_myMdArray.Length);
    }
}

class TestSimpleObject
{
    static object s_object = new object();

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestSimpleObject));
        Assert.AreSame(typeof(object), s_object.GetType());
    }
}

class TestFinalizableObject
{
    class Finalizable
    {
        ~Finalizable()
        {
            Console.WriteLine("Finalized");
        }
    }

    static object s_object = new Finalizable();

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestFinalizableObject));
        Assert.AreSame(typeof(Finalizable), s_object.GetType());
    }
}

static class TestStoreIntoOtherStatic
{
    class Park
    {
        public static int s_parked;
    }

    static TestStoreIntoOtherStatic()
    {
        Park.s_parked = 123;
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestStoreIntoOtherStatic));
    }
}

static class TestCctorCycle
{
    static readonly int s_value = Cycler.s_theValue;

    class Cycler
    {
        public static readonly int s_theValue = s_value;
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestCctorCycle));
        Assert.AreEqual(0, s_value);
    }
}

class TestReferenceTypeAllocation
{
    class ReferenceType
    {
        public int IntValue;
        public double DoubleValue;

        public ReferenceType(int intValue, double doubleValue)
        {
            IntValue = intValue;
            DoubleValue = doubleValue;
        }
    }

    static ReferenceType s_referenceType = new ReferenceType(12345, 3.14159);

    public static void Run()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Wasm)
        {
            // Because of the double field, this is not preinitialized
            Assert.IsLazyInitialized(typeof(TestReferenceTypeAllocation));
        }
        else
        {
            Assert.IsPreinitialized(typeof(TestReferenceTypeAllocation));
        }
        Assert.AreEqual(12345, s_referenceType.IntValue);
        Assert.AreEqual(3.14159, s_referenceType.DoubleValue);
    }
}

class TestReferenceTypeWithGCPointerAllocation
{
    class ReferenceType
    {
        public string StringValue;

        public ReferenceType(string stringvalue)
        {
            StringValue = stringvalue;
        }
    }

    static ReferenceType s_referenceType = new ReferenceType("hi");

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestReferenceTypeWithGCPointerAllocation));
        Assert.AreSame("hi", s_referenceType.StringValue);
    }
}

static class TestRelationalOperators
{
    static int s_zeroInt = 0;
    static double s_zeroDouble = 0.0;
    static long s_zeroLong = 0;
    static int s_minusOneInt = -1;
    static long s_minusOneLong = -1;

    static bool s_finished;

    static TestRelationalOperators()
    {
        if (s_zeroInt > 0)
            throw new Exception();
        if (s_zeroInt < 0)
            throw new Exception();
        if (s_zeroInt >= 0 && s_zeroInt <= 0)
        {
            if (s_zeroLong > 0)
                throw new Exception();
            if (s_zeroLong < 0)
                throw new Exception();
            if (s_zeroLong >= 0 && s_zeroLong <= 0)
            {
                if (s_zeroDouble > 0)
                    throw new Exception();
                if (s_zeroDouble < 0)
                    throw new Exception();
                if (s_zeroDouble >= 0 && s_zeroDouble <= 0)
                {
                    if ((uint)s_minusOneInt < (uint)s_zeroInt)
                        throw new Exception();
                    if ((uint)s_zeroInt > (uint)s_minusOneInt)
                        throw new Exception();
                    if ((ulong)s_minusOneLong < (ulong)s_zeroLong)
                        throw new Exception();
                    if ((ulong)s_zeroLong > (ulong)s_minusOneLong)
                        throw new Exception();

                    if (s_zeroInt == 0 && s_zeroLong == 0)
                        s_finished = true;
                }
            }
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestRelationalOperators));
        Assert.AreEqual(true, s_finished);
    }
}

class TestTryFinally
{
    static int s_cookie;

    static TestTryFinally()
    {
        try
        {
            if (new byte[0].Length > 0)
                throw new Exception();
        }
        finally
        {
            s_cookie = 1985;
        }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestTryFinally));
        Assert.AreEqual(1985, s_cookie);
    }
}

class TestTryCatch
{
    static int s_cookie;

    static TestTryCatch()
    {
        try
        {
            if (s_cookie > 0)
                throw null;
        }
        catch (Exception)
        {
            s_cookie = 100;
        }
        s_cookie = 2020;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestTryCatch));
        Assert.AreEqual(2020, s_cookie);
    }
}

class TestBadClass
{
    [StructLayout(LayoutKind.Explicit)]
    class BadLayoutClass<T>
    {
    }

    static int s_cookie;
    static object s_badClass;

    static object MakeBadLayoutClass() => new BadLayoutClass<int>();

    static TestBadClass()
    {
        try
        {
            s_badClass = MakeBadLayoutClass();
            s_cookie = -1;
        }
        catch (Exception)
        {
            s_cookie = 1;
        }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TestBadClass));
        Assert.AreEqual(1, s_cookie);
        Assert.AreSame(null, s_badClass);
    }
}

class TestRefs
{
    struct IntStruct { public int Value { get; set; } }
    struct DoubleStruct { public double Value { get; set; } }

    static IntStruct s_value1;
    static IntStruct s_value2;
    static DoubleStruct s_doubleValue;

    static ref IntStruct PickOne(int which)
    {
        if (which == 1)
            return ref s_value1;
        return ref s_value2;
    }

    static void Set(ref IntStruct location, int value)
    {
        location.Value = value;
    }

    static TestRefs()
    {
        ref IntStruct loc1 = ref PickOne(1);
        Set(ref loc1, 41);
        s_value1.Value++;

        s_value2.Value = 98;
        ref IntStruct loc2 = ref PickOne(2);
        if (loc2.Value == 98)
        {
            loc2.Value++;
        }
        if (s_value2.Value == 99)
        {
            s_value2.Value++;
        }

        ref DoubleStruct dblRef = ref s_doubleValue;
        dblRef.Value = 3.14;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestRefs));
        Assert.AreEqual(42, s_value1.Value);
        Assert.AreEqual(100, s_value2.Value);
        Assert.AreEqual(3.14, s_doubleValue.Value);
    }
}

class TestDelegate
{
    static Func<int> s_delegate = GetVal;

    static int GetVal() => 42;

    static Func<int> s_lambda = () => 2020;

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestDelegate));
        Assert.AreEqual(42, s_delegate());
        Assert.AreEqual(2020, s_lambda());
    }
}

class TestDelegateReflectionVisible
{
    static readonly Action s_a = DelegateTarget;

    static void DelegateTarget() { }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestDelegateReflectionVisible));
        Assert.AreEqual(nameof(DelegateTarget), s_a.Method.Name);
    }
}

class TestInitFromOtherClass
{
    class OtherClass
    {
        public static readonly int IntValue = 456;
        public static readonly string StringValue = "Hello";
        public static readonly object ObjectValue = new object();
    }

    static int s_intValue = OtherClass.IntValue;
    static string s_stringValue = OtherClass.StringValue;
    static object s_objectValue = OtherClass.ObjectValue;
    static bool s_areStringsSame = Object.ReferenceEquals(OtherClass.StringValue, "Hello");

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestInitFromOtherClass));
        Assert.AreEqual(OtherClass.IntValue, s_intValue);
        Assert.AreSame(OtherClass.StringValue, s_stringValue);
        Assert.AreSame(OtherClass.ObjectValue, s_objectValue);
        Assert.True(s_areStringsSame);
    }
}

class TestInitFromOtherClassDouble
{
    class OtherClass
    {
        public static readonly int IntValue = 456;
        public static readonly string StringValue = "Hello";
        public static readonly object ObjectValue = new object();
    }

    class OtherClassDouble
    {
        public static readonly int IntValue = OtherClass.IntValue;
        public static readonly string StringValue = OtherClass.StringValue;
        public static readonly object ObjectValue = OtherClass.ObjectValue;
    }

    static int s_intValue = OtherClassDouble.IntValue;
    static string s_stringValue = OtherClassDouble.StringValue;
    static object s_objectValue = OtherClassDouble.ObjectValue;

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestInitFromOtherClassDouble));
        Assert.AreEqual(OtherClass.IntValue, s_intValue);
        Assert.AreSame(OtherClass.StringValue, s_stringValue);
        Assert.AreSame(OtherClass.ObjectValue, s_objectValue);
    }
}


class TestDelegateToOtherClass
{
    static Func<int> s_getCookie = OtherClass.s_otherclass.GetCookie;
    static Func<Type> s_getStringType = OtherClass.s_otherString.GetType;
    static Func<int> s_getCookieDoubleIndirect = OtherClass.s_getCookie;
    static Func<Type> s_getStringTypeDoubleIndirect = OtherClass.s_getStringType;
    static Func<int> s_getCookieIndirected = OtherClass.s_otherclassFromYetAnother.GetCookie;
    static Func<Type> s_getStringTypeIndirected = OtherClass.s_otherStringFromYetAnother.GetType;

    class OtherClass
    {
        int _cookie;
        public static readonly OtherClass s_otherclass = new OtherClass(4040);
        public static readonly string s_otherString = "1";
        public static readonly Func<int> s_getCookie = YetAnotherClass.s_otherclass.GetCookie;
        public static readonly Func<Type> s_getStringType = YetAnotherClass.s_otherString.GetType;
        public static readonly OtherClass s_otherclassFromYetAnother = YetAnotherClass.s_otherclass;
        public static readonly string s_otherStringFromYetAnother = YetAnotherClass.s_otherString;
        public OtherClass(int cookie) { _cookie = cookie; }
        public int GetCookie() => _cookie;
    }

    class YetAnotherClass
    {
        public static readonly OtherClass s_otherclass = new OtherClass(1010);
        public static readonly string s_otherString = "1";
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(TestDelegateToOtherClass));

        Assert.AreEqual(4040, s_getCookie());
        Assert.AreSame(OtherClass.s_otherclass, s_getCookie.Target);
        Assert.AreSame(typeof(string), s_getStringType());
        Assert.AreSame(OtherClass.s_otherString, s_getStringType.Target);

        Assert.AreEqual(1010, s_getCookieDoubleIndirect());
        Assert.AreSame(YetAnotherClass.s_otherclass, s_getCookieDoubleIndirect.Target);
        Assert.AreSame(typeof(string), s_getStringTypeDoubleIndirect());
        Assert.AreSame(YetAnotherClass.s_otherString, s_getStringTypeDoubleIndirect.Target);
        Assert.AreSame(OtherClass.s_getCookie, s_getCookieDoubleIndirect);
        Assert.AreSame(OtherClass.s_getStringType, s_getStringTypeDoubleIndirect);

        Assert.AreEqual(1010, s_getCookieIndirected());
        Assert.AreSame(YetAnotherClass.s_otherclass, s_getCookieIndirected.Target);
        Assert.AreSame(typeof(string), s_getStringTypeIndirected());
        Assert.AreSame(YetAnotherClass.s_otherString, s_getStringTypeIndirected.Target);
    }
}

class TestLotsOfBackwardsBranches
{
    class TypeWithLotsOfBackwardsBranches
    {
        public static readonly int Sum;

        static TypeWithLotsOfBackwardsBranches()
        {
            int sum = 0;
            for (int i = 0; i < int.MaxValue / 2; i++)
                sum += i;
            Sum = sum;
        }
    }

    class TypeWithSomeBackwardsBranches
    {
        public static readonly int Sum;

        static TypeWithSomeBackwardsBranches()
        {
            int sum = 0;
            for (int i = 0; i < 100; i++)
                sum += i;
            Sum = sum;
        }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(TypeWithLotsOfBackwardsBranches));
        Assert.AreEqual(-1610612735, TypeWithLotsOfBackwardsBranches.Sum);

        Assert.IsPreinitialized(typeof(TypeWithSomeBackwardsBranches));
        Assert.AreEqual(4950, TypeWithSomeBackwardsBranches.Sum);
    }
}

class TestSwitch
{
    class Switcher
    {
        public static int CaseMinus1 = Switch(-1);
        public static int Case0 = Switch(0);
        public static int Case6 = Switch(6);
        public static int Case100 = Switch(100);

        private static int Switch(int x)
        {
            switch (x)
            {
                case 0: return 100;
                case 1: return 200;
                case 2: return 300;
                case 3: return 400;
                case 4: return 500;
                case 5: return 600;
                case 6: return 700;
                default: return 100000;
            }
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(Switcher));
        Assert.AreEqual(Switcher.CaseMinus1, 100000);
        Assert.AreEqual(Switcher.Case0, 100);
        Assert.AreEqual(Switcher.Case6, 700);
        Assert.AreEqual(Switcher.Case100, 100000);
    }
}

class TestDrawCircle
{
    static class CircleHolder
    {
        public static readonly byte[] s_bytes;

        static CircleHolder()
        {
            s_bytes = ComputeCircleBytes();
        }
    }

    private static byte[] ComputeCircleBytes()
    {
        const int Width = 16;

        byte[] bytes = new byte[Width * Width];
        for (int i = 0; i < bytes.Length; i++)
        {
            int x = i % Width;
            int y = i / Width;

            x -= Width / 2;
            y -= Width / 2;

            if (x * x + y * y < (Width / 2) * (Width / 2))
                bytes[i] = (byte)'*';
        }

        return bytes;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(CircleHolder));

        byte[] expected = ComputeCircleBytes();
        byte[] actual = CircleHolder.s_bytes;

        Assert.AreEqual(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], actual[i]);
        }
    }
}

class TestValueTypeDup
{
    class Dup
    {
        public static byte[] s_bytes;

        static Dup()
        {
            var bytes = new byte[2];
            int i = 0;
            while (i < 2)
            {
                bytes[i++] = 42;
            }
            s_bytes = bytes;
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(Dup));

        Assert.AreEqual(2, Dup.s_bytes.Length);
        Assert.AreEqual(42, Dup.s_bytes[0]);
        Assert.AreEqual(42, Dup.s_bytes[1]);
    }
}

unsafe class TestFunctionPointers
{
    struct WithFunctionPointer
    {
        public void* Ptr;
        internal static WithFunctionPointer s_foo { get; } = new WithFunctionPointer() { Ptr = (delegate*<void>)&X };
        internal static void X() { }
    }

    public static void Run()
    {
        Assert.IsLazyInitialized(typeof(WithFunctionPointer));
        Assert.AreEqual(WithFunctionPointer.s_foo.Ptr, (delegate*<void>)&WithFunctionPointer.X);
    }
}

class TestGCInteraction
{
    class WithFrozenObjects
    {
        internal readonly static string s_someStringLiteral = "Some string literal";
        internal readonly static object s_someObject = new object();
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(WithFrozenObjects));

        var holder = new object[]
        {
            WithFrozenObjects.s_someStringLiteral,
            WithFrozenObjects.s_someObject,
        };

        var h1 = new DependentHandle(WithFrozenObjects.s_someObject, WithFrozenObjects.s_someStringLiteral);
        var h2 = new DependentHandle(WithFrozenObjects.s_someStringLiteral, WithFrozenObjects.s_someObject);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.AreSame(holder[0], WithFrozenObjects.s_someStringLiteral);
        Assert.AreSame(holder[1], WithFrozenObjects.s_someObject);

        h1.Dispose();
        h2.Dispose();
    }
}

class TestDuplicatedFields
{
    class WithSameFields
    {
        public static WithSameFields Field1a = new WithSameFields();
        public static WithSameFields Field1b = Field1a;

        public static int[] Field2a = new int[1];
        public static int[] Field2b = Field2a;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(WithSameFields));
        Assert.AreSame(WithSameFields.Field1a, WithSameFields.Field1b);
        Assert.AreSame(WithSameFields.Field2a, WithSameFields.Field2b);
    }
}

class TestInstanceDelegate
{
    class ClassWithInstanceDelegate
    {
        public static Func<int> Instance1 = new ClassWithInstanceDelegate(42).GetCookie;
        public static ClassWithInstanceDelegate Target = new ClassWithInstanceDelegate(123);
        public static Func<int> Instance2 = Target.GetCookie;

        private int _cookie;
        public ClassWithInstanceDelegate(int cookie) => _cookie = cookie;
        public int GetCookie() => _cookie;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(ClassWithInstanceDelegate));
        Assert.AreEqual(42, ClassWithInstanceDelegate.Instance1());
        Assert.AreEqual(123, ClassWithInstanceDelegate.Instance2());
        Assert.AreSame(ClassWithInstanceDelegate.Target, ClassWithInstanceDelegate.Instance2.Target);
    }
}

class TestStringFields
{
    class ClassAccessingLength
    {
        public static int Length = "Hello".Length;
    }

    class ClassAccessingNull
    {
        public static int Length;
        static ClassAccessingNull()
        {
            string myNull = null;
            try
            {
                Length = myNull.Length;
            }
            catch (Exception) { }
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(ClassAccessingLength));
        Assert.AreEqual(5, ClassAccessingLength.Length);

        Assert.IsLazyInitialized(typeof(ClassAccessingNull));
        Assert.AreEqual(0, ClassAccessingNull.Length);
    }
}

class TestSharedCode
{
    class ClassWithTemplate<T>
    {
        public static int Cookie = 42;
        public static T[] Array = new T[0];
    }

    class C1 { }
    class C2 { }
    class C3 { }
    class C4 { }
    class C5 { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int AccessCookie<T>()
        => ClassWithTemplate<T>.Cookie;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object AccessArray<T>()
        => ClassWithTemplate<T>.Array;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050:MakeGeneric",
        Justification = "MakeGeneric is over reference types")]
    public static void Run()
    {
        {
            int val = AccessCookie<C1>();
            Assert.AreEqual(42, val);

            val = (int)typeof(ClassWithTemplate<>).MakeGenericType(GetC2()).GetField("Cookie").GetValue(null);
            Assert.AreEqual(42, val);
            static Type GetC2() => typeof(C2);

            val = (int)typeof(TestSharedCode).GetMethod(nameof(AccessCookie)).MakeGenericMethod(GetC3()).Invoke(null, Array.Empty<object>());
            Assert.AreEqual(42, val);
            static Type GetC3() => typeof(C3);
        }

        {
            // Expecting this to be a frozen array, and reported as int.MaxValue by the GC
            object val = AccessArray<C1>();
            Assert.AreEqual(int.MaxValue, GC.GetGeneration(val));

            val = typeof(ClassWithTemplate<>).MakeGenericType(GetC4()).GetField("Array").GetValue(null);
            Assert.AreEqual(0, GC.GetGeneration(val));
            Assert.AreEqual(nameof(C4), val.GetType().GetElementType().Name);
            static Type GetC4() => typeof(C4);

            val = typeof(TestSharedCode).GetMethod(nameof(AccessArray)).MakeGenericMethod(GetC5()).Invoke(null, Array.Empty<object>());
            Assert.AreEqual(0, GC.GetGeneration(val));
            Assert.AreEqual(nameof(C5), val.GetType().GetElementType().Name);
            static Type GetC5() => typeof(C5);
        }
    }
}

class TestSpan
{
    class StackAlloc
    {
        public static byte FirstByte;
        public static byte LastByte;
        public static char FirstChar;
        public static char LastChar;

        static StackAlloc()
        {
            Span<byte> s1 = stackalloc byte[8];
            s1.Slice(0, 1)[0] = 42;
            s1.Slice(s1.Length - 1, 1)[0] = 100;
            FirstByte = s1[0];
            LastByte = s1[7];

            Span<char> s2 = stackalloc char[8];
            s2.Slice(0, 1)[0] = 'H';
            s2.Slice(s2.Length - 1, 1)[0] = '!';
            FirstChar = s2[0];
            LastChar = s2[7];
        }
    }

    class ArrayAlloc
    {
        public static byte FirstByte;
        public static byte LastByte;
        public static char FirstChar;
        public static char LastChar;

        static ArrayAlloc()
        {
            byte[] a1 = new byte[8];
            Span<byte> s1 = a1;
            s1.Slice(0, 1)[0] = 42;
            s1.Slice(s1.Length - 1, 1)[0] = 100;
            FirstByte = a1[0];
            LastByte = a1[7];

            char[] a2 = new char[8];
            Span<char> s2 = a2;
            s2.Slice(0, 1)[0] = 'H';
            s2.Slice(s2.Length - 1, 1)[0] = '!';
            FirstChar = a2[0];
            LastChar = a2[7];
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(StackAlloc));
        Assert.AreEqual(42, StackAlloc.FirstByte);
        Assert.AreEqual(100, StackAlloc.LastByte);
        Assert.AreEqual('H', StackAlloc.FirstChar);
        Assert.AreEqual('!', StackAlloc.LastChar);

        Assert.IsPreinitialized(typeof(ArrayAlloc));
        Assert.AreEqual(42, ArrayAlloc.FirstByte);
        Assert.AreEqual(100, ArrayAlloc.LastByte);
        Assert.AreEqual('H', ArrayAlloc.FirstChar);
        Assert.AreEqual('!', ArrayAlloc.LastChar);
    }
}

class TestReadOnlySpan
{
    class SimpleReadOnlySpanAccess
    {
        private static ReadOnlySpan<int> Ints => new int[] { 5, 6, 7, 8 };

        public /* not readonly on purpose */ static int Sum;

        static SimpleReadOnlySpanAccess()
        {
            ReadOnlySpan<int> val = Ints;
            Sum = val[0] + val[1] + val[2] + val[3];
        }
    }

    class OutOfRangeAccess
    {
        private static ReadOnlySpan<int> Ints => new int[] { 5, 6, 7, 8 };

        public readonly static int Sum;

        static OutOfRangeAccess()
        {
            ReadOnlySpan<int> val = Ints;
            Sum = val[4];
        }
    }

    class DefaultInstanceAccess
    {
        public readonly static int Sum;

        static DefaultInstanceAccess()
        {
            ReadOnlySpan<int> val = default;
            Sum = val[0];
        }
    }

    class MoreOperations
    {
        public readonly static int IntsLength;
        public readonly static int StringLength;
        public readonly static char FirstChar;

        private static ReadOnlySpan<int> Ints => new int[] { 5, 6, 7, 8 };

        private static ReadOnlySpan<char> GetString() => "Hello World!";

        static MoreOperations()
        {
            IntsLength = Ints.Length;
            StringLength = GetString().Length;
            FirstChar = GetString()[0];
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(SimpleReadOnlySpanAccess));
        Assert.AreEqual(26, SimpleReadOnlySpanAccess.Sum);

        Assert.IsLazyInitialized(typeof(OutOfRangeAccess));
        if (SimpleReadOnlySpanAccess.Sum == 1000) // never true
            OutOfRangeAccess.Sum.ToString(); // make sure cctor is looked at

        Assert.IsLazyInitialized(typeof(DefaultInstanceAccess));
        if (SimpleReadOnlySpanAccess.Sum == 1000) // never true
            DefaultInstanceAccess.Sum.ToString(); // make sure cctor is looked at

        Assert.IsPreinitialized(typeof(MoreOperations));
        Assert.AreEqual(4, MoreOperations.IntsLength);
        Assert.AreEqual(12, MoreOperations.StringLength);
        Assert.AreEqual('H', MoreOperations.FirstChar);
    }
}

class TestRvaDataReads
{
    static class GuidProvider
    {
        public static ref readonly Guid TheGuid1
        {
            get
            {
                ReadOnlySpan<byte> data = [0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89, 0x9A, 0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xF0, 0x00];
                return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
            }
        }

        public static ref readonly Guid TheGuid2
        {
            get
            {
                ReadOnlySpan<byte> data = [0xDE, 0xEF, 0xF0, 0x00, 0x9A, 0xAB, 0xBC, 0xCD, 0x56, 0x67, 0x78, 0x89, 0x12, 0x23, 0x34, 0x45];
                return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
            }
        }
    }

    struct TwoGuids
    {
        public Guid Guid1, Guid2;
    }

    static class GuidReader
    {
        public static TwoGuids Value = new TwoGuids()
        {
            Guid1 = GuidProvider.TheGuid1,
            Guid2 = GuidProvider.TheGuid2,
        };
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(GuidReader));
        Assert.AreEqual(new Guid("45342312-6756-8978-9aab-bccddeeff000"), GuidReader.Value.Guid1);
        Assert.AreEqual(new Guid("00f0efde-ab9a-cdbc-5667-788912233445"), GuidReader.Value.Guid2);
    }
}

class TestStaticInterfaceMethod
{
    interface IFoo
    {
        static virtual int GetCookie1() => 42;
        static virtual int GetCookie2() => 0;
    }

    struct Foo : IFoo
    {
        static int IFoo.GetCookie2() => 100;
    }

    class SimpleStaticInterfaceMethodCall
    {
        public static readonly int s_value1 = Compute1<Foo>();
        public static readonly int s_value2 = Compute2<Foo>();

        static int Compute1<T>() where T : IFoo => T.GetCookie1();
        static int Compute2<T>() where T : IFoo => T.GetCookie2();
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(SimpleStaticInterfaceMethodCall));
        Assert.AreEqual(42, SimpleStaticInterfaceMethodCall.s_value1);
        Assert.AreEqual(100, SimpleStaticInterfaceMethodCall.s_value2);
    }
}

class TestConstrainedCall
{
    interface IFoo
    {
        int Frob();
    }

    struct Foo : IFoo
    {
        public int val;

        int IFoo.Frob()
        {
            val = 42;
            return 100;
        }
    }

    static int Call<T>(ref T inst) where T : IFoo => inst.Frob();

    class ConstrainedCall
    {
        public static Foo s_f;
        public static int s_i;

        static ConstrainedCall()
        {
            Foo f = default;
            s_i = Call<Foo>(ref f);
            s_f = f;
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(ConstrainedCall));
        Assert.AreEqual(100, ConstrainedCall.s_i);
        Assert.AreEqual(42, ConstrainedCall.s_f.val);
    }
}

class TestTypeHandles
{
    class Foo<T>
    {
        public static bool IsChar = typeof(T) == typeof(char);
        public static bool IsBool = typeof(T) == typeof(bool);
    }

    class CharHolder
    {
        public static readonly Type Type = typeof(char);
    }

    class IsChar
    {
        public static bool Is = typeof(char) == CharHolder.Type;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(Foo<char>));
        Assert.True(Foo<char>.IsChar);
        Assert.True(!Foo<char>.IsBool);

        Assert.IsPreinitialized(typeof(Foo<bool>));
        Assert.True(!Foo<bool>.IsChar);
        Assert.True(Foo<bool>.IsBool);

        Assert.IsPreinitialized(typeof(CharHolder));
        Assert.IsPreinitialized(typeof(IsChar));
        Assert.True(IsChar.Is);
    }
}

class TestPreinitDefinition
{
    class Gen<T>;

    class PreinitHolder
    {
        public readonly static Type TheType = typeof(Gen<>);
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(PreinitHolder));
        Assert.AreEqual("Gen`1", PreinitHolder.TheType.Name);
    }
}

class TestIsValueType
{
    class IsValueTypeTests
    {
        public static bool IntIsValueType = typeof(int).IsValueType;
        public static bool CharStarIsValueType = typeof(char*).IsValueType;
        public static bool ObjectIsValueType = typeof(object).IsValueType;
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(IsValueTypeTests));
        Assert.AreEqual(true, IsValueTypeTests.IntIsValueType);
        Assert.AreEqual(false, IsValueTypeTests.CharStarIsValueType);
        Assert.AreEqual(false, IsValueTypeTests.ObjectIsValueType);
    }
}

class TestIndirectLoads
{
    static unsafe sbyte Read(byte val) => *(sbyte*)&val;
    static unsafe short Read(ushort val) => *(short*)&val;
    static unsafe int Read(uint val) => *(int*)&val;
    static unsafe long Read(ulong val) => *(long*)&val;

    class LdindTester
    {
        public static sbyte SByte = Read(byte.MaxValue);
        public static short Short = Read(ushort.MaxValue);
        public static int Int = Read(uint.MaxValue);
        public static long Long = Read(ulong.MaxValue);
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(LdindTester));
        Assert.AreEqual(-1, LdindTester.SByte);
        Assert.AreEqual(-1, LdindTester.Short);
        Assert.AreEqual(-1, LdindTester.Int);
        Assert.AreEqual(-1, LdindTester.Long);
    }
}

class TestInitBlock
{
    class Simple
    {
        public static byte Value;

        static Simple()
        {
            Value = 123;
            Unsafe.InitBlockUnaligned(ref Value, 42, 1);
        }
    }

    class Overrun
    {
        public static byte Value;
        public static byte Pad;

        static Overrun()
        {
            Value = 123;
            Unsafe.InitBlockUnaligned(ref Value, 42, 2);
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(Simple));
        Assert.AreEqual(42, Simple.Value);

        Assert.IsLazyInitialized(typeof(Overrun));
        Assert.AreEqual(42, Overrun.Value);
        Assert.AreEqual(42, Overrun.Pad);
    }
}

class TestDataflow
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public static Type TheType = typeof(MyType);

    class MyType
    {
        public static void TheMethod() => Console.WriteLine("Hello");
    }

    public static void Run()
    {
        TheType.GetMethod("TheMethod").Invoke(null, []);
    }
}

class TestConversions
{
    private static int GetInt() => -42;
    private static long GetLong() => -42;
    private static float GetFloat() => -42;
    private static double GetDouble() => -42;
    private static nint GetNativeInt() => -42;

    class IntConversions
    {
        internal static byte s_byte;
        internal static sbyte s_sbyte;
        internal static short s_short;
        internal static ushort s_ushort;
        internal static uint s_uint;
        internal static long s_long;
        internal static ulong s_ulong;
        internal static float s_float;
        internal static double s_double;
        internal static nint s_nint;
        internal static nuint s_nuint;

        static IntConversions()
        {
            s_byte = unchecked((byte)GetInt());
            s_sbyte = unchecked((sbyte)GetInt());
            s_short = unchecked((short)GetInt());
            s_ushort = unchecked((ushort)GetInt());
            s_uint = unchecked((uint)GetInt());
            s_long = unchecked((long)GetInt());
            s_ulong = unchecked((ulong)GetInt());
            s_float = unchecked((float)GetInt());
            s_double = unchecked(GetInt());
            s_nint = unchecked((nint)GetInt());
            s_nuint = unchecked((nuint)GetInt());
        }
    }

    class LongConversions
    {
        internal static byte s_byte;
        internal static sbyte s_sbyte;
        internal static short s_short;
        internal static ushort s_ushort;
        internal static int s_int;
        internal static uint s_uint;
        internal static ulong s_ulong;
        internal static float s_float;
        internal static double s_double;
        internal static nint s_nint;
        internal static nuint s_nuint;

        static LongConversions()
        {
            s_byte = unchecked((byte)GetLong());
            s_sbyte = unchecked((sbyte)GetLong());
            s_short = unchecked((short)GetLong());
            s_ushort = unchecked((ushort)GetLong());
            s_int = unchecked((int)GetLong());
            s_uint = unchecked((uint)GetLong());
            s_ulong = unchecked((ulong)GetLong());
            s_float = unchecked((float)GetLong());
            s_double = unchecked((double)GetLong());
            s_nint = unchecked((nint)GetLong());
            s_nuint = unchecked((nuint)GetLong());
        }
    }

    class FloatConversions
    {
        internal static byte s_byte;
        internal static sbyte s_sbyte;
        internal static short s_short;
        internal static ushort s_ushort;
        internal static int s_int;
        internal static uint s_uint;
        internal static long s_long;
        internal static ulong s_ulong;
        internal static double s_double;
        internal static nint s_nint;
        internal static nuint s_nuint;

        static FloatConversions()
        {
            s_byte = unchecked((byte)GetFloat());
            s_sbyte = unchecked((sbyte)GetFloat());
            s_short = unchecked((short)GetFloat());
            s_ushort = unchecked((ushort)GetFloat());
            s_int = unchecked((int)GetFloat());
            s_uint = unchecked((uint)GetFloat());
            s_long = unchecked((long)GetFloat());
            s_ulong = unchecked((ulong)GetFloat());
            s_double = unchecked((double)GetFloat());
            s_nint = unchecked((nint)GetFloat());
            s_nuint = unchecked((nuint)GetFloat());
        }
    }

    class DoubleConversions
    {
        internal static byte s_byte;
        internal static sbyte s_sbyte;
        internal static short s_short;
        internal static ushort s_ushort;
        internal static int s_int;
        internal static uint s_uint;
        internal static long s_long;
        internal static ulong s_ulong;
        internal static float s_float;
        internal static nint s_nint;
        internal static nuint s_nuint;

        static DoubleConversions()
        {
            s_byte = unchecked((byte)GetDouble());
            s_sbyte = unchecked((sbyte)GetDouble());
            s_short = unchecked((short)GetDouble());
            s_ushort = unchecked((ushort)GetDouble());
            s_int = unchecked((int)GetDouble());
            s_uint = unchecked((uint)GetDouble());
            s_long = unchecked((long)GetDouble());
            s_ulong = unchecked((ulong)GetDouble());
            s_float = unchecked((float)GetDouble());
            s_nint = unchecked((nint)GetDouble());
            s_nuint = unchecked((nuint)GetDouble());
        }
    }

    class NativeIntConversions
    {
        internal static byte s_byte;
        internal static sbyte s_sbyte;
        internal static short s_short;
        internal static ushort s_ushort;
        internal static int s_int;
        internal static uint s_uint;
        internal static long s_long;
        internal static ulong s_ulong;
        internal static float s_float;
        internal static double s_double;
        internal static nuint s_nuint;

        static NativeIntConversions()
        {
            s_byte = unchecked((byte)GetNativeInt());
            s_sbyte = unchecked((sbyte)GetNativeInt());
            s_short = unchecked((short)GetNativeInt());
            s_ushort = unchecked((ushort)GetNativeInt());
            s_int = unchecked((int)GetNativeInt());
            s_uint = unchecked((uint)GetNativeInt());
            s_long = unchecked((long)GetNativeInt());
            s_ulong = unchecked((ulong)GetNativeInt());
            s_float = unchecked((float)GetNativeInt());
            s_double = unchecked((double)GetNativeInt());
            s_nuint = unchecked((nuint)GetNativeInt());
        }
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(IntConversions));
        Assert.AreEqual(unchecked((byte)GetInt()), IntConversions.s_byte);
        Assert.AreEqual(unchecked((sbyte)GetInt()), IntConversions.s_sbyte);
        Assert.AreEqual(unchecked((short)GetInt()), IntConversions.s_short);
        Assert.AreEqual(unchecked((ushort)GetInt()), IntConversions.s_ushort);
        Assert.AreEqual(unchecked((uint)GetInt()), IntConversions.s_uint);
        Assert.AreEqual(unchecked((long)GetInt()), IntConversions.s_long);
        Assert.AreEqual(unchecked((ulong)GetInt()), IntConversions.s_ulong);
        Assert.AreEqual(unchecked((float)GetInt()), IntConversions.s_float);
        Assert.AreEqual(unchecked((double)GetInt()), IntConversions.s_double);
        Assert.AreEqual(unchecked((nint)GetInt()), IntConversions.s_nint);
        Assert.AreEqual(unchecked((nuint)GetInt()), IntConversions.s_nuint);

        Assert.IsPreinitialized(typeof(LongConversions));
        Assert.AreEqual(unchecked((byte)GetLong()), LongConversions.s_byte);
        Assert.AreEqual(unchecked((sbyte)GetLong()), LongConversions.s_sbyte);
        Assert.AreEqual(unchecked((short)GetLong()), LongConversions.s_short);
        Assert.AreEqual(unchecked((ushort)GetLong()), LongConversions.s_ushort);
        Assert.AreEqual(unchecked((int)GetLong()), LongConversions.s_int);
        Assert.AreEqual(unchecked((uint)GetLong()), LongConversions.s_uint);
        Assert.AreEqual(unchecked((ulong)GetLong()), LongConversions.s_ulong);
        Assert.AreEqual(unchecked((float)GetLong()), LongConversions.s_float);
        Assert.AreEqual(unchecked((double)GetLong()), LongConversions.s_double);
        Assert.AreEqual(unchecked((nint)GetLong()), LongConversions.s_nint);
        Assert.AreEqual(unchecked((nuint)GetLong()), LongConversions.s_nuint);

        Assert.IsPreinitialized(typeof(FloatConversions));
        Assert.AreEqual(unchecked((byte)GetFloat()), FloatConversions.s_byte);
        Assert.AreEqual(unchecked((sbyte)GetFloat()), FloatConversions.s_sbyte);
        Assert.AreEqual(unchecked((short)GetFloat()), FloatConversions.s_short);
        Assert.AreEqual(unchecked((ushort)GetFloat()), FloatConversions.s_ushort);
        Assert.AreEqual(unchecked((int)GetFloat()), FloatConversions.s_int);
        Assert.AreEqual(unchecked((uint)GetFloat()), FloatConversions.s_uint);
        Assert.AreEqual(unchecked((long)GetFloat()), FloatConversions.s_long);
        Assert.AreEqual(unchecked((ulong)GetFloat()), FloatConversions.s_ulong);
        Assert.AreEqual(unchecked((double)GetFloat()), FloatConversions.s_double);
        Assert.AreEqual(unchecked((nint)GetFloat()), FloatConversions.s_nint);
        Assert.AreEqual(unchecked((nuint)GetFloat()), FloatConversions.s_nuint);

        Assert.IsPreinitialized(typeof(DoubleConversions));
        Assert.AreEqual(unchecked((byte)GetDouble()), DoubleConversions.s_byte);
        Assert.AreEqual(unchecked((sbyte)GetDouble()), DoubleConversions.s_sbyte);
        Assert.AreEqual(unchecked((short)GetDouble()), DoubleConversions.s_short);
        Assert.AreEqual(unchecked((ushort)GetDouble()), DoubleConversions.s_ushort);
        Assert.AreEqual(unchecked((int)GetDouble()), DoubleConversions.s_int);
        Assert.AreEqual(unchecked((uint)GetDouble()), DoubleConversions.s_uint);
        Assert.AreEqual(unchecked((long)GetDouble()), DoubleConversions.s_long);
        Assert.AreEqual(unchecked((ulong)GetDouble()), DoubleConversions.s_ulong);
        Assert.AreEqual(unchecked((float)GetDouble()), DoubleConversions.s_float);
        Assert.AreEqual(unchecked((nint)GetDouble()), DoubleConversions.s_nint);
        Assert.AreEqual(unchecked((nuint)GetDouble()), DoubleConversions.s_nuint);

        Assert.IsPreinitialized(typeof(NativeIntConversions));
        Assert.AreEqual(unchecked((byte)GetNativeInt()), NativeIntConversions.s_byte);
        Assert.AreEqual(unchecked((sbyte)GetNativeInt()), NativeIntConversions.s_sbyte);
        Assert.AreEqual(unchecked((short)GetNativeInt()), NativeIntConversions.s_short);
        Assert.AreEqual(unchecked((ushort)GetNativeInt()), NativeIntConversions.s_ushort);
        Assert.AreEqual(unchecked((int)GetNativeInt()), NativeIntConversions.s_int);
        Assert.AreEqual(unchecked((uint)GetNativeInt()), NativeIntConversions.s_uint);
        Assert.AreEqual(unchecked((long)GetNativeInt()), NativeIntConversions.s_long);
        Assert.AreEqual(unchecked((ulong)GetNativeInt()), NativeIntConversions.s_ulong);
        Assert.AreEqual(unchecked((float)GetNativeInt()), NativeIntConversions.s_float);
        Assert.AreEqual(unchecked((double)GetNativeInt()), NativeIntConversions.s_double);
        Assert.AreEqual(unchecked((nuint)GetNativeInt()), NativeIntConversions.s_nuint);
    }
}

class TestVTables
{
    public static unsafe class IUnknownImpl
    {
        [FixedAddressValueType]
        public static readonly IUnknownVftbl Vtbl;

        public static nint AbiToProjectionVftablePtr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in Vtbl));
        }

        static IUnknownImpl()
        {
            ComWrappers.GetIUnknownImpl(
                fpQueryInterface: out *(nint*)&((IUnknownVftbl*)Unsafe.AsPointer(ref Vtbl))->QueryInterface,
                fpAddRef: out *(nint*)&((IUnknownVftbl*)Unsafe.AsPointer(ref Vtbl))->AddRef,
                fpRelease: out *(nint*)&((IUnknownVftbl*)Unsafe.AsPointer(ref Vtbl))->Release);
        }
    }

    public static unsafe class IInspectableImpl
    {
        [FixedAddressValueType]
        public static readonly IInspectableVftbl Vtbl;

        public static nint AbiToProjectionVftablePtr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in Vtbl));
        }

        static IInspectableImpl()
        {
            *(IUnknownVftbl*)Unsafe.AsPointer(ref Vtbl) = *(IUnknownVftbl*)IUnknownImpl.AbiToProjectionVftablePtr;

            Vtbl.GetIids = &GetIids;
            Vtbl.GetRuntimeClassName = &GetRuntimeClassName;
            Vtbl.GetTrustLevel = &GetTrustLevel;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        public static int GetIids(void* thisPtr, uint* iidCount, Guid** iids) => 0;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int GetRuntimeClassName(void* thisPtr, nint* className) => 0;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        public static int GetTrustLevel(void* thisPtr, int* trustLevel) => 0;
    }

    internal static unsafe class IStringableImpl
    {
        public static readonly IStringableVftbl Vtbl;

        static IStringableImpl()
        {
            *(IInspectableVftbl*)Unsafe.AsPointer(ref Vtbl) = *(IInspectableVftbl*)IInspectableImpl.AbiToProjectionVftablePtr;

            Vtbl.ToString = &ToString;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        public static int ToString(void* thisPtr, nint* value) => 0;
    }

    public unsafe struct IUnknownVftbl
    {
        public delegate* unmanaged[MemberFunction]<void*, Guid*, void**, int> QueryInterface;
        public delegate* unmanaged[MemberFunction]<void*, uint> AddRef;
        public delegate* unmanaged[MemberFunction]<void*, uint> Release;
    }

    public unsafe struct IInspectableVftbl
    {
        public delegate* unmanaged[MemberFunction]<void*, Guid*, void**, int> QueryInterface;
        public delegate* unmanaged[MemberFunction]<void*, uint> AddRef;
        public delegate* unmanaged[MemberFunction]<void*, uint> Release;
        public delegate* unmanaged[MemberFunction]<void*, uint*, Guid**, int> GetIids;
        public delegate* unmanaged[MemberFunction]<void*, nint*, int> GetRuntimeClassName;
        public delegate* unmanaged[MemberFunction]<void*, int*, int> GetTrustLevel;
    }

    internal unsafe struct IStringableVftbl
    {
        public delegate* unmanaged[MemberFunction]<void*, Guid*, void**, int> QueryInterface;
        public delegate* unmanaged[MemberFunction]<void*, uint> AddRef;
        public delegate* unmanaged[MemberFunction]<void*, uint> Release;
        public delegate* unmanaged[MemberFunction]<void*, uint*, Guid**, int> GetIids;
        public delegate* unmanaged[MemberFunction]<void*, nint*, int> GetRuntimeClassName;
        public delegate* unmanaged[MemberFunction]<void*, int*, int> GetTrustLevel;
        public new delegate* unmanaged[MemberFunction]<void*, nint*, int> ToString;
    }

    public static unsafe void Run()
    {
        Assert.IsPreinitialized(typeof(IUnknownImpl));
        ComWrappers.GetIUnknownImpl(
                fpQueryInterface: out nint qi,
                fpAddRef: out nint addref,
                fpRelease: out nint release);
        Assert.AreEqual((nuint)qi, (nuint)IUnknownImpl.Vtbl.QueryInterface);
        Assert.AreEqual((nuint)addref, (nuint)IUnknownImpl.Vtbl.AddRef);
        Assert.AreEqual((nuint)release, (nuint)IUnknownImpl.Vtbl.Release);

        Assert.IsPreinitialized(typeof(IInspectableImpl));
        Assert.AreEqual((nuint)qi, (nuint)IInspectableImpl.Vtbl.QueryInterface);
        Assert.AreEqual((nuint)addref, (nuint)IInspectableImpl.Vtbl.AddRef);
        Assert.AreEqual((nuint)release, (nuint)IInspectableImpl.Vtbl.Release);
        Assert.AreEqual((nuint)(delegate* unmanaged[MemberFunction]<void*, uint*, Guid**, int>)&IInspectableImpl.GetIids, (nuint)IInspectableImpl.Vtbl.GetIids);
        Assert.AreEqual((nuint)(delegate* unmanaged[MemberFunction]<void*, int*, int>)&IInspectableImpl.GetTrustLevel, (nuint)IInspectableImpl.Vtbl.GetTrustLevel);

        Assert.IsPreinitialized(typeof(IStringableImpl));
        Assert.AreEqual((nuint)qi, (nuint)IStringableImpl.Vtbl.QueryInterface);
        Assert.AreEqual((nuint)addref, (nuint)IStringableImpl.Vtbl.AddRef);
        Assert.AreEqual((nuint)release, (nuint)IStringableImpl.Vtbl.Release);
        Assert.AreEqual((nuint)(delegate* unmanaged[MemberFunction]<void*, uint*, Guid**, int>)&IInspectableImpl.GetIids, (nuint)IStringableImpl.Vtbl.GetIids);
        Assert.AreEqual((nuint)(delegate* unmanaged[MemberFunction]<void*, int*, int>)&IInspectableImpl.GetTrustLevel, (nuint)IStringableImpl.Vtbl.GetTrustLevel);
        Assert.AreEqual((nuint)(delegate* unmanaged[MemberFunction]<void*, nint*, int>)&IStringableImpl.ToString, (nuint)IStringableImpl.Vtbl.ToString);
    }
}

class TestVTableManipulation
{
    public unsafe class TinyVtableAImpl
    {
        [FixedAddressValueType]
        public static readonly ITinyVtableA Vtbl = Initialize();

        private static ITinyVtableA Initialize()
        {
            ITinyVtableA result = default;
            result.First = &First;
            result.Second = &Second;
            return result;
        }
    }

    public unsafe class TinyVtableBImpl
    {
        [FixedAddressValueType]
        public static readonly ITinyVtableB Vtbl;

        public static nint AbiToProjectionVftablePtr => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in Vtbl));

        static TinyVtableBImpl()
        {
            *(ITinyVtableA*)Unsafe.AsPointer(ref Vtbl) = TinyVtableAImpl.Vtbl;
            Vtbl.Third = &Third;
        }
    }

    public unsafe class TinyVtableCImpl
    {
        [FixedAddressValueType]
        public static readonly ITinyVtableC Vtbl;

        static TinyVtableCImpl()
        {
            *(ITinyVtableB*)Unsafe.AsPointer(ref Vtbl) = *(ITinyVtableB*)TinyVtableBImpl.AbiToProjectionVftablePtr;
            Vtbl.Fourth = &Fourth;
        }
    }

    public unsafe struct ITinyVtableA
    {
        public delegate*<void> First;
        public delegate*<void> Second;
    }

    public unsafe struct ITinyVtableB
    {
        public delegate*<void> First;
        public delegate*<void> Second;
        public delegate*<void> Third;
    }

    public unsafe struct ITinyVtableC
    {
        public delegate*<void> First;
        public delegate*<void> Second;
        public delegate*<void> Third;
        public delegate*<void> Fourth;
    }

    static void First() { }
    static void Second() { }
    static void Third() { }
    static void Fourth() { }

    public static unsafe void Run()
    {
        Assert.IsPreinitialized(typeof(TinyVtableAImpl));
        Assert.AreEqual((nuint)(delegate*<void>)&First, (nuint)TinyVtableAImpl.Vtbl.First);
        Assert.AreEqual((nuint)(delegate*<void>)&Second, (nuint)TinyVtableAImpl.Vtbl.Second);

        Assert.IsPreinitialized(typeof(TinyVtableBImpl));
        Assert.AreEqual((nuint)(delegate*<void>)&First, (nuint)TinyVtableBImpl.Vtbl.First);
        Assert.AreEqual((nuint)(delegate*<void>)&Second, (nuint)TinyVtableBImpl.Vtbl.Second);
        Assert.AreEqual((nuint)(delegate*<void>)&Third, (nuint)TinyVtableBImpl.Vtbl.Third);

        Assert.IsPreinitialized(typeof(TinyVtableCImpl));
        Assert.AreEqual((nuint)(delegate*<void>)&First, (nuint)TinyVtableCImpl.Vtbl.First);
        Assert.AreEqual((nuint)(delegate*<void>)&Second, (nuint)TinyVtableCImpl.Vtbl.Second);
        Assert.AreEqual((nuint)(delegate*<void>)&Third, (nuint)TinyVtableCImpl.Vtbl.Third);
        Assert.AreEqual((nuint)(delegate*<void>)&Fourth, (nuint)TinyVtableCImpl.Vtbl.Fourth);
    }
}

class TestVTableNegativeScenarios
{
    class StoreIntoNint
    {
        public static readonly nint Field;

        unsafe static StoreIntoNint()
        {
            ITinyVtable result = default;
            Field = (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in result));
        }
    }

    class WriteNonMethodPointer
    {
        public static readonly ITinyVtable Vtbl;

        static unsafe WriteNonMethodPointer()
        {
            Vtbl.First = (delegate*<void>)123;
            Vtbl.Second = (delegate*<void>)456;
        }
    }

    unsafe class WriteNonMethodIndirect
    {
        public static readonly ITinyVtable Vtbl;

        static void Write(ref delegate*<void> f, int val) => f = (delegate*<void>)val;

        static unsafe WriteNonMethodIndirect()
        {
            Write(ref Vtbl.First, 123);
            Write(ref Vtbl.Second, 456);
        }
    }

    static void First() { }
    static void Second() { }

    public unsafe struct ITinyVtable
    {
        public delegate*<void> First;
        public delegate*<void> Second;
    }

    public static unsafe void Run()
    {
        Assert.IsLazyInitialized(typeof(StoreIntoNint));
        if (StoreIntoNint.Field == 0)
            throw new Exception();

        Assert.IsLazyInitialized(typeof(WriteNonMethodPointer));
        Assert.AreEqual(WriteNonMethodPointer.Vtbl.First, (void*)123);
        Assert.AreEqual(WriteNonMethodPointer.Vtbl.Second, (void*)456);

        Assert.IsLazyInitialized(typeof(WriteNonMethodIndirect));
        Assert.AreEqual(WriteNonMethodIndirect.Vtbl.First, (void*)123);
        Assert.AreEqual(WriteNonMethodIndirect.Vtbl.Second, (void*)456);
    }
}

unsafe class TestByRefFieldAddressEquality
{
    class ClassWithInitializedByRefs
    {
        [FixedAddressValueType]
        public static readonly int MyByRef = 1234;

        public static nint HiddenGetAddress() => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in MyByRef));
    }

    class ClassWithUninitializedByRefs
    {
        [FixedAddressValueType]
        public static readonly int MyByRef;

        public static nint HiddenGetAddress() => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in MyByRef));
    }

    class ClassTakingAddressOfInitialized
    {
        public static bool AreEqual = ClassWithInitializedByRefs.HiddenGetAddress() == ClassWithInitializedByRefs.HiddenGetAddress();
    }

    class ClassTakingAddressOfUninitialized
    {
        public static bool AreEqual = ClassWithUninitializedByRefs.HiddenGetAddress() == ClassWithUninitializedByRefs.HiddenGetAddress();
    }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(ClassTakingAddressOfInitialized));
        Assert.AreEqual(true, ClassTakingAddressOfInitialized.AreEqual);

        Assert.AreEqual(true, ClassTakingAddressOfUninitialized.AreEqual);
    }
}

unsafe class TestComInterfaceEntry
{
    struct MyVTableEntries
    {
        public ComWrappers.ComInterfaceEntry TinyImpl;
        public ComWrappers.ComInterfaceEntry SmallImpl;
    }

    class VtableEntries
    {
        [FixedAddressValueType]
        public static MyVTableEntries Entries;

        static VtableEntries()
        {
            Entries.TinyImpl.IID = new Guid(0x1234, 0x4567, 0x789A, 0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89);
            Entries.TinyImpl.Vtable = ITinyVtableImpl.VftablePtr;
            Entries.SmallImpl.IID = new Guid(0x4321, 0x7654, 0xA987, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87, 0x98);
            Entries.SmallImpl.Vtable = ISmallVtableImpl.VftablePtr;
        }
    }

    class ITinyVtableImpl
    {
        [FixedAddressValueType]
        private static readonly ITinyVtable Vtbl;

        public static nint VftablePtr => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in Vtbl));

        static ITinyVtableImpl()
        {
            Vtbl.Method = &Method;
        }
    }

    class ISmallVtableImpl
    {
        [FixedAddressValueType]
        private static readonly ISmallVtable Vtbl;

        public static nint VftablePtr => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in Vtbl));

        static ISmallVtableImpl()
        {
            Vtbl.Method1 = &Method;
            Vtbl.Method2 = &OtherMethod;
        }
    }

    public unsafe struct ITinyVtable
    {
        public delegate*<void> Method;
    }

    public unsafe struct ISmallVtable
    {
        public delegate*<void> Method1;
        public delegate*<void> Method2;
    }

    static void Method() { }
    static void OtherMethod() { }

    public static void Run()
    {
        Assert.IsPreinitialized(typeof(VtableEntries));
        Assert.AreEqual(ITinyVtableImpl.VftablePtr, VtableEntries.Entries.TinyImpl.Vtable);
        Assert.AreEqual(new Guid(0x1234, 0x4567, 0x789A, 0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89), VtableEntries.Entries.TinyImpl.IID);
        Assert.AreEqual(ISmallVtableImpl.VftablePtr, VtableEntries.Entries.SmallImpl.Vtable);
        Assert.AreEqual(new Guid(0x4321, 0x7654, 0xA987, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87, 0x98), VtableEntries.Entries.SmallImpl.IID);
    }
}

unsafe class TestPreinitializedBclTypes
{
    // Verify that (given that all of the other tests have passed), that a select number of BCL types
    // that depend on this optimization for high-performance scenarios are preinitialized.
    public static void Run()
    {
        Assert.IsPreinitialized(Type.GetType("System.Runtime.InteropServices.ComWrappers+VtableImplementations, System.Private.CoreLib"));
    }
}

static class Assert
{
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "Yep, we don't want to keep the cctor if it wasn't kept")]
    private static bool HasCctor(Type type)
    {
        return type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null) != null;
    }

    public static void IsPreinitialized(Type type, [CallerLineNumber] int line = 0)
    {
        if (HasCctor(type))
            throw new Exception($"{type} is not preinitialized. At line {line}.");
    }

    public static void IsLazyInitialized(Type type, [CallerLineNumber] int line = 0)
    {
        if (!HasCctor(type))
            throw new Exception($"{type} is not lazy initialized. At line {line}.");
    }

    public static void AreEqual(Guid v1, Guid v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(void* v1, void* v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {(nint)v1}, but get {(nint)v2}. At line {line}.");
    }

    public static unsafe void AreEqual(bool v1, bool v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(int v1, int v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static void AreEqual(string v1, string v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(long v1, long v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(ulong v1, ulong v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(float v1, float v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(double v1, double v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(nint v1, nint v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static unsafe void AreEqual(nuint v1, nuint v2, [CallerLineNumber] int line = 0)
    {
        if (v1 != v2)
            throw new Exception($"Expect {v1}, but get {v2}. At line {line}.");
    }

    public static void True(bool v, [CallerLineNumber] int line = 0)
    {
        if (!v)
            throw new Exception($"Expect True, but get {v}. At line {line}.");
    }

    public static void AreSame<T>(T v1, T v2, [CallerLineNumber] int line = 0) where T : class
    {
        if (v1 != v2)
            throw new Exception($"{v1} and {v2} is not the same. At line {line}.");
    }
}
