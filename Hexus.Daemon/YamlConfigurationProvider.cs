using YamlDotNet.RepresentationModel;

namespace Hexus.Daemon;

public sealed class YamlConfigurationSource : FileConfigurationSource
{
    public string SectionRoot { get; set; } = "";

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new YamlConfigurationProvider(this);
    }
}

public sealed class YamlConfigurationProvider : FileConfigurationProvider
{
    public new YamlConfigurationSource Source { get; }

    public YamlConfigurationProvider(YamlConfigurationSource source) : base(source)
    {
        Source = source;
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public override void Load(Stream stream)
    {
        var yaml = new YamlStream();
        yaml.Load(new StreamReader(stream));

        if (yaml.Documents.Count is <= 0)
            return;

        var root = yaml.Documents[0].RootNode;
        VisitYamlNode(root, Source.SectionRoot);
    }

    private void VisitYamlNode(YamlNode node, string path)
    {
        switch (node.NodeType)
        {
            case YamlNodeType.Scalar:
                Data[path] = ((YamlScalarNode)node).Value;
                break;
            case YamlNodeType.Mapping:
                var mapping = (YamlMappingNode)node;

                foreach (var entry in mapping.Children)
                {
                    VisitYamlNode(entry.Value, $"{path}:{((YamlScalarNode)entry.Key).Value}");
                }

                break;
            case YamlNodeType.Sequence:
                var sequence = (YamlSequenceNode)node;

                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    VisitYamlNode(sequence.Children[i], $"{path}:{i}");
                }

                break;
        }
    }
}
