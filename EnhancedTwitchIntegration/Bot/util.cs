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
using SongRequestManager.RequestBotConfig;
using StreamCore.Utils;
using System.IO.Compression;
// Feature requests: Add Reason for being banned to banlist

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {



        public static string Backup(ParseState state)
        {
            string errormsg = Backup();
            if (errormsg=="") state.msg("SRManager files backed up.");
            return errormsg;
        } 
    public static string Backup()
        {
            DateTime Now = DateTime.Now;
            string BackupName = Path.Combine(RequestBotConfig.RequestBotConfig.Instance.backuppath, $"SRMBACKUP-{Now.ToString("yyyy-MM-dd-HHmm")}.zip");

            Plugin.Log($"Backing up {Globals.DataPath}");
            try
            {
                if (!Directory.Exists(RequestBotConfig.RequestBotConfig.Instance.backuppath))
                    Directory.CreateDirectory(RequestBotConfig.RequestBotConfig.Instance.backuppath);

                ZipFile.CreateFromDirectory(Globals.DataPath, BackupName, CompressionLevel.Fastest, true);
                RequestBotConfig.RequestBotConfig.Instance.LastBackup = DateTime.Now.ToString();
                RequestBotConfig.RequestBotConfig.Instance.Save();

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