namespace ElectronApp.Models;

public class n8nResponse
{
    public List<string> content { get; set; }
}

public class RootObject
{
    public List<DataItem> data { get; set; }
}

public class DataItem
{
    public List<string> content { get; set; }
}
