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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
// Feature requests: Add Reason for being banned to banlist
//  

//
// NOTE: Any unreleased code structure, dependencies, or files are subject to change without notice. Any dependencies you create around this code 
// are virtually guaranteed not to work in future builds. If I thought the code was release ready, it wouldn't be here.

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        enum MapField {id,version,songName,songSubName,authorName,rating,hashMd5,hashSha1};
      
        public class SongMap
            {
            public List<string> Fields = new List<string>(); // For performance, we need to extract these from the JSON.
            JSONObject song;

            void addfields(List<string> Fields)
                {
                foreach (var field in Fields)
                    {
                    string[] parts = field.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
//                        HashSet<Map> v;
  //                      MapDatabase.;
                       }
                    }
                }
            SongMap(JSONObject song)
            {
                this.song = song;
                Fields.Add(song["id"].Value);
                Fields.Add(song["version"].Value);
                Fields.Add(song["songName"].Value);
                Fields.Add(song["songSubName"].Value);
                Fields.Add(song["authorName"].Value);
                Fields.Add(song["rating"].Value);

                addfields(Fields);
            }
        }


        // Song primary key can be song ID, or level hashes. This dictionary is many:1
        public class MapDatabase
        {
            static ConcurrentDictionary<string, SongMap> MapLibrary = new ConcurrentDictionary<string, SongMap>();
            public static ConcurrentDictionary<string, HashSet<SongMap>> SearchDictionary;

          

            // Fast? Full Text Search
            List<SongMap> Search(string SearchKey)
            {
                List<SongMap> result = new List<SongMap>();
                List <HashSet<SongMap>> resultlist=new List <HashSet<SongMap>>();             

                string[] SearchParts = SearchKey.ToLower().Split(new char[] {' '},StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in SearchParts)
                    {
                    HashSet <SongMap> partresult;

                    if (!SearchDictionary.TryGetValue(part, out partresult)) return result; // Keyword must be found
                    resultlist.Add(partresult);
                    }

                // We now have n lists of candidates

                resultlist.Sort((L1, L2) => L1.Count.CompareTo(L2.Count));

                // We now have an optimized query

                // Compute all matches
                foreach (var map in resultlist[0])
                    {
                    for (int i=1;i<resultlist.Count;i++)
                        {
                        if (!resultlist[i].Contains(map)) goto next; // We can't continue from here :(    
                        }
                    result.Add(map);
                    next:
                    ;
                    }   

                return result;
                }

            public void RemoveMap(JSONObject song)
            {


            }
            public void AddDirectory()
                {

                }
                
            public void DownloadSongs()
                {

                }

            public void SaveDatabase()
                {

                }

            public void Loadatabase()
            {

            }

        }


    }
}