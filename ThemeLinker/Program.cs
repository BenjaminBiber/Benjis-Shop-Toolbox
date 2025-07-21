using System;
using System.Collections.Generic;
using System.IO;

namespace ThemeLinker
{
    class Program
    {
        const string DefaultRepoPath = "/Pfad/zum/repo-ordner";
        const string DefaultShopThemesPath = "/Pfad/zum/shop-themes-ordner";

        static void Main(string[] args)
        {
            string repoPath = DefaultRepoPath;
            string shopPath = DefaultShopThemesPath;
            bool auto = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--repo" && i + 1 < args.Length)
                {
                    repoPath = args[++i];
                }
                else if (args[i] == "--shop" && i + 1 < args.Length)
                {
                    shopPath = args[++i];
                }
                else if (args[i] == "--auto")
                {
                    auto = true;
                }
            }

            repoPath = Path.GetFullPath(repoPath);
            shopPath = Path.GetFullPath(shopPath);

            if (!Directory.Exists(repoPath))
            {
                Console.Error.WriteLine($"Repo-Verzeichnis nicht gefunden: {repoPath}");
                return;
            }
            if (!Directory.Exists(shopPath))
            {
                Console.Error.WriteLine($"Shop-Themes-Verzeichnis nicht gefunden: {shopPath}");
                return;
            }

            var themes = new List<(string Name, string Path)>();
            foreach (var dir in Directory.EnumerateDirectories(repoPath, "Themes", SearchOption.AllDirectories))
            {
                foreach (var themeDir in Directory.EnumerateDirectories(dir))
                {
                    themes.Add((Path.GetFileName(themeDir), themeDir));
                }
            }

            if (themes.Count == 0)
            {
                Console.WriteLine("Keine Themes gefunden.");
                return;
            }

            Console.WriteLine($"{"Theme",-30} {"Repo-Pfad",-60} {"Symlink",-8}");
            foreach (var t in themes)
            {
                string linkPath = Path.Combine(shopPath, t.Name);
                bool linkExists = File.Exists(linkPath) || Directory.Exists(linkPath);
                bool isLink = false;
                if (linkExists)
                {
                    try
                    {
                        var attr = File.GetAttributes(linkPath);
                        isLink = attr.HasFlag(FileAttributes.ReparsePoint);
                    }
                    catch { }
                }

                Console.WriteLine($"{t.Name,-30} {t.Path,-60} {(isLink ? "Ja" : "Nein"),-8}");

                if (auto)
                {
                    if (!isLink)
                    {
                        Directory.CreateSymbolicLink(linkPath, t.Path);
                    }
                    else if (isLink && !auto)
                    {
                        // not removing automatically
                    }
                }
                else
                {
                    if (!isLink)
                    {
                        Console.Write($"Symlink für {t.Name} anlegen? [y/N] ");
                        var input = Console.ReadLine();
                        if (input?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Directory.CreateSymbolicLink(linkPath, t.Path);
                            Console.WriteLine(" -> erstellt.");
                        }
                    }
                    else
                    {
                        Console.Write($"Symlink für {t.Name} entfernen? [y/N] ");
                        var input = Console.ReadLine();
                        if (input?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            File.Delete(linkPath);
                            Console.WriteLine(" -> entfernt.");
                        }
                    }
                }
            }
        }
    }
}
