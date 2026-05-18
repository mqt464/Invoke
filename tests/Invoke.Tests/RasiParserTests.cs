using Invoke.Core.Rasi;

namespace Invoke.Tests;

[TestClass]
public sealed class RasiParserTests
{
    [TestMethod]
    public void Parse_SupportsCommentsListsFunctionsAndIdentifiers()
    {
        var document = RasiParser.Parse(
            """
            /* block */
            configuration {
              // line
              modes: [drun, run, window];
              show-icons: true;
              xoffset: 12px;
              theme: env(APPDATA);
              inherit-probe: inherit;
            }
            """);

        var configuration = document.Sections["configuration"];

        Assert.AreEqual(RasiValueKind.List, configuration["modes"].Kind);
        CollectionAssert.AreEqual(
            new[] { "drun", "run", "window" },
            configuration["modes"].ListValue!.Select(static value => value.AsString()).ToArray());
        Assert.IsTrue(configuration["show-icons"].AsBoolean());
        Assert.AreEqual(12d, configuration["xoffset"].AsNumber());
        Assert.AreEqual(RasiValueKind.Function, configuration["theme"].Kind);
        Assert.AreEqual("env", configuration["theme"].FunctionName);
        Assert.AreEqual("APPDATA", configuration["theme"].FunctionArguments![0].AsString());
        Assert.AreEqual(RasiValueKind.Identifier, configuration["inherit-probe"].Kind);
        Assert.AreEqual("inherit", configuration["inherit-probe"].AsString());
    }

    [TestMethod]
    public void Parse_MergesSharedSelectorPropertiesAcrossSections()
    {
        var document = RasiParser.Parse(
            """
            window, mainbox {
              width: 50%;
              orientation: horizontal;
            }

            element selected {
              background-color: #123456;
            }
            """);

        Assert.AreEqual("50%", document.Sections["window"]["width"].AsString());
        Assert.AreEqual("horizontal", document.Sections["mainbox"]["orientation"].AsString());
        Assert.AreEqual("#123456", document.Sections["element selected"]["background-color"].AsString());
    }

    [TestMethod]
    public void Parse_SupportsScopedPropertyNamesWithEmbeddedColons()
    {
        var document = RasiParser.Parse(
            """
            configuration {
              display-script:git-branches: "Git Branches";
              icon-path: shell:downloads;
            }
            """);

        var configuration = document.Sections["configuration"];
        Assert.AreEqual("Git Branches", configuration["display-script:git-branches"].AsString());
        Assert.AreEqual("shell:downloads", configuration["icon-path"].AsString());
    }

    [TestMethod]
    public void Loader_MergesImportsThenThemeThenDocument()
    {
        using var workspace = new TestWorkspace();
        var basePath = workspace.GetPath("base.rasi");
        var themePath = workspace.GetPath("theme.rasi");
        var rootPath = workspace.GetPath("config.rasi");

        File.WriteAllText(
            basePath,
            """
            prompt {
              text: "Imported";
            }

            message {
              text-color: #aabbcc;
            }
            """);
        File.WriteAllText(
            themePath,
            """
            window {
              background-color: #101010;
            }

            prompt {
              text: "Theme";
            }
            """);
        File.WriteAllText(
            rootPath,
            """
            @import "base.rasi"
            @theme "theme.rasi"

            prompt {
              text: "Root";
            }

            window {
              border-color: #ffffff;
            }
            """);

        var document = new RasiLoader().LoadFile(rootPath);

        Assert.AreEqual("Root", document.Sections["prompt"]["text"].AsString());
        Assert.AreEqual("#101010", document.Sections["window"]["background-color"].AsString());
        Assert.AreEqual("#ffffff", document.Sections["window"]["border-color"].AsString());
        Assert.AreEqual("#aabbcc", document.Sections["message"]["text-color"].AsString());
    }
}
