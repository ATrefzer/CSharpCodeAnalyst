using CodeParserTests.UnitTests.Parser;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;

namespace CodeParserTests.UnitTests.DeadCode;

/// <summary>
///     End-to-end check of the dead code analysis against a real parse result: the synthetic graph tests
///     pin the rules, this fixture proves the rules match what the parser actually produces.
/// </summary>
[TestFixture]
public class DeadCodeParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public interface IService
                                      {
                                          void Run();
                                          void NeverCalled();
                                      }

                                      public class Service : IService
                                      {
                                          private readonly int _used = 1;
                                          private readonly int _unused = 2;

                                          public void Run() { Helper.Help(_used); }
                                          public void NeverCalled() { }
                                          private void PrivateUnused() { }
                                      }

                                      public static class Helper
                                      {
                                          public static void Help(int x) { }
                                          public static void UnusedHelp() { }
                                      }

                                      public class DeadClass
                                      {
                                          public void A() { B(); }
                                          private void B() { }
                                      }

                                      public class Program
                                      {
                                          public static void Main()
                                          {
                                              IService service = new Service();
                                              service.Run();
                                          }
                                      }
                                      """;

    private string[] Reported()
    {
        return DeadCodeAnalysis.Calculate(Graph).Select(f => PathOf(f.Element)).ToArray();
    }

    [Test]
    public void Calculate_ReportsExactlyTheUnreferencedElements()
    {
        // Service and Helper are reached from Main, IService.Run through the interface call.
        // DeadClass only calls itself, Program is only the entry point holder.
        Assert.That(Reported(), Is.EquivalentTo(new[]
        {
            "DeadClass",
            "Program",
            "Helper.UnusedHelp",
            "IService.NeverCalled",
            "Service.NeverCalled",
            "Service.PrivateUnused",
            "Service._unused"
        }));
    }

    [Test]
    public void Calculate_MainHolder_CarriesEntryPointHint()
    {
        var program = DeadCodeAnalysis.Calculate(Graph).Single(f => PathOf(f.Element) == "Program");

        Assert.That(program.Hints.HasFlag(DeadCodeHint.EntryPoint), Is.True);
    }

    [Test]
    public void Calculate_UncalledContract_LinksToItsImplementation()
    {
        var findings = DeadCodeAnalysis.Calculate(Graph);
        var contract = findings.Single(f => PathOf(f.Element) == "IService.NeverCalled");
        var implementation = findings.Single(f => PathOf(f.Element) == "Service.NeverCalled");

        Assert.Multiple(() =>
        {
            Assert.That(contract.Hints.HasFlag(DeadCodeHint.ContractNeverCalled), Is.True);
            Assert.That(contract.RelatedMembers.Select(PathOf), Is.EquivalentTo(new[] { "Service.NeverCalled" }));

            Assert.That(implementation.Hints.HasFlag(DeadCodeHint.ImplementsDeadContract), Is.True);
            Assert.That(implementation.RelatedMembers.Select(PathOf), Is.EquivalentTo(new[] { "IService.NeverCalled" }));
        });
    }
}
