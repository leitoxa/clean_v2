/*
 * FolderConfig - Конфигурация очистки для отдельной папки
 * 
 * Хранит все настройки для одной папки, которые отображаются на вкладке.
 */

using System;
using System.Collections.Generic;

public class FolderConfig
{
    public string TabName { get; set; }
    public string FolderPath { get; set; }
    public int DaysOld { get; set; }
    public bool Recursive { get; set; }
    public bool UseRecycleBin { get; set; }
    public List<string> FileExtensions { get; set; }
    public bool Enabled { get; set; }

    public FolderConfig()
    {
        TabName = "Новая папка";
        FolderPath = "";
        DaysOld = 7;
        Recursive = false;
        UseRecycleBin = true;
        FileExtensions = new List<string>();
        Enabled = true;
    }

    public FolderConfig(string tabName, string folderPath)
    {
        TabName = tabName;
        FolderPath = folderPath;
        DaysOld = 7;
        Recursive = false;
        UseRecycleBin = true;
        FileExtensions = new List<string>();
        Enabled = true;
    }
}
