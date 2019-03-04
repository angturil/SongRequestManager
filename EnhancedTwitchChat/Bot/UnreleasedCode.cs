using EnhancedTwitchChat.Chat;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EnhancedTwitchChat.Bot
{
    public partial class RequestBot : MonoBehaviour
    {
        // BEGIN EXTRA FEATURES SECTION

        private void loaddecks(TwitchUser requestor, string request)
        {
            createdeck(requestor, Config.Instance.DeckList.ToLower());
        }

        private void createdeck(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            string[] decks = request.Split(new char[] { ',', ' ', '\t' });

            if (decks[0] == "")
            {
                QueueChatMessage($"usage: deck <deckname> ... omit <>'s.");
                return;
            }

            string msg = "deck";
            if (decks.Length > 1) msg += "s";

            msg += ": ";

            foreach (string req in decks)
            {

                try
                {
                    string DeckFile = Path.Combine(datapath,  req + ".deck");

                    string fileContent = File.ReadAllText(DeckFile);

                    string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);


                    if (integerStrings.Length > 0)
                    {
                        deck[req] = fileContent;
                        Commands[req] = drawcard;
                        msg += ($"!{req} ({integerStrings.Length} cards) ");
                    }

                }
                catch (Exception e)
                {
                    msg += ($"!{req} (invalid) ");
                }

            }

            QueueChatMessage(msg);


        }

        private void decklist(TwitchUser requestor, string request)
        {
            string decks = "";
            foreach (var item in deck)
            {
                decks += "!" + item.Key + " ";
            }

            if (decks == "")
                QueueChatMessage("No decks loaded.");
            else
                QueueChatMessage(decks);
        }


        private void unloaddeck(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            if (Commands.ContainsKey(request) && deck.ContainsKey(request))
            {
                Commands.Remove(request);
                QueueChatMessage($"{request} unloaded.");
            }
        }


        private void drawcard(TwitchUser requestor, string fullrequest)
        {

            if (QueueOpen == false && !requestor.isMod && !requestor.isBroadcaster)
            {
                Commands["usermessage"].Invoke(requestor, "Queue is currently closed.");
                return;
            }


            string[] reqparts = fullrequest.Split(new char[] { ' ' }, 2);

            var request = reqparts[0];

            if (reqparts.Length > 1)
            {
                if (requestor.isBroadcaster)
                {
                    string queuefile = Path.Combine(datapath,  request + ".deck");
                    File.AppendAllText(queuefile, "," + reqparts[1]);
                    deck[request] += "," + reqparts[1];
                    QueueChatMessage($"Added {reqparts[1]} to deck {request}.");
                    return;
                }
            }

            while (true)
            {

                string[] integerStrings = deck[request].Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (integerStrings.Length > 0)
                {
                    int entry = generator.Next(0, integerStrings.Length);
                    string newlist = "";

                    for (int x = 0; x < entry; x++)
                    {
                        newlist += integerStrings[x] + ",";
                    }

                    for (int y = entry + 1; y < integerStrings.Length; y++)
                    {
                        newlist += integerStrings[y] + ",";
                    }

             
                    deck[request] = newlist;

                    string songid = integerStrings[entry];

                    if (duplicatelist.Contains(songid)) continue;
                    if (SongBlacklist.Songs.ContainsKey(integerStrings[entry])) continue;
                    if (IsInQueue(songid)) continue;

                    Commands["bsr"]?.Invoke(requestor, integerStrings[entry]);


                }
                else
                {
                    QueueChatMessage("Deck is empty.");
                }

                break;
            }

        }


    }
}