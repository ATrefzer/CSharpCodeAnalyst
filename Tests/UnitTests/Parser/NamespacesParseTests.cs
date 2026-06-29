using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Type resolution across deeply nested namespaces: classes in Level1 / Level1.Level2 /
///     Level1.Level2.Level3 reference each other and the root namespace in both directions, producing
///     cross-namespace Uses and Calls edges. The nested namespace structure is preserved on purpose so
///     the snippet still exercises namespace lookup. Migrated from the former Core.Namespaces approval
///     fixture (RootLevel + Level1/2/3 source files).
/// </summary>
[TestFixture]
public class NamespacesParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                using Demo.Level1;
                                using Demo.Level1.Level2;
                                using Demo.Level1.Level2.Level3;

                                namespace Demo
                                {
                                    public class RootClass
                                    {
                                        public void UseLevel1()
                                        {
                                            var obj1 = new Level1Class();
                                            obj1.DoSomething();
                                        }

                                        public void UseLevel2()
                                        {
                                            var obj2 = new Level2Class();
                                            obj2.ProcessData();
                                        }

                                        public void UseLevel3()
                                        {
                                            var obj3 = new Level3Class();
                                            obj3.DeepOperation();
                                        }
                                    }
                                }

                                namespace Demo.Level1
                                {
                                    public class Level1Class
                                    {
                                        private Level2Class? _level2;

                                        public void DoSomething()
                                        {
                                            _level2?.ProcessData();
                                        }

                                        public void CreateLevel2()
                                        {
                                            _level2 = new Level2Class();
                                        }
                                    }

                                    public class AnotherLevel1Class
                                    {
                                        public void WorkWithRoot()
                                        {
                                            var root = new RootClass();
                                            root.UseLevel1();
                                        }
                                    }
                                }

                                namespace Demo.Level1.Level2
                                {
                                    public class Level2Class
                                    {
                                        public void ProcessData()
                                        {
                                            var level3 = new Level3Class();
                                            level3.DeepOperation();
                                        }
                                    }

                                    public class Level2Processor
                                    {
                                        public void ProcessWithLevel1()
                                        {
                                            var level1 = new Level1Class();
                                            level1.DoSomething();
                                        }
                                    }
                                }

                                namespace Demo.Level1.Level2.Level3
                                {
                                    public class Level3Class
                                    {
                                        public void DeepOperation()
                                        {
                                            var root = new RootClass();
                                            root.UseLevel1();
                                        }
                                    }

                                    public class DeepestClass
                                    {
                                        public void ReachToTop()
                                        {
                                            var level1 = new Level1Class();
                                            level1.DoSomething();
                                        }
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "RootClass", "Level1Class", "AnotherLevel1Class", "Level2Class", "Level2Processor",
            "Level3Class", "DeepestClass"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void CrossNamespaceTypeUsages_AreDetected()
    {
        var expected = new[]
        {
            "Level1Class._level2 -> Level2Class",
            "Level1Class.DoSomething -> Level1Class._level2",
            "Level1Class.CreateLevel2 -> Level1Class._level2",
            "AnotherLevel1Class.WorkWithRoot -> RootClass",
            "Level2Class.ProcessData -> Level3Class",
            "Level2Processor.ProcessWithLevel1 -> Level1Class",
            "DeepestClass.ReachToTop -> Level1Class",
            "Level3Class.DeepOperation -> RootClass",
            "RootClass.UseLevel1 -> Level1Class",
            "RootClass.UseLevel2 -> Level2Class",
            "RootClass.UseLevel3 -> Level3Class"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }

    [Test]
    public void CrossNamespaceMethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "Level1Class.DoSomething -> Level2Class.ProcessData",
            "AnotherLevel1Class.WorkWithRoot -> RootClass.UseLevel1",
            "Level2Class.ProcessData -> Level3Class.DeepOperation",
            "Level2Processor.ProcessWithLevel1 -> Level1Class.DoSomething",
            "Level3Class.DeepOperation -> RootClass.UseLevel1",
            "DeepestClass.ReachToTop -> Level1Class.DoSomething",
            "RootClass.UseLevel1 -> Level1Class.DoSomething",
            "RootClass.UseLevel2 -> Level2Class.ProcessData",
            "RootClass.UseLevel3 -> Level3Class.DeepOperation"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
