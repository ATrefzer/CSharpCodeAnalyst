namespace CSharpLanguage;

internal record RecordA
{
    private RecordB _recordB;
}

internal record RecordB
{
    private RecordA _recordA;
}