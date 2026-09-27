namespace EasyDisk.Models;

public class SearchItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }

    public string SizeText => FolderItem.Format(Size);
    public string ModifiedText => Modified == default ? "" : Modified.ToString("yyyy-MM-dd HH:mm");
}
