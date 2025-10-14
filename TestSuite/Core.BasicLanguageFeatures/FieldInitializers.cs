using System.Collections.Generic;

namespace Core.BasicLanguageFeatures;

public class FieldInitializers
{

    private static List<BaseClass> _baseClassList = [new()];
    private BaseClass _baseClass = new();
}