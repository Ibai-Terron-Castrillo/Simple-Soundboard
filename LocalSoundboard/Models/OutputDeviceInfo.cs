namespace LocalSoundboard.Models;

public sealed record OutputDeviceInfo(int DeviceNumber, string Name)
{
    public string DisplayName => DeviceNumber < 0 ? Name : $"{Name} ({DeviceNumber})";

    public override string ToString()
    {
        return DisplayName;
    }
}
