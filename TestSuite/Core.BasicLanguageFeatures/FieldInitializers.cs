using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.BasicLanguageFeatures;

public class FieldInitializers
{
    private BaseClass _baseClass = new BaseClass();

    private static List<BaseClass> _baseClassList = [new()];
}