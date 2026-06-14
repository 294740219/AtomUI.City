namespace AtomUI.City.Presentation;

public interface IViewRegistry : IViewLocator
{
    void Register(ViewDescriptor descriptor);

    void RegisterManifest(IEnumerable<ViewDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        foreach (var descriptor in descriptors)
        {
            Register(descriptor);
        }
    }

    int RevokePlugin(string pluginId);

    int RevokeContribution(string contributionId);
}
