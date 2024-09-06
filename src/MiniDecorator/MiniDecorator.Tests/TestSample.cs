using Xunit;

namespace MiniDecorator.Tests;

public sealed class TestSample
{
    [Fact]
    public void Test1()
    {

    }
    
}

public class Sample
{
    [AutoNotify]
    public string foo = "n";
}