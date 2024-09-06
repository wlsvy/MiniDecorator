using System;
using Humanizer;
using Xunit;

namespace MiniDecorator.Tests;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public sealed class AutoNotifyAttribute : Attribute;

public sealed class TestSample
{
    [Fact]
    public void Test1()
    {
        Sample t = new Sample();
        Assert.Equal("Foo", t.Foo.Pascalize());
    }
    
}

public partial class Sample
{
    [AutoNotify]
    public string foo = "foo";
}