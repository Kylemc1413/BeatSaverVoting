using IPA;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IPALogger = IPA.Logging.Logger;

namespace BeatSaverVoting
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static string beatsaverURL = "https://beatsaver.com";
        internal static string votedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> votedSongs = new Dictionary<string, SongVote>();

        public enum VoteType { Upvote, Downvote };
        public class SongVote
        {
            public string key;
            [JsonConverter(typeof(StringEnumConverter))]
            public VoteType voteType;

            public SongVote(string key, VoteType voteType)
            {
                this.key = key;
                this.voteType = voteType;
            }
        }

        [Init]
        public void Init(IPALogger pluginLogger)
        {
            Utilities.Logging.Log = pluginLogger;
        }

        [OnStart]
        public void OnApplicationStart()
        {
            BS_Utils.Utilities.BSEvents.lateMenuSceneLoadedFresh    += BSEvents_menuSceneLoadedFresh;
            BS_Utils.Utilities.BSEvents.gameSceneLoaded             += BSEvents_gameSceneLoaded1;

            if (!File.Exists(votedSongsPath))
                File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
            else
                votedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText(votedSongsPath, Encoding.UTF8));
        }

        public static void WriteVotes()
        {
            File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
        }

        private void BSEvents_gameSceneLoaded1()
        {
            UI.VotingUI.instance._lastSong = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level;
        }
        private void BSEvents_menuSceneLoadedFresh(ScenesTransitionSetupDataSO data)
        {
         //   Utilities.Logging.Log.Info("Menu Scene Loaded");
            UI.VotingUI.instance.Setup();
        }
    }
}
