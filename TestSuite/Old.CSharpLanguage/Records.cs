namespace CSharpLanguage
{
    internal record RecordA
    {
        RecordB _recordB;
    }

    internal record RecordB
    {
        RecordA _recordA;
    }
}