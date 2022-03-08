using ChatCore.Models.Twitch;
using System.Collections.Generic;
using System;
using Xunit;

namespace SongRequestManager.Tests
{
    public class AddSongToQueueTest
    {
        private TwitchUser generateFakeTwitchUser(string id)
        {
            return new TwitchUser("{\"Id\": \"" + id + "\"}");
        }
        

        [Fact]
        public void AddSongToQueue_onEmptyQueue_insertsAtIndex0()
        {
            List<SongRequest> queue = new();            
            int insertionPoint = RequestBot.GetQueueInsertionPoint(queue, RequestBot.QueueInsertionStyle.RoundRobin, "A");
            Assert.Equal(0, insertionPoint);
        }

        [Fact]
        public void AddSongToQueue_onQueueWhereEveryoneHasOneSong_insertsAtEnd()
        {
            List<SongRequest> queue = new()
            {
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("B"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("C"), DateTime.UtcNow),
            };
            int insertionPoint = RequestBot.GetQueueInsertionPoint(queue, RequestBot.QueueInsertionStyle.RoundRobin, "D");
            Assert.Equal(3, insertionPoint);
        }

        [Fact]
        public void AddSongToQueue_onIfThisIsRequestersFirstSongAndSomeoneHasMultipleSongsInQueue_insertBeforeAnyonesSecondSong()
        {
            List<SongRequest> queue = new()
            {
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("B"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("C"), DateTime.UtcNow),
            };
            int insertionPoint = RequestBot.GetQueueInsertionPoint(queue, RequestBot.QueueInsertionStyle.RoundRobin, "D");
            Assert.Equal(2, insertionPoint);
        }

        [Fact]
        public void AddSongToQueue_onIfThisIsRequestersSecondSongAnd_insertBeforeAnyonesThirdSong()
        {
            List<SongRequest> queue = new()
            {
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("B"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("D"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("B"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("C"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("A"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("C"), DateTime.UtcNow),
                new SongRequest(null, generateFakeTwitchUser("C"), DateTime.UtcNow),
            };
            int insertionPoint = RequestBot.GetQueueInsertionPoint(queue, RequestBot.QueueInsertionStyle.RoundRobin, "D");
            Assert.Equal(6, insertionPoint);
        }
    }
}