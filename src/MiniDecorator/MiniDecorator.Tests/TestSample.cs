using System;
using Humanizer;
using Xunit;

namespace MiniDecorator.Tests;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
public sealed class AutoNotifyAttribute : Attribute;

public sealed class TestSample
{
    [Fact]
    public void Test1()
    {
        Sample t = new Sample();
        
        Assert.Equal(t.coo.Pascalize(), t.Generated_coo.Pascalize());
    }
    
}

public partial class Sample
{
    [AutoNotify]
    public string foo = "foo";
    
    [AutoNotify]
    public string coo { get; set; } = "abe";
}