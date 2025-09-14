﻿using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using System.Windows.Media.Imaging;

namespace CSharpCodeAnalyst.CycleArea;

internal class Sorter : Comparer<CodeElementLineViewModel>
{
    public override int Compare(CodeElementLineViewModel? x, CodeElementLineViewModel? y)
    {
        if (x == null || y == null)
        {
            throw new ArgumentNullException();
        }

        return string.Compare(x.FullName, y.FullName, StringComparison.InvariantCulture);
    }
}

public class CodeElementLineViewModel(CodeElement e)
{
    public BitmapImage? Icon => CodeElementIconMapper.GetIcon(e.ElementType);

    public CodeElementType ElementType { get; set; } = e.ElementType;
    public string ElementTypeName { get; set; } = e.ElementType.ToString();

    public string FullName { get; set; } = e.FullName;
}