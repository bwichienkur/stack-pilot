using StackPilot.Application.DTOs;
using StackPilot.Infrastructure.Services;

namespace StackPilot.UnitTests;

public class AiCodeGenerationValidationTests
{
    [Fact]
    public void ValidateAndParseCodeGeneration_AcceptsValidPayload()
    {
        var json = """
            {
              "language": "csharp",
              "summary": "Add feature",
              "files": [
                { "path": "src/Foo.cs", "content": "class Foo {}", "language": "csharp" }
              ]
            }
            """;

        var result = AiService.ValidateAndParseCodeGeneration(json);
        Assert.Equal("Add feature", result.Summary);
        Assert.Single(result.Files);
        Assert.Equal("src/Foo.cs", result.Files[0].Path);
    }

    [Fact]
    public void ValidateAndParseCodeGeneration_RejectsMissingFiles()
    {
        var json = """{"language":"csharp","summary":"x","files":[]}""";
        Assert.Throws<InvalidOperationException>(() => AiService.ValidateAndParseCodeGeneration(json));
    }
}
