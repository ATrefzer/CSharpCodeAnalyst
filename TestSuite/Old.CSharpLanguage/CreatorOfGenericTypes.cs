namespace CSharpLanguage;

internal class ProjectFile : XmlFile<Project>
{
}

internal class CreatorOfGenericTypes
{
    private ProjectFile _file;

    private void Create()
    {
        var x = new List<Project>();
        var y = new XmlFile<Project>();
    }
}