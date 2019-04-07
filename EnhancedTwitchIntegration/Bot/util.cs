using StreamCore;
using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SongRequestManager;
using StreamCore.Utils;
using System.IO.Compression;
// Feature requests: Add Reason for being banned to banlist

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
            {
                string newFilePath = Path.Combine(target.FullName, file.Name);
                    try
                    {
                        file.CopyTo(newFilePath);
                    }
                    catch (Exception)
                    {
                    }
            }
        }

        public static string Backup(ParseState state)
        {
            string errormsg = Backup();
            if (errormsg=="") state.msg("SRManager files backed up.");
            return errormsg;
        } 
    public static string Backup()
        {
            DateTime Now = DateTime.Now;
            string BackupName = Path.Combine(RequestBotConfig.Instance.backuppath, $"SRMBACKUP-{Now.ToString("yyyy-MM-dd-HHmm")}.zip");

            Plugin.Log($"Backing up {Globals.DataPath}");
            try
            {
                if (!Directory.Exists(RequestBotConfig.Instance.backuppath))
                    Directory.CreateDirectory(RequestBotConfig.Instance.backuppath);

                ZipFile.CreateFromDirectory(Globals.DataPath, BackupName, System.IO.Compression.CompressionLevel.Fastest, true);
                RequestBotConfig.Instance.LastBackup = DateTime.Now.ToString();
                RequestBotConfig.Instance.Save();

                Plugin.Log($"Backup success writing {BackupName}");
                return success;
            }
        catch       
            {

            }
            Plugin.Log($"Backup failed writing {BackupName}");
            return $"Failed to backup to {BackupName}";
        }
           

    }
}